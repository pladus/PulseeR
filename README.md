# PulseeR
PulseeR is crontab expressions based module for running recurring tasks in ASP .NET Core host

A project is in implementation progress now.
But Schedule Package is ready to experimental expluatation now

Get started. At first you need to provide to ConfigrationBuilder (for example through appsettings.json) following options
...
```json
"WorkerOptions": {
    "SleepMilliseconds": 10000,
    "Routines": {
      "TestRoutineKey": {
        "Concurrency": 1,
        "Schedule": "* * * * *",
        "Timeout": 1000
      },
      "TestRoutineKey2": {
        "Concurrency": 1,
        "Schedule": "*/2 * * * *",
        "Timeout": 1000
      }
    }
  }
  ```
  ...
  Where:
  - WorkerOptions - root for Pulseer settings. 
  - SleepMilliseconds - sleep interval between two loop iterations where PulseeR try to search routines to be fired. It not make a lot of sense to settle less than 10000 to that. In Fact minimal crontab time step is one minute. 
  - Routines - blok for your routine customization.
  - TestRoutineKey, TestRoutineKey2 - example keys for routines. This key must be same with attribute RoutineKey which is marks your routine.
  - Concurrency - maximum count of parallel instances of routine. 
  - Schedule - Crontab expression for scheduling
  - Timeout - just routine timeout.

Second thing you must to know - routines must implement IRoutine Interface and have RoutineKey attribute with value that correspond to Routines element in configuration. For example
C#
```c#
[Models.RoutineKeyAttribute("TestRoutineKey")]
    public class TestRoutineKey : Models.IRoutine
    {
        public Task ExecuteAsync(CancellationToken ct)
        {
            // Yor awesome code here
        }
    }  
```
or F#
```f#
[<RoutineKey("TestRoutineKey")>]
type testRoutine() =
    interface IRoutine with
        member this.ExecuteAsync(ct) =
            async {
                // Yor awesome code here
            }
            |> Async.StartAsTask :> Task
```

Last thing - you need to use .AddPulseeR(assembliesToScanForRoutines) extension method on IHostBuilder:
C#
```c#
public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .AddPulseeR(new [] {Assembly.GetEntryAssembly()})
                .ConfigureServices((hostContext, services) => { services.AddHostedService<Worker>(); });
```
or F#
```f#
let host =
        Host
            .CreateDefaultBuilder([||])
            .AddPulseeR([| typeof<testRoutine>.Assembly |])
            .Build()
```
---
Known issues:
- Routine concurrency ignoring;
- Not enough logging
