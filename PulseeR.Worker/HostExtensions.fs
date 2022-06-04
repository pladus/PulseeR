namespace PulseeR.HostExtensions

open System.Runtime.CompilerServices
open Microsoft.Extensions.Hosting
open System.Reflection
open Microsoft.Extensions.DependencyInjection
open Models
open Work

module Utils =


    type Maybe<'a> = Some of 'a  | None

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
             with _ -> ""),
            x)
        |> Seq.filter (fun x -> fst x <> "")

    let internal tryCreateRoutineDescriptor (routinesTable: Map<string, System.Type>) (x: RoutineStrategy)  = 
         try Some(RoutineDescriptor(routinesTable[x.Key], x))
         with e -> 
            printf "Error when resolving %s: %s" x.Key e.Message
            None

[<Extension>]
type HostBuilderExtensions() =
    /// Add the PulseeR worker
    [<Extension>]
     static member AddPulseeR(builder: IHostBuilder, jobsAssemblies: Assembly []) =
        builder.ConfigureServices(fun hb s ->
            let config = hb.Configuration
            let ops = WorkerOptions()
            ops.SleepMilliseconds <- uint64 config["WorkerOptions:SleepMilliseconds"]
            ops.Routines <- Array.empty

            let routinesTable = jobsAssemblies |> Utils.toKeyedRoutinesCorrelations

            for (k, t) in routinesTable do
                try
                    let str = RoutineStrategy()
                    str.Concurrency <- uint16 config[$"WorkerOptions:Routines:{k}:Concurrency"]
                    str.Key <- k
                    str.Schedule <- config[$"WorkerOptions:Routines:{k}:Schedule"]
                    str.Timeout <- uint64 config[$"WorkerOptions:Routines:{k}:Timeout"]

                    ops.Routines <-
                        ops.Routines
                        |> Array.append [| RoutineDescriptor(t, str) |]

                    s.AddTransient(t) |> ignore
                with e -> printf "%s" e.Message

            s.AddSingleton<WorkerOptions>(ops) |> ignore
            s.AddHostedService<PulseeR>() |> ignore)

    /// Add the PulseeR worker
    [<Extension>]
     static member AddPulseeR(builder: IHostBuilder, jobsAssemblies: Assembly [], configurationBuilder: WorkerConfiguration -> unit) =
        builder.ConfigureServices(fun hb s ->

            let config = WorkerConfiguration()
            configurationBuilder config
            let routinesTable = 
                jobsAssemblies 
                |> Utils.toKeyedRoutinesCorrelations
                |> Seq.filter (fun x -> config.Routines |> Array.map (fun r -> r.Key) |> Array.contains (fst x))
                |> Map.ofSeq 
            
            let ops = WorkerOptions()
            ops.SleepMilliseconds <- uint64 config.SleepMilliseconds
            ops.Routines <- config.Routines 
                |> Seq.map (Utils.tryCreateRoutineDescriptor routinesTable)
                |> Seq.fold (fun s x -> match x with 
                                        | Utils.Some v -> s |> List.append [v] 
                                        | Utils.None -> s) List.empty
                |> Array.ofSeq


            for r in ops.Routines do
                try s.AddTransient(r.Type) |> ignore
                with e -> printf "%s" e.Message

            s.AddSingleton<WorkerOptions>(ops) |> ignore
            s.AddHostedService<PulseeR>() |> ignore)