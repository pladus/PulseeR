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
open HostExtensions

type mock() =
    [<DefaultValue>]
    val mutable FirstCalls: int

    [<DefaultValue>]
    val mutable SecondCalls: int

    [<DefaultValue>]
    val mutable ThirdCalls: int

    [<DefaultValue>]

    val mutable ForthCalls: int

    [<DefaultValue>]
    val mutable FifthCalls: int

    [<DefaultValue>]
    val mutable SixthCalls: int


[<RoutineKey("TestRoutineKey")>]
type testRoutine(mock: mock, logger: ILogger<testRoutine>) =
    interface IRoutine with
        member this.ExecuteAsync(ct) =
            async {
                do! Async.Sleep 5000
                ct.ThrowIfCancellationRequested()
                mock.FirstCalls <- mock.FirstCalls + 1
                logger.LogInformation("testRoutine done")
            }
            |> Async.StartAsTask :> Task

[<RoutineKey("TestRoutineKey2")>]
type testRoutine2(mock: mock, logger: ILogger<testRoutine2>) =
    interface IRoutine with
        member this.ExecuteAsync(ct) =
            async {
                do! Async.Sleep 5000
                mock.SecondCalls <- mock.SecondCalls + 1
                ct.ThrowIfCancellationRequested()
            }
            |> Async.StartAsTask :> Task

type testRoutine3(mock: mock, logger: ILogger<testRoutine3>) =
    interface IRoutine with
        member this.ExecuteAsync(ct) =
            async {
                do! Async.Sleep 5000
                mock.ThirdCalls <- mock.ThirdCalls + 1

                logger.LogInformation("testRoutine3 done")
            }
            |> Async.StartAsTask :> Task

[<RoutineKey("TestRoutineKeyLongSingle")>]
type testRoutine4(mock: mock, logger: ILogger<testRoutine4>) =
    interface IRoutine with
        member this.ExecuteAsync(ct) =
            async {
                logger.LogInformation("TestRoutineKeyLongSingle fired")

                mock.ForthCalls <- mock.ForthCalls + 1
                do! Async.Sleep 600_000
            }
            |> Async.StartAsTask :> Task

[<RoutineKey("TestRoutineKeyLongConcurrent")>]
type testRoutine5(mock: mock, logger: ILogger<testRoutine5>) =
    interface IRoutine with
        member this.ExecuteAsync(ct) =
            async {
                logger.LogInformation("TestRoutineKeyLongConcurrent fired")

                mock.FifthCalls <- mock.FifthCalls + 1
                do! Async.Sleep 600_000 
            }
            |> Async.StartAsTask :> Task

[<RoutineKey("TestRoutineKeyLongConcurrentMore")>]
type testRoutine6(mock: mock, logger: ILogger<testRoutine6>) =
    interface IRoutine with
        member this.ExecuteAsync(ct) =
            async {
                logger.LogInformation("TestRoutineKeyLongConcurrentMore fired")

                mock.SixthCalls <- mock.SixthCalls + 1
                do! Async.Sleep 600_000
            }
            |> Async.StartAsTask :> Task


[<Test>]
let ``Should run routines correcrly from config`` () =
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

    // test scheduling
    Assert.Greater(mock.FirstCalls, 0)
    Assert.True(mock.FirstCalls > mock.SecondCalls)

    // test concurrency
    Assert.AreEqual(3, mock.SixthCalls)
    Assert.AreEqual(2, mock.FifthCalls)
    Assert.AreEqual(1, mock.ForthCalls)

    // test routine observing
    Assert.AreEqual(0, mock.ThirdCalls)

[<Test>]
let ``Should run routines correcrly from ops object`` () =
    let mock = mock ()

    let config = new WorkerConfiguration()
    config.SleepMilliseconds <- uint64 300

    let testRoutine = RoutineStrategy()
    testRoutine.Key <- "TestRoutineKey"
    testRoutine.Concurrency <- uint16 1
    testRoutine.Schedule <- "* * * * *"
    testRoutine.Timeout <- uint64 8000

    let testRoutine2 = RoutineStrategy()
    testRoutine2.Key <- "TestRoutineKey2"
    testRoutine2.Concurrency <-uint16  1
    testRoutine2.Schedule <- "*/2 * * * *"
    testRoutine2.Timeout <- uint64 1000

    let testRoutineLongSingle = RoutineStrategy()
    testRoutineLongSingle.Key <- "TestRoutineKeyLongSingle"
    testRoutineLongSingle.Concurrency <- uint16 1
    testRoutineLongSingle.Schedule <- "* * * * *"
    testRoutineLongSingle.Timeout <- uint64 1000

    let testRoutineLongConcurrent = RoutineStrategy()
    testRoutineLongConcurrent.Key <- "TestRoutineKeyLongConcurrent"
    testRoutineLongConcurrent.Concurrency <- uint16 2
    testRoutineLongConcurrent.Schedule <- "* * * * *"
    testRoutineLongConcurrent.Timeout <- uint64 1000

    let testRoutineLongConcurrentMore = RoutineStrategy()
    testRoutineLongConcurrentMore.Key <- "TestRoutineKeyLongConcurrentMore"
    testRoutineLongConcurrentMore.Concurrency <- uint16 3
    testRoutineLongConcurrentMore.Schedule <- "* * * * *"
    testRoutineLongConcurrentMore.Timeout <- uint64 1000

    let testRoutineKeyTimeout = RoutineStrategy()
    testRoutineKeyTimeout.Key <- "TestRoutineKeyTimeout"
    testRoutineKeyTimeout.Concurrency <- uint16 1
    testRoutineKeyTimeout.Schedule <- "* * * * *"
    testRoutineKeyTimeout.Timeout <- uint64 1000

    config.Routines <- 
        [|
        testRoutine;
        testRoutine2; 
        testRoutineKeyTimeout; 
        testRoutineLongConcurrent;
        testRoutineLongConcurrentMore;
        testRoutineLongSingle
        |]

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
            .AddPulseeR([| typeof<testRoutine>.Assembly |], config)
            .Build()

    let t =
        new Timer((fun _ -> host.StopAsync().Wait()), null, TimeSpan.FromSeconds(180.), TimeSpan.FromSeconds(180.))

    host.Run()

    GC.KeepAlive(t)

    // test scheduling
    Assert.Greater(mock.FirstCalls, 0)
    Assert.True(mock.FirstCalls > mock.SecondCalls)

    // test concurrency
    Assert.AreEqual(3, mock.SixthCalls)
    Assert.AreEqual(2, mock.FifthCalls)
    Assert.AreEqual(1, mock.ForthCalls)

    // test routine observing
    Assert.AreEqual(0, mock.ThirdCalls)
