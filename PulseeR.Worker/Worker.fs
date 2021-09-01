namespace Worker

open System
open System
open System.Collections.Concurrent
open System.Reflection
open System.Runtime.CompilerServices
open System.Threading
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Configuration
open Models
open System.Threading.Tasks
open Microsoft.Extensions.Logging

module Worker =

    let private selectRoutines (a: Assembly) =
        a.GetTypes()
        |> Seq.filter (fun x ->
            x.GetInterfaces()
            |> Seq.exists ((=) typeof<IRoutine>))

    let private extractRoutines (a: Assembly []) =
        a
        |> Seq.map selectRoutines
        |> Seq.reduce (fun x y -> x |> Seq.append y)

    let private extractKeyedRoutine key (a: Assembly [])  =
        a
        |> extractRoutines
        |> Seq.find (fun x -> x.GetCustomAttribute<RoutineKeyAttribute>().Key = key)


    type PulseeR(logger: ILogger<PulseeR>,
                 ops: WorkerOptions,
                 container: IServiceProvider,
                 assemblyInfo: RoutinesAssemblyInfo) =
        inherit BackgroundService()

        override this.ExecuteAsync(stoppingToken) =
            async { return! this.StartWork stoppingToken this.ServiceMap ops assemblyInfo logger container }
            |> Async.StartAsTask :> Task

        member this.ServiceMap =
            ConcurrentDictionary<string, RoutineDescriptor>()

        member private _.StartWork stoppingToken
                                   (servicesMap: ConcurrentDictionary<string, RoutineDescriptor>)
                                   (ops: WorkerOptions)
                                   (assemblyInfo: RoutinesAssemblyInfo)
                                   (logger: ILogger<PulseeR>)
                                   (container: IServiceProvider)
                                   =
            async {
                let sleepTime = ops.SleepMilliseconds

                logger.LogInformation("Pulsing started with sleep interval {sleep} millis... ", sleepTime)

                while (not stoppingToken.IsCancellationRequested) do
                    use scope = container.CreateScope()
                    let toExecute = "MyLol"
                    
                    let routineOps = ops.JobOptions |> Seq.tryFind (fun x -> x.Key = toExecute)
                    match routineOps with
                    | Some o -> 
                        let extractFun = extractKeyedRoutine toExecute
                        let serviceType =
                            servicesMap.GetOrAdd(toExecute, (fun x -> RoutineDescriptor(assemblyInfo.Assemblies |> extractFun, o)))
                        
                        let routine = scope.ServiceProvider.GetService(serviceType.RoutineType) :?> IRoutine
                        try
                            do! routine.ExecuteAsync(CancellationToken.None)
                        with
                        | e -> logger.LogError(e, "Error occured while executing {key}", toExecute)
                    | _ -> logger.LogError("Options not found for key {key}", toExecute)
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
                let config2 = hb.Configuration.Get<WorkerOptions>()

                let assemblyInfo = RoutinesAssemblyInfo(jobsAssemblies)
                let ops = WorkerOptions()
                let sec = config.GetSection(nameof WorkerOptions)
                config.Bind(ref ops)

                let types = jobsAssemblies |> extractRoutines

                s.AddSingleton<WorkerOptions>(ops) |> ignore

                s.AddSingleton<RoutinesAssemblyInfo>(assemblyInfo)
                |> ignore

                s.AddHostedService<PulseeR>() |> ignore

                for t in types do
                    s.AddTransient(t) |> ignore)
