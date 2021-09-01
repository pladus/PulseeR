module PulseeR.Worker.Tests.WorkerTests

open System
open System.Collections.Generic
open System.Reflection
open System.Threading
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Hosting
open Models
open NUnit.Framework
open System
open System.Collections.Generic
open System.Linq
open System.Threading.Tasks
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Worker
open Worker.Worker

[<RoutineKey("MyLol")>]
type testRoutine =
    interface IRoutine with
        member this.ExecuteAsync(var0) = failwith "todo"
    

[<Test>]
let should_builds_correctly () =
    let host =
        Host
            .CreateDefaultBuilder([||])
            .ConfigureAppConfiguration(fun x -> x.AddJsonFile("appsettings.json", false, true) |> ignore)
            .AddPulseeR([| typeof<testRoutine>.Assembly |])
            .Build()

    let t = new Timer((fun (_) -> host.StopAsync().Wait()), null, TimeSpan.FromMinutes(20.), TimeSpan.FromMinutes(20.))
    host.Run()
    
    GC.KeepAlive(t)
    Assert.True(true)
