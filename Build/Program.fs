module Build

open Cake.Core.IO
open Cake.Frosting
open Pipeline
open Global


type Program() =
    interface IFrostingStartup with
        member this.Configure(services) =
            services.UseContext<Context>() |> ignore
            services.UseLifetime<Lifetime>() |> ignore

            services.UseWorkingDirectory
            <| (DirectoryPath SolutionDirectory)
            |> ignore

            ()

    static member Main(args: string []) =
        let x =
            CakeHost()
            |> CakeHostExtensions.UseStartup<Program>

        x.Run(args) |> ignore
        0

[<EntryPoint>]
let entry (args) = Program.Main(args)