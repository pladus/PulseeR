module Models

open System
open System.Reflection
open System.Threading
open System.Threading.Tasks

type RoutineKeyAttribute(key: string) =
    inherit Attribute()
    member this.Key = key

type RoutinesAssemblyInfo(assembly: Assembly []) =
    member this.Assemblies = assembly


type RoutineOptions() =
    member this.Concurrency = 0
    member this.Key: string = null
    member this.Schedule: string = null
    member this.Timeout: int = 0


type WorkerOptions() =
    member this.JobOptions: RoutineOptions [] = Array.empty
    member this.SleepMilliseconds: int = 0


type IRoutine =
    abstract ExecuteAsync: CancellationToken -> Async<unit>

type RoutineDescriptor(tp, ops) =
    member this.RoutineOptions: RoutineOptions = ops
    member this.RoutineType: Type = tp
