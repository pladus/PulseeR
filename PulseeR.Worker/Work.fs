module Work

open System
open System.Reflection
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks.Dataflow
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Schedule
open Models

///60 seconds
let private defaultSleepTime = 
    uint64 (60 * 1000)

let private trimSecs (dt: DateTime) =
    DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0)

let private updateMap (scheduleTable: seq<string * Schedule>) timestamp =
    scheduleTable
    |> Seq.map (fun x -> fst x, (snd x).GetLaunchAfter timestamp)
    |> Map.ofSeq

let private selectRoutines (a: Assembly) =
    a.GetTypes()
    |> Seq.filter (fun x ->
        x.GetInterfaces()
        |> Seq.exists ((=) typeof<IRoutine>))


let internal toKeyedRoutinesCorrelations (a: Assembly []) =
    a
    |> Seq.map selectRoutines
    |> Seq.reduce (fun x y -> x |> Seq.append y)
    |> Seq.map (fun x ->
        (try
            x.GetCustomAttribute<RoutineKeyAttribute>().Key
         with _ -> String.Empty),
        x)
    |> Seq.filter (fun x -> fst x <> String.Empty)

let private processRoutine (routineDescriptor: RoutineDescriptor)
                           (container: IServiceProvider)
                           (stoppingToken: CancellationToken)
                           (logger : ILogger)
                           =
    async {

        logger.LogDebug ("Creating scope for routine {key}", routineDescriptor.Strategy.Key)
        use scope = container.CreateScope()
        
        logger.LogDebug ("Instantiating routine {key}", routineDescriptor.Strategy.Key)
        let routine =
            scope.ServiceProvider.GetService(routineDescriptor.Type) :?> IRoutine

        stoppingToken.ThrowIfCancellationRequested()

        use cts =
            CancellationTokenSource.CreateLinkedTokenSource stoppingToken

        cts.CancelAfter(TimeSpan.FromMilliseconds(float routineDescriptor.Strategy.Timeout))
        cts.Token.UnsafeRegister (new Action<obj>(fun _ -> 
            logger.LogInformation("Routine cancelled {key}", routineDescriptor.Strategy.Key) 
            ()), null) 
            |> ignore

        logger.LogDebug ("Start executing routine {key}", routineDescriptor.Strategy.Key)
        do! routine.ExecuteAsync(cts.Token) |> Async.AwaitTask
    }

let private tryProcessRoutine (routineDescriptor: RoutineDescriptor)
                              (container: IServiceProvider)
                              (logger: ILogger)
                              (stoppingToken: CancellationToken)
                              =
    async {
        try
            logger.LogDebug("{key} fired", routineDescriptor.Strategy.Key)
            do! processRoutine routineDescriptor container stoppingToken logger
            logger.LogDebug("{key} finished", routineDescriptor.Strategy.Key)
        with e -> logger.LogError(e, "Error occurred while executing {key}", routineDescriptor.Strategy.Key)
    }

let private buildActionBlock (routineDescriptor: RoutineDescriptor) (container: IServiceProvider) logger stoppingToken =
    let action =
        Func<unit, Task>(fun _ ->
            tryProcessRoutine routineDescriptor container logger stoppingToken
            |> Async.StartAsTask :> Task)

    let ops = ExecutionDataflowBlockOptions()
    ops.BoundedCapacity <- int routineDescriptor.Strategy.Concurrency
    ops.MaxDegreeOfParallelism <- int routineDescriptor.Strategy.Concurrency

    ActionBlock(action, ops)

let private startRoutine (key: string) (worker: ActionBlock<unit>) (logger: ILogger) =
    match worker.Post(()) with
    | false -> logger.LogInformation("Concurrency slots for {key} exceeded. Execution skipped", key)
    | _ -> ()

type internal PulseeR(logger: ILogger<PulseeR>, ops: WorkerOptions, container: IServiceProvider) =
    inherit BackgroundService()

    override this.ExecuteAsync(stoppingToken) =
        async { return! this.StartWork stoppingToken ops logger container }
        |> Async.StartAsTask :> Task


    member private _.StartWork stoppingToken
                               (ops: WorkerOptions)
                               (logger: ILogger)
                               (container: IServiceProvider)
                               =
        async {
            let sleepTime =
                if ops.SleepMilliseconds = (uint64 0) then defaultSleepTime else ops.SleepMilliseconds

            let workers =
                ops.Routines
                |> Seq.map (fun x -> x.Strategy.Key, buildActionBlock x container logger stoppingToken)
                |> Map.ofSeq

            logger.LogInformation("Pulsing started with sleep interval {sleep} millis... ", sleepTime)

            let scheduleTable =
                ops.Routines
                |> Seq.map (fun x -> (x.Strategy.Key, Schedule(x.Strategy.Schedule)))

            for r in ops.Routines do
                logger.LogInformation
                    ("{key} scheduled with [Schedule: {schedule}, Concurrency: {concurrency}, Timeout: {timeout}]",
                     r.Strategy.Key,
                     r.Strategy.Schedule,
                     r.Strategy.Concurrency,
                     r.Strategy.Timeout)

            let mutable nextFireMap =
                updateMap scheduleTable (DateTime.Now |> trimSecs)

            while (not stoppingToken.IsCancellationRequested) do

                let now = DateTime.Now |> trimSecs

                nextFireMap
                |> Seq.filter (fun x -> x.Value = now)
                |> Seq.map (fun x -> x.Key)
                |> Seq.map (fun x -> startRoutine x workers.[x] logger)
                |> Seq.toArray
                |> ignore

                nextFireMap <- updateMap scheduleTable now

                do! Async.Sleep (int sleepTime)

            logger.LogInformation("Pulsing finished. ")
        }
