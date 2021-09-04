# PulseeR

PulseeR is crontab expressions based module for running recurring tasks in ASP .NET Core host.

It works with C# and F# as well.

**A project is in implementation progress now and not recommended for production!**

---
# Get started


**More examples you can find in PulseeR.Worker.Tests project.**

## At first 

You need to provide to ConfigurationBuilder (for example through appsettings.json) following options

...
```json
"WorkerOptions": {
    "SleepMilliseconds": 10000,
    "Routines": {
      "YourRoutineKey": {
        "Concurrency": 1,
        "Schedule": "* * * * *",
        "Timeout": 1000
      },
      "YourAnotherRoutineKey": {
        "Concurrency": 3,
        "Schedule": "*/2 * * * *",
        "Timeout": 2000
      }
    }
  }
  ```
  ...
  
  Where:
  - WorkerOptions - root for PulseeR settings. 
  - SleepMilliseconds - sleep interval between two loop iterations where PulseeR try to search routines to be fired. It not make a lot of sense to set it up less than 10000. In Fact minimal crontab time step is one minute. 
  - Routines - block for customization of each of your routines.
  - TestRoutineKey, TestRoutineKey2 - example keys for routines. This key must be same with attribute RoutineKey which is marks your routine.
  - Concurrency - maximum count of parallel instances of routine. 
  - Schedule - Crontab expression for scheduling
  - Timeout - timeout for routine execution. 

## Second 

You need to import PulseeR.Worker from Nuget repository: https://www.nuget.org/packages/PulseeR.Worker

## Third thing 

Routines must implement IRoutine Interface and have RoutineKey attribute with value that correspond to Routines element in configuration. For example:

C#
```c#
[RoutineKey("YourRoutineKey")]
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
[<RoutineKey("YourRoutineKey")>]
type testRoutine() =
    interface IRoutine with
        member this.ExecuteAsync(ct) =
            async {
                // Your awesome code here
            }
            |> Async.StartAsTask :> Task
```

## Last thing 

You need to use .AddPulseeR(assembliesToScanForRoutines) extension method on IHostBuilder:

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
- Worker can't handle with invalid date like february 31
- Configuration way is only provide from appsettings.json

