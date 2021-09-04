module ParserTests

open NUnit.Framework

type positiveTestCase(temp: string, exp: seq<seq<int>>) =
    member this.Template = temp
    member this.ExpectedResult = exp

type negativeTestCase(temp: string, error: string) =
    member this.Template = temp
    member this.ExpectedResult = error


[<Theory>]
let should_parse_correctly (case: positiveTestCase) =
    let actual = Parser.GetLaunchTable(case.Template)
    Assert.AreEqual(case.ExpectedResult, actual)

[<SetUp>]
let Setup () = ()

[<DatapointSource>]
let data () =
    seq {
        positiveTestCase
            ("* * * * *",
             seq {
                 seq { 0 .. 59 }
                 seq { 0 .. 23 }
                 seq { 1 .. 31 }
                 seq { 1 .. 12 }
                 seq { 1 .. 7 }

             })

        positiveTestCase
            ("0 0 1 1 1",
             seq {
                 seq { 0 }
                 seq { 0 }
                 seq { 1 }
                 seq { 1 }
                 seq { 1 }

             })

        positiveTestCase
            ("* 1 1-10 */1 1-10/3",
             seq {
                 seq { 0 .. 59 }
                 seq { 1 }
                 seq { 1 .. 10 }
                 seq { 1 .. 12 }
                 seq { 1 .. 3 .. 7 }

             })

        positiveTestCase
            ("0 1 3/4 * 1",
             seq {
                 seq { 0 }
                 seq { 1 }
                 seq { 3 .. 4 .. 31 }
                 seq { 1 .. 12 }
                 seq { 1 }

             })

        positiveTestCase
            ("* * * 8 8",
             seq {
                 seq { 0 .. 59 }
                 seq { 0 .. 23 }
                 seq { 1 .. 31 }
                 seq { 8 }
                 Seq.empty
             })

        positiveTestCase
            ("1,5,9 3 * * *",
             seq {
                 seq {
                     1
                     5
                     9
                 }

                 seq { 3 }
                 seq { 1 .. 31 }
                 seq { 1 .. 12 }
                 seq { 1 .. 7 }
             })

        positiveTestCase
            ("5/10 0 * * *",
             seq {
                 seq { 5 .. 10 .. 55 }
                 seq { 0 }
                 seq { 1 .. 31 }
                 seq { 1 .. 12 }
                 seq { 1 .. 7 }
             })

        positiveTestCase
            ("10/10 0 * * *",
             seq {
                 seq { 10 .. 10 .. 50 }
                 seq { 0 }
                 seq { 1 .. 31 }
                 seq { 1 .. 12 }
                 seq { 1 .. 7 }
             })

        positiveTestCase
            ("0,10,20/20 0 * * *",
             seq {
                 seq {
                     0
                     10
                     20
                     40
                 }

                 seq { 0 }
                 seq { 1 .. 31 }
                 seq { 1 .. 12 }
                 seq { 1 .. 7 }
             })

        positiveTestCase
            ("0 0 * * FRI",
             seq {
                 seq { 0 }

                 seq { 0 }
                 seq { 1 .. 31 }
                 seq { 1 .. 12 }
                 seq { 5 }
             })

        positiveTestCase
            ("0 0 * * MON-SAT",
             seq {
                 seq { 0 }

                 seq { 0 }
                 seq { 1 .. 31 }
                 seq { 1 .. 12 }
                 seq { 1 .. 6 }
             })

        positiveTestCase
            ("0 0 * * TUE,WED,THU,SUN",
             seq {
                 seq { 0 }

                 seq { 0 }
                 seq { 1 .. 31 }
                 seq { 1 .. 12 }

                 seq {
                     2
                     3
                     4
                     7
                 }
             })

        positiveTestCase
            ("0 0 * * FRI",
             seq {
                 seq { 0 }

                 seq { 0 }
                 seq { 1 .. 31 }
                 seq { 1 .. 12 }
                 seq { 5 }
             })

    }
