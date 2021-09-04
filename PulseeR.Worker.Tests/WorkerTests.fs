module PulseeR.Worker.Tests.WorkerTests

open System
open System.Threading
open System.Threading.Tasks
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Models
open NUnit.Framework
open Work

type mock() =
    [<DefaultValue>]
    val mutable FirstCalls: int

    [<DefaultValue>]
    val mutable SecondCalls: int

    [<DefaultValue>]
    val mutable ThirdCalls: int

[<RoutineKey("TestRoutineKey")>]
type testRoutine(mock: mock, logger: ILogger<testRoutine>) =
    interface IRoutine with
        member this.ExecuteAsync(var0) =
            async {
                do! Async.Sleep 5000
                mock.FirstCalls <- mock.FirstCalls + 1
                logger.LogInformation("testRoutine done")
            }
            |> Async.StartAsTask :> Task

[<RoutineKey("TestRoutineKey2")>]
type testRoutine2(mock: mock, logger: ILogger<testRoutine2>) =
    interface IRoutine with
        member this.ExecuteAsync(var0) =
            async {
                do! Async.Sleep 5000
                mock.SecondCalls <- mock.SecondCalls + 1

                raise (Exception("testRoutine2 done"))
            }
            |> Async.StartAsTask :> Task

type testRoutine3(mock: mock, logger: ILogger<testRoutine2>) =
    interface IRoutine with
        member this.ExecuteAsync(var0) =
            async {
                do! Async.Sleep 5000
                mock.ThirdCalls <- mock.ThirdCalls + 1

                logger.LogInformation("testRoutine3 done")
            }
            |> Async.StartAsTask :> Task




[<Test>]
let should_builds_correctly () =
    let mock = mock ()

    let host =
        Host
            .CreateDefaultBuilder([||])
            .ConfigureAppConfiguration(fun x ->
                x.AddJsonFile("appsettings.json", false, true)
                |> ignore)
            .ConfigureLogging(fun x -> x.AddConsole() |> ignore)
            .ConfigureServices(fun x y ->
                y.Add(ServiceDescriptor(typeof<mock>, mock))
                |> ignore)
            .AddPulseeR([| typeof<testRoutine>.Assembly |])
            .Build()

    let t =
        new Timer((fun _ -> host.StopAsync().Wait()), null, TimeSpan.FromSeconds(180.), TimeSpan.FromSeconds(180.))

    host.Run()

    GC.KeepAlive(t)

    Assert.Greater(mock.FirstCalls, 0)
    Assert.Greater(mock.FirstCalls, 0)

    Assert.True(mock.FirstCalls > mock.SecondCalls)
    Assert.AreEqual(0, mock.ThirdCalls)
