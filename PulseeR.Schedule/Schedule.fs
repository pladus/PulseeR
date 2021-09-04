module Schedule

open System

type Schedule(template: string) =

    let scheduleTable =
        Parser.GetLaunchTable(template) |> Seq.toArray

    let elInc (allowed: seq<int>) current =
        let found = allowed |> Seq.tryFind ((<) current)

        match found with
        | Some x -> (x, false)
        | _ -> (allowed |> Seq.min, true)

    let getCorrectDayOfWeek weekDay x y =
        let result = (weekDay + x - y) % 7
        if result = 0 then 7 else result

    let rec seekDateWeekly (allowed: seq<int>)
                           allowedWeekDays
                           currentDay
                           currentMonth
                           currentYear
                           currentWeekDay
                           needIncDate
                           =
        let maxDays =
            DateTime.DaysInMonth(currentYear, currentMonth)

        let legalDaysAllowed = allowed |> Seq.filter ((>=) maxDays)

        let predicate =
            if needIncDate then fun x -> x > currentDay else fun x -> x >= currentDay

        let found =
            legalDaysAllowed
            |> Seq.filter predicate
            |> Seq.map (fun x -> (x, getCorrectDayOfWeek currentWeekDay x currentDay))
            |> Seq.tryFind (fun x ->
                predicate (fst x)
                && allowedWeekDays |> Seq.exists (fun y -> y = snd x))

        match found with
        | Some x -> (fst x, currentMonth, currentYear)
        | _ ->
            let addDays = (maxDays - currentDay) + 1
            let newWeekDay = (currentWeekDay + addDays) % 7

            let (newYear, newMonth) =
                if currentMonth = 12 then (currentYear + 1, 1) else (currentYear, currentMonth + 1)

            seekDateWeekly allowed allowedWeekDays 1 newMonth newYear newWeekDay false

    let rec seekDateMonthly (allowed: seq<int>) allowedMonths currentDay currentMonth currentYear needIncDate =
        let maxDays =
            DateTime.DaysInMonth(currentYear, currentMonth)

        let predicate =
            if needIncDate then fun x -> x > currentDay else fun x -> x >= currentDay

        let legalDaysAllowed = allowed |> Seq.filter ((>=) maxDays)

        let found =
            legalDaysAllowed |> Seq.tryFind predicate

        match found with
        | Some x when allowedMonths |> Seq.exists ((=) currentMonth) -> (x, currentMonth, currentYear)
        | _ ->
            let (newYear, newMonth) =
                if currentMonth = 12 then (currentYear + 1, 1) else (currentYear, currentMonth + 1)

            seekDateMonthly allowed allowedMonths 1 newMonth newYear false

    let dayOfWeekSettled =
        scheduleTable.[4] |> Seq.toList
        <> DateComponentItems.timeComponents.[4]

    member this.GetNextLaunch() = this.GetLaunchAfter(DateTime.Now)

    member this.GetLaunchAfter(lastLaunchDatetime: DateTime) =
        let lastMin = lastLaunchDatetime.Minute
        let lastHour = lastLaunchDatetime.Hour
        let lastDay = lastLaunchDatetime.Day
        let lastMonth = lastLaunchDatetime.Month
        let lastYear = lastLaunchDatetime.Year

        let lastDayOfWeek =
            if lastLaunchDatetime.DayOfWeek = DayOfWeek.Sunday
            then 7
            else int lastLaunchDatetime.DayOfWeek

        let allowedH =
            scheduleTable.[1] |> Seq.exists ((=) lastHour)

        let allowedD =
            scheduleTable.[2] |> Seq.exists ((=) lastDay)

        let allowedMon =
            scheduleTable.[3] |> Seq.exists ((=) lastMonth)

        let allowedDayOfWeek =
            not dayOfWeekSettled
            || scheduleTable.[4]
               |> Seq.exists ((=) lastDayOfWeek)

        let (nextM, needIncH) =
            if allowedD
               && allowedH
               && allowedDayOfWeek
               && allowedMon then
                elInc scheduleTable.[0] lastMin
            else
                (scheduleTable.[0] |> Seq.min, false)

        let (nextH, needIncD) =
            if needIncH || not allowedH then elInc scheduleTable.[1] lastHour else (lastHour, false)

        if dayOfWeekSettled then
            let (nextD, nextMon, nextYear) =
                if needIncD || not allowedDayOfWeek || not allowedD
                then seekDateWeekly
                         scheduleTable.[2]
                         scheduleTable.[4]
                         lastDay
                         lastMonth
                         lastYear
                         lastDayOfWeek
                         needIncD
                else (lastDay, lastMonth, lastYear)

            DateTime(nextYear, nextMon, nextD, nextH, nextM, 0)
        else
            let (nextD, nextMon, nextYear) =
                if needIncD || not allowedD || not allowedMon
                then seekDateMonthly scheduleTable.[2] scheduleTable.[3] lastDay lastMonth lastYear needIncD
                else (lastDay, lastMonth, lastYear)

            DateTime(nextYear, nextMon, nextD, nextH, nextM, 0)
