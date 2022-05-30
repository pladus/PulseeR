module Pipeline

open Cake.Common.Tools.DotNetCore.Pack
open Cake.Core.IO
open Cake.Frosting
open Cake.Common.Diagnostics
open Cake.Common.Tools.DotNetCore

type Context(context) =
    inherit FrostingContext(context)

type Lifetime() =
    inherit FrostingLifetime()

    override this.Setup(context) =
        context.Information("PulseeR build started")
        ()

    override this.Teardown(context, info) =
        context.Information("PulseeR build finished")
        ()


type Build() =
    inherit FrostingTask<Context>()

    override this.Run context =
        context.DotNetCoreBuild ".."
        ()

[<IsDependentOn(typeof<Build>)>]

type Test() =
    inherit FrostingTask<Context>()

    override this.Run context =
       // context.DotNetCoreTest ".."
        ()

[<IsDependentOn(typeof<Test>)>]
type Publish() =
    inherit FrostingTask<Context>()

    override this.Run context =
        let ops = DotNetCorePackSettings()

        ops.OutputDirectory <- context.Environment.WorkingDirectory.Combine <| DirectoryPath "Package"
        ops.IncludeSource <- true
        ops.IncludeSymbols <- true
        ops.Configuration <- "Release"
        
        context.DotNetCorePack("..", ops)
        ()

[<IsDependentOn(typeof<Publish>)>]
type NugetPush() =
    inherit FrostingTask<Context>()

    override this.Run context = ()

[<IsDependentOn(typeof<NugetPush>)>]
type Default() =
    inherit FrostingTask<Context>()
    override this.Run _ = ()
