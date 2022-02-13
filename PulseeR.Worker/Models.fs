module Models

open System
open System.Threading
open System.Threading.Tasks

[<AttributeUsage(AttributeTargets.Class)>]
type public RoutineKeyAttribute(key: string) =
    inherit Attribute()
    member this.Key = key

/// Routine execution strategy
type public RoutineStrategy() =
    /// Maximum instances for concurrent execution.
    [<DefaultValue>]
    val mutable Concurrency: uint16
    /// Routine class key.
    [<DefaultValue>]
    val mutable Key: string
    /// Crontab expression schedule.
    [<DefaultValue>]
    val mutable Schedule: string
    /// Routine execution timeout in seconds. Infinity if 0 is provided value. 
    [<DefaultValue>]
    val mutable Timeout: uint64

type public IRoutine =
    abstract ExecuteAsync: CancellationToken -> Task

type internal RoutineDescriptor(tp, ops) =
    member this.Strategy: RoutineStrategy = ops
    member this.Type: Type = tp

type internal WorkerOptions() =
    [<DefaultValue>]
    val mutable Routines: RoutineDescriptor []
    [<DefaultValue>]
    val mutable SleepMilliseconds: uint64

/// Pulseer worker configuration
[<Class>]
type public WorkerConfiguration() =
    /// Routines configuration.
    [<DefaultValue>]
    val mutable public Routines: RoutineStrategy []
    /// Interval between routines scan.
    [<DefaultValue>]
    val mutable public SleepMilliseconds: uint64