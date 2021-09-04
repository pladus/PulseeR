module Work

open System
open System.Collections.Concurrent
open System.Reflection
open System.Runtime.CompilerServices
open System.Threading
open System.Threading.Tasks.Dataflow
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Models
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Schedule

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

let private extractRoutines (a: Assembly []) =
    a
    |> Seq.map selectRoutines
    |> Seq.reduce (fun x y -> x |> Seq.append y)

let private extractRoutinesTable (a: Assembly []) =
    a
    |> Seq.map selectRoutines
    |> Seq.reduce (fun x y -> x |> Seq.append y)
    |> Seq.map (fun x ->
        (try
            x.GetCustomAttribute<RoutineKeyAttribute>().Key
         with _ -> ""),
        x)
    |> Seq.filter (fun x -> fst x <> "")

let private processRoutine (routineDescriptor: RoutineDescriptor)
                           (container: IServiceProvider)
                           (stoppingToken: CancellationToken)
                           =
    async {
        use scope = container.CreateScope()

        let routine =
            scope.ServiceProvider.GetService(routineDescriptor.Type) :?> IRoutine

        stoppingToken.ThrowIfCancellationRequested()

        use cts =
            CancellationTokenSource.CreateLinkedTokenSource stoppingToken

        cts.CancelAfter(TimeSpan.FromMilliseconds(float routineDescriptor.Strategy.Timeout))
        do! routine.ExecuteAsync(cts.Token) |> Async.AwaitTask
    }

let private tryProcessRoutine (routineDescriptor: RoutineDescriptor)
                              (container: IServiceProvider)
                              (logger: ILogger<'a>)
                              (stoppingToken: CancellationToken)
                              =
    async {
        try
            logger.LogDebug("{key} fired", routineDescriptor.Strategy.Key)
            do! processRoutine routineDescriptor container stoppingToken
            logger.LogDebug("{key} finished", routineDescriptor.Strategy.Key)
        with e -> logger.LogError(e, "Error occurred while executing {key}", routineDescriptor.Strategy.Key)
    }

let private buildDataBlock (routineDescriptor: RoutineDescriptor) (container: IServiceProvider) logger stoppingToken =
    let action =
        Func<unit, Task>(fun _ ->
            tryProcessRoutine routineDescriptor container logger stoppingToken
            |> Async.StartAsTask :> Task)

    let ops = ExecutionDataflowBlockOptions()
    ops.BoundedCapacity <- routineDescriptor.Strategy.Concurrency
    ops.MaxDegreeOfParallelism <- routineDescriptor.Strategy.Concurrency

    ActionBlock(action, ops)

let private startRoutine (key: string) (worker: ActionBlock<unit>) (logger: ILogger<'a>) =
    match worker.Post(()) with
    | false -> logger.LogInformation("Concurrency cap for {key} achieved. Firing skipped", key)
    | _ -> ()

type PulseeR(logger: ILogger<PulseeR>, ops: WorkerStrategy, container: IServiceProvider) =
    inherit BackgroundService()

    override this.ExecuteAsync(stoppingToken) =
        async { return! this.StartWork stoppingToken ops logger container }
        |> Async.StartAsTask :> Task


    member private _.StartWork stoppingToken
                               (ops: WorkerStrategy)
                               (logger: ILogger<PulseeR>)
                               (container: IServiceProvider)
                               =
        async {
            let sleepTime =
                if ops.SleepMilliseconds = 0 then 5000 else ops.SleepMilliseconds

            logger.LogInformation("Pulsing started with sleep interval {sleep} millis... ", sleepTime)

            let workers =
                ops.Routines
                |> Seq.map (fun x -> x.Strategy.Key, buildDataBlock x container logger stoppingToken)
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

                do! Async.Sleep sleepTime

            logger.LogInformation("Pulsing finished. ")
        }

[<Extension>]
type HostBuilderExtensions() =
    /// Add the PulseeR worker
    [<Extension>]
    static member AddPulseeR(builder: IHostBuilder, jobsAssemblies: Assembly []) =
        builder.ConfigureServices(fun hb s ->
            let config = hb.Configuration
            let ops = WorkerStrategy()
            ops.SleepMilliseconds <- int config.["WorkerOptions:SleepMilliseconds"]
            ops.Routines <- Array.empty

            let routinesTable = jobsAssemblies |> extractRoutinesTable

            for (k, t) in routinesTable do
                try
                    let str = RoutineStrategy()
                    str.Concurrency <- int config.[$"WorkerOptions:Routines:{k}:Concurrency"]
                    str.Key <- k
                    str.Schedule <- config.[$"WorkerOptions:Routines:{k}:Schedule"]
                    str.Timeout <- int config.[$"WorkerOptions:Routines:{k}:Timeout"]

                    ops.Routines <-
                        ops.Routines
                        |> Array.append [| RoutineDescriptor(t, str) |]

                    s.AddTransient(t) |> ignore
                with e -> printf "%s" e.Message

            s.AddSingleton<WorkerStrategy>(ops) |> ignore
            s.AddHostedService<PulseeR>() |> ignore)
