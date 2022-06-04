module ScheduleHolderTests

open System
open NUnit.Framework
open Schedule

type positiveTestCase(temp: string, start: DateTime, exp: DateTime []) =
    member this.Template = temp
    member this.Start = start
    member this.ExpectedResult = exp

type negativeTestCase(temp: string, error: string) =
    member this.Template = temp
    member this.ExpectedResult = error

[<Theory>]
[<Parallelizable>]

let should_find_next_fire_correctly (case: positiveTestCase) =
    let schedule = Schedule(case.Template)

    let next = schedule.GetLaunchAfter(case.Start)
    let next2 = schedule.GetLaunchAfter(next)
    let next3 = schedule.GetLaunchAfter(next2)

    Assert.AreEqual(case.ExpectedResult.[0], next)
    Assert.AreEqual(case.ExpectedResult.[1], next2)
    Assert.AreEqual(case.ExpectedResult.[2], next3)


[<SetUp>]
let Setup () = ()

[<DatapointSource>]
let data () =
    seq {
        positiveTestCase
            ("10-15 21 28 8,9 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 8, 28, 21, 10, 0)
                DateTime(2021, 8, 28, 21, 11, 0)
                DateTime(2021, 8, 28, 21, 12, 0) |])

        positiveTestCase
            ("* * * * *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 0, 1, 0)
                DateTime(2021, 1, 1, 0, 2, 0)
                DateTime(2021, 1, 1, 0, 3, 0) |])

        positiveTestCase
            ("* 10 * * *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 10, 0, 0)
                DateTime(2021, 1, 1, 10, 1, 0)
                DateTime(2021, 1, 1, 10, 2, 0) |])

        positiveTestCase
            ("* * 10 * *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 10, 0, 0, 0)
                DateTime(2021, 1, 10, 0, 1, 0)
                DateTime(2021, 1, 10, 0, 2, 0) |])

        positiveTestCase
            ("* * * 10 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 10, 1, 0, 0, 0)
                DateTime(2021, 10, 1, 0, 1, 0)
                DateTime(2021, 10, 1, 0, 2, 0) |])

        positiveTestCase
            ("5 2 * * *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 2, 5, 0)
                DateTime(2021, 1, 2, 2, 5, 0)
                DateTime(2021, 1, 3, 2, 5, 0) |])

        positiveTestCase
            ("5 2 31 * *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 31, 2, 5, 0)
                DateTime(2021, 3, 31, 2, 5, 0)
                DateTime(2021, 5, 31, 2, 5, 0) |])

        positiveTestCase
            ("10 2,3 * * *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 2, 10, 0)
                DateTime(2021, 1, 1, 3, 10, 0)
                DateTime(2021, 1, 2, 2, 10, 0) |])

        positiveTestCase
            ("1-20/15 * * * *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 0, 1, 0)
                DateTime(2021, 1, 1, 0, 16, 0)
                DateTime(2021, 1, 1, 1, 1, 0) |])

        positiveTestCase
            ("5,10/2 * * * *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 0, 5, 0)
                DateTime(2021, 1, 1, 0, 10, 0)
                DateTime(2021, 1, 1, 0, 12, 0) |])

        positiveTestCase
            ("45 0 * * *",
             DateTime(2021, 1, 1, 1, 30, 0),
             [| DateTime(2021, 1, 2, 0, 45, 0)
                DateTime(2021, 1, 3, 0, 45, 0)
                DateTime(2021, 1, 4, 0, 45, 0) |])

        positiveTestCase
            ("45 2 * 12 *",
             DateTime(2021, 1, 1, 1, 30, 0),
             [| DateTime(2021, 12, 1, 2, 45, 0)
                DateTime(2021, 12, 2, 2, 45, 0)
                DateTime(2021, 12, 3, 2, 45, 0) |])

        positiveTestCase
            ("1,2 1/1 1/1 1/1 1/1",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 1, 1, 0)
                DateTime(2021, 1, 1, 1, 2, 0)
                DateTime(2021, 1, 1, 2, 1, 0) |])

        positiveTestCase
            ("1,2 3 4 5 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 5, 4, 3, 1, 0)
                DateTime(2021, 5, 4, 3, 2, 0)
                DateTime(2022, 5, 4, 3, 1, 0) |])

        positiveTestCase
            ("59 3 4 5 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 5, 4, 3, 59, 0)
                DateTime(2022, 5, 4, 3, 59, 0)
                DateTime(2023, 5, 4, 3, 59, 0) |])

        positiveTestCase
            ("50/9 * * * *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 0, 50, 0)
                DateTime(2021, 1, 1, 0, 59, 0)
                DateTime(2021, 1, 1, 1, 50, 0) |])

        positiveTestCase
            ("55 2/20 * * *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 2, 55, 0)
                DateTime(2021, 1, 1, 22, 55, 0)
                DateTime(2021, 1, 2, 2, 55, 0) |])

        positiveTestCase
            ("0 0 * * 7",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 3, 0, 0, 0)
                DateTime(2021, 1, 10, 0, 0, 0)
                DateTime(2021, 1, 17, 0, 0, 0) |])

        positiveTestCase
            ("0 0 * * 6,7",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 2, 0, 0, 0)
                DateTime(2021, 1, 3, 0, 0, 0)
                DateTime(2021, 1, 9, 0, 0, 0) |])

        positiveTestCase
            ("0 1,2,3 * * 7",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 3, 1, 0, 0)
                DateTime(2021, 1, 3, 2, 0, 0)
                DateTime(2021, 1, 3, 3, 0, 0) |])

        positiveTestCase
            ("0 0 1-10 * 7",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 3, 0, 0, 0)
                DateTime(2021, 1, 10, 0, 0, 0)
                DateTime(2021, 2, 7, 0, 0, 0) |])

        positiveTestCase
            ("0 0 31 * 5,6,7",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 31, 0, 0, 0)
                DateTime(2021, 7, 31, 0, 0, 0)
                DateTime(2021, 10, 31, 0, 0, 0) |])

        positiveTestCase
            ("0 0 31 * 7",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 31, 0, 0, 0)
                DateTime(2021, 10, 31, 0, 0, 0)
                DateTime(2022, 7, 31, 0, 0, 0) |])

        positiveTestCase
            ("5 0 1 1 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 0, 5, 0)
                DateTime(2022, 1, 1, 0, 5, 0)
                DateTime(2023, 1, 1, 0, 5, 0) |])

        positiveTestCase
            ("5 0 1/2 1 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 0, 5, 0)
                DateTime(2021, 1, 3, 0, 5, 0)
                DateTime(2021, 1, 5, 0, 5, 0) |])

        positiveTestCase
            ("5 0 1 1,11,12 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 0, 5, 0)
                DateTime(2021, 11, 1, 0, 5, 0)
                DateTime(2021, 12, 1, 0, 5, 0) |])

        positiveTestCase
            ("5 0 1 1-2,12 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 0, 5, 0)
                DateTime(2021, 2, 1, 0, 5, 0)
                DateTime(2021, 12, 1, 0, 5, 0) |])

        positiveTestCase
            ("5 0 1 1-10/2 *",
             DateTime(2021, 8, 10, 0, 0, 0),
             [| DateTime(2021, 9, 1, 0, 5, 0)
                DateTime(2022, 1, 1, 0, 5, 0)
                DateTime(2022, 3, 1, 0, 5, 0) |])

        positiveTestCase
            ("5 0 1 1-2/2 *",
             DateTime(2021, 8, 10, 0, 0, 0),
             [| DateTime(2022, 1, 1, 0, 5, 0)
                DateTime(2023, 1, 1, 0, 5, 0)
                DateTime(2024, 1, 1, 0, 5, 0) |])

        positiveTestCase
            ("5 0-10,2 1 1 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 0, 5, 0)
                DateTime(2021, 1, 1, 1, 5, 0)
                DateTime(2021, 1, 1, 2, 5, 0) |])

        positiveTestCase
            ("5 0-1,8 1 1 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 0, 5, 0)
                DateTime(2021, 1, 1, 1, 5, 0)
                DateTime(2021, 1, 1, 8, 5, 0) |])

        positiveTestCase
            ("5 0 1 1-10/7 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 1, 1, 0, 5, 0)
                DateTime(2021, 8, 1, 0, 5, 0)
                DateTime(2022, 1, 1, 0, 5, 0) |])

        positiveTestCase
            ("0 0 29 2 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2024, 2, 29, 0, 0, 0)
                DateTime(2028, 2, 29, 0, 0, 0)
                DateTime(2032, 2, 29, 0, 0, 0) |])

        positiveTestCase
            ("0 0 29,30 2 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2024, 2, 29, 0, 0, 0)
                DateTime(2028, 2, 29, 0, 0, 0)
                DateTime(2032, 2, 29, 0, 0, 0) |])

        positiveTestCase
            ("0 0 29-31 2 *",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2024, 2, 29, 0, 0, 0)
                DateTime(2028, 2, 29, 0, 0, 0)
                DateTime(2032, 2, 29, 0, 0, 0) |])

        positiveTestCase
            ("0 0 1 * MON",
             DateTime(2021, 1, 1, 0, 0, 0),
             [| DateTime(2021, 2, 1, 0, 0, 0)
                DateTime(2021, 3, 1, 0, 0, 0)
                DateTime(2021, 11, 1, 0, 0, 0) |])

    }
