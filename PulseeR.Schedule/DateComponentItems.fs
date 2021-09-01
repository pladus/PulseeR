module DateComponentItems

let internal timeComponents =
    [| [ 0 .. 59 ] // minutes
       [ 0 .. 23 ] // hours
       [ 1 .. 31 ] // days
       [ 1 .. 12 ] // months
       [ 1 .. 7 ] |] // days of week
