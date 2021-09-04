module Models

open System
open System.Threading
open System.Threading.Tasks

[<AttributeUsage(AttributeTargets.Class)>]
type RoutineKeyAttribute(key: string) =
    inherit Attribute()
    member this.Key = key


type RoutineStrategy() =
    [<DefaultValue>]
    val mutable Concurrency: int

    [<DefaultValue>]
    val mutable Key: string

    [<DefaultValue>]
    val mutable Schedule: string

    [<DefaultValue>]
    val mutable Timeout: int





type IRoutine =
    abstract ExecuteAsync: CancellationToken -> Task

type RoutineDescriptor(tp, ops) =
    member this.Strategy: RoutineStrategy = ops
    member this.Type: Type = tp

type WorkerStrategy() =
    [<DefaultValue>]
    val mutable Routines: RoutineDescriptor []

    [<DefaultValue>]
    val mutable SleepMilliseconds: int
