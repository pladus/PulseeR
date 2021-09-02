namespace Worker

open System
open System.Collections.Concurrent
open System.Reflection
open System.Runtime.CompilerServices
open System.Threading
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Options
open Models
open System.Threading.Tasks
open Microsoft.Extensions.Logging
open Schedule


module Worker =

    let trimSecs (dt: DateTime) =
        DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0)

    let updateMap (scheduleTable : seq<string * Schedule>) timestamp =
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

    let private extractKeyedRoutine key (a: Assembly []) =
        a
        |> extractRoutines
        |> Seq.find (fun x -> x.GetCustomAttribute<RoutineKeyAttribute>().Key = key)


    let private processRoutine (routineDescriptor: RoutineDescriptor) (container: IServiceProvider) =
        async {
            use scope = container.CreateScope()

            let routine =
                scope.ServiceProvider.GetService(routineDescriptor.Type) :?> IRoutine

            use cts = new CancellationTokenSource()
            cts.CancelAfter(TimeSpan.FromMilliseconds(float routineDescriptor.Strategy.Timeout))
            do! routine.ExecuteAsync(cts.Token) |> Async.AwaitTask
        }

    let private tryProcessRoutine (routineDescriptor: RoutineDescriptor)
                                  (container: IServiceProvider)
                                  (logger: ILogger<'a>)
                                  =
        async {
            try
                do! processRoutine routineDescriptor container
            with e -> logger.LogInformation(e, "Error occurred while executing {key}", routineDescriptor.Strategy.Key)
        }

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

                let descriptorMap =
                    ops.Routines
                    |> Seq.map (fun x -> x.Strategy.Key, x)
                    |> Map.ofSeq

                logger.LogInformation("Pulsing started with sleep interval {sleep} millis... ", sleepTime)

                let scheduleTable =
                    ops.Routines
                    |> Seq.map (fun x -> (x.Strategy.Key, Schedule(x.Strategy.Schedule)))

                let mutable nextFireMap = updateMap scheduleTable (DateTime.Now |> trimSecs)
                    

                while (not stoppingToken.IsCancellationRequested) do

                    let now = DateTime.Now |> trimSecs

                    let keysToExecute =
                        nextFireMap
                        |> Seq.filter (fun x -> x.Value = now)
                        |> Seq.map (fun x -> x.Key)

                    let! executed =
                        keysToExecute
                        |> Seq.map (fun x -> tryProcessRoutine descriptorMap.[x] container logger)
                        |> Async.Parallel

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
