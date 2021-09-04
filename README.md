# PulseeR
PulseeR is crontab expressions based module for running recurring tasks in ASP .NET Core host.

It works with C# and F# as well.

!! A project is in implementation progress now and not recommended for production. !!

Get started
---

At first you need to provide to ConfigrationBuilder (for example through appsettings.json) following options

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
  - WorkerOptions - root for PulseeR settings. 
  - SleepMilliseconds - sleep interval between two loop iterations where PulseeR try to search routines to be fired. It not make a lot of sense to settle less than 10000 to that. In Fact minimal crontab time step is one minute. 
  - Routines - block for your routine customization.
  - TestRoutineKey, TestRoutineKey2 - example keys for routines. This key must be same with attribute RoutineKey which is marks your routine.
  - Concurrency - maximum count of parallel instances of routine. 
  - Schedule - Crontab expression for scheduling
  - Timeout - timeout for routine execution. 

Second thing you must to know - routines must implement IRoutine Interface and have RoutineKey attribute with value that correspond to Routines element in configuration. For example:

C#
```c#
[Models.RoutineKeyAttribute("TestRoutineKey")]
    public class TestRoutine : IRoutine
    {
        public Task ExecuteAsync(CancellationToken ct)
        {
            // Your awesome code here
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
                // Your awesome code here
            }
            |> Async.StartAsTask :> Task
```

Last thing - you need to use .AddPulseeR(assembliesToScanForRoutines) extension method on IHostBuilder:

C#
```c#
var host =
            Host.CreateDefaultBuilder(args)
                .AddPulseeR(new [] {Assembly.GetEntryAssembly()})
                .Build()
```
or F#
```f#
let host =
        Host
            .CreateDefaultBuilder([||])
            .AddPulseeR([| Assembly.GetEntryAssembly() |])
            .Build()
```
---
Known issues:
- Cron shedule validation is pretty poor
- Worker can't handle with invalid date like 31 february
- Configuration way is only provide from appsettings.json

