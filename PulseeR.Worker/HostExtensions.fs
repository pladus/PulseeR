namespace PulseeR.HostExtensions

open System.Runtime.CompilerServices
open System.Threading
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open System.Reflection
open Microsoft.Extensions.DependencyInjection
open Models
open Work

module Utils =


    type Maybe<'a> =
        | Some of 'a
        | None

    let private selectRoutines (a: Assembly) =
        a.GetTypes()
        |> Seq.filter
            (fun x ->
                x.GetInterfaces()
                |> Seq.exists ((=) typeof<IRoutine>))

    let internal toKeyedRoutinesCorrelations (a: Assembly []) =
        a
        |> Seq.map selectRoutines
        |> Seq.reduce (fun x y -> x |> Seq.append y)
        |> Seq.map
            (fun x ->
                (try
                    x.GetCustomAttribute<RoutineKeyAttribute>().Key
                 with
                 | _ -> ""),
                x)
        |> Seq.filter (fun x -> fst x <> "")

    let internal tryCreateRoutineDescriptor (routinesTable: Map<string, System.Type>) (x: RoutineStrategy) =
        try
            Some(RoutineDescriptor(routinesTable.[x.Key], x))
        with
        | e ->
            printf "Error when resolving %s: %s" x.Key e.Message
            None

    let tryGetConfigValue parse path (config: IConfiguration) =
        let rawValue = config[path]

        match rawValue with
        | null ->
            printf $"parameter %s{path} not present in config"
            Option.None
        | _ ->
            try
                let res = parse rawValue
                Option.Some res
            with
            | e ->
                printf $"parameter %s{path} not valid and it parsed with error %s{e.Message}"
                Option.None


[<Extension>]
type HostBuilderExtensions() =
    /// Add the PulseeR worker
    [<Extension>]
    static member AddPulseeR(builder: IHostBuilder, jobsAssemblies: Assembly []) =
        builder.ConfigureServices
            (fun hb s ->
                try
                    let config = hb.Configuration
                    let ops = WorkerOptions()

                    let root =
                        Utils.tryGetConfigValue id "WorkerOptions" config

                    if root.IsNone then
                        failwith
                            "WorkerOptions not found - PulseeR not activated.
                If you need to configure routines with options WorkerOptions"

                    let sleep =
                        match Utils.tryGetConfigValue uint64 "WorkerOptions:SleepMilliseconds" config with
                        | None ->
                            let def = 10000UL
                            printf $"Timeout not set so it will be default %d{def}"
                            def
                        | Some value -> value

                    ops.SleepMilliseconds <- sleep
                    ops.Routines <- Array.empty

                    let routinesTable =
                        jobsAssemblies
                        |> Utils.toKeyedRoutinesCorrelations

                    for (k, t) in routinesTable do
                        try
                            let str = RoutineStrategy()
                            str.Key <- k

                            let scheduleName = $"WorkerOptions:Routines:{k}:Schedule"

                            let schedule =
                                Utils.tryGetConfigValue id scheduleName config

                            if schedule.IsNone then
                                failwith $"%s{scheduleName} not set so PulseeR ignores %s{k}"

                            str.Schedule <- schedule.Value

                            let concurrencyName =
                                $"WorkerOptions:Routines:{k}:Concurrency"

                            let concurrency =
                                match Utils.tryGetConfigValue uint16 concurrencyName config with
                                | None ->
                                    let def = 1us
                                    printf $"%s{concurrencyName} not set so it will be default %d{def}"
                                    def
                                | Some value -> value

                            str.Concurrency <- concurrency

                            let timeoutName = $"WorkerOptions:Routines:{k}:Timeout"

                            let timeout =
                                match Utils.tryGetConfigValue uint64 timeoutName config with
                                | None ->
                                    let def = 60000UL
                                    printf $"%s{timeoutName} not set so it will be default %d{def}"
                                    def
                                | Some value -> value

                            str.Timeout <- timeout

                            ops.Routines <-
                                ops.Routines
                                |> Array.append [| RoutineDescriptor(t, str) |]

                            s.AddTransient(t) |> ignore
                        with
                        | e -> printf "%s" e.Message

                    s.AddSingleton<WorkerOptions>(ops) |> ignore
                    s.AddHostedService<PulseeR>() |> ignore
                with
                | e -> printf $"PulseeR not initialized: %s{e.Message}")


    /// Add the PulseeR worker
    [<Extension>]
    static member AddPulseeR
        (
            builder: IHostBuilder,
            jobsAssemblies: Assembly [],
            configurationBuilder: WorkerConfiguration -> unit
        ) =
        builder.ConfigureServices
            (fun hb s ->

                let config = WorkerConfiguration()
                configurationBuilder config

                let routinesTable =
                    jobsAssemblies
                    |> Utils.toKeyedRoutinesCorrelations
                    |> Seq.filter
                        (fun x ->
                            config.Routines
                            |> Array.map (fun r -> r.Key)
                            |> Array.contains (fst x))
                    |> Map.ofSeq

                let ops = WorkerOptions()
                ops.SleepMilliseconds <- uint64 config.SleepMilliseconds

                ops.Routines <-
                    config.Routines
                    |> Seq.map (Utils.tryCreateRoutineDescriptor routinesTable)
                    |> Seq.fold
                        (fun s x ->
                            match x with
                            | Utils.Some v -> s |> List.append [ v ]
                            | Utils.None -> s)
                        List.empty
                    |> Array.ofSeq


                for r in ops.Routines do
                    try
                        s.AddTransient(r.Type) |> ignore
                    with
                    | e -> printf "%s" e.Message

                s.AddSingleton<WorkerOptions>(ops) |> ignore
                s.AddHostedService<PulseeR>() |> ignore)
