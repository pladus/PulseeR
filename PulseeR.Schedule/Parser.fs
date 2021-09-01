namespace Parser

open System
open System.Runtime.CompilerServices
open System.Text.RegularExpressions
open DateComponentItems


module Parser =

    [<assembly: InternalsVisibleTo("PulseeR.Schedule.Tests")>]
    do ()

    let private templateIncorrectError = Exception("Incorrect schedule template")

    let private validateLength (a: string []) =
        match a.Length with
        | 5 -> a
        | _ -> raise templateIncorrectError

    let private intersect a b =
        set a |> Set.intersect (set b) |> Set.toSeq

    let normalize (template: string) =
        template
            .Replace("MON", "1")
            .Replace("TUE", "2")
            .Replace("WED", "3")
            .Replace("THU", "4")
            .Replace("FRI", "5")
            .Replace("SAT", "6")
            .Replace("SUN", "7")

    let private parseRange (template: string) items =
        let range = template.Split('-') |> Seq.toArray

        let min = int range.[0]
        let max = int range.[1]

        items
        |> Seq.filter (fun x -> min <= x && x <= max)

    let private parseWild template items = items

    let private parseEnumeration (template: string) items =
        template.Split(',')
        |> Seq.map int
        |> intersect items

    let parseRangeWithEnumeration (template: string) items =
        let separated = template.Split(',') |> Seq.toArray
        let rangeItems = parseRange separated.[0] items

        let enumItems =
            separated.[1..] |> Seq.map int |> intersect items

        rangeItems
        |> Seq.append enumItems
        |> Seq.distinct
        |> Seq.sort


    let private parseConst template items =
        items |> intersect (Seq.singleton (int template))

    let private parseWildRepeat (template: string) items =
        let step = int (template.Split('/').[1])
        items |> Seq.filter (fun x -> x % step = 0)

    let private parseInRangeRepeat (template: string) items =
        let components = template.Split('/')
        let step = int components.[1]
        let range = parseRange components.[0] items
        let start = range |> Seq.find (fun x -> true)
        let final = range |> Seq.last
        seq { start .. step .. final }

    let private parseRepeatAfterEnum (template: string) items =
        let components = template.Split('/')
        let itemSet = set items

        let leftSubSet =
            set (components.[0].Split(',')) |> Set.map int

        let boundaryFirst = Seq.last leftSubSet
        let boundaryLast = Seq.last items

        let step = int components.[1]

        let rightSubSet =
            seq { boundaryFirst + step .. step .. boundaryLast }

        itemSet
        |> Set.intersect leftSubSet
        |> Seq.append rightSubSet
        |> Seq.sort

    let private extractNext (items, template) =
        match normalize template with
        | "*" -> parseWild template items
        | s when Regex.IsMatch(s, "^[0-9]{1,2}$") -> parseConst s items
        | s when Regex.IsMatch(s, "^\*/[0-9]{1,2}$") -> parseWildRepeat s items
        | s when Regex.IsMatch(s, "^[0-9]{1,2}(\,[0-9]{1,2}){0,}$") -> parseEnumeration s items
        | s when Regex.IsMatch(s, "^[0-9]{1,2}-[0-9]{1,2}$") -> parseRange s items
        | s when Regex.IsMatch(s, "^[0-9]{1,2}-[0-9]{1,2}/[0-9]{1,2}$") -> parseInRangeRepeat s items
        | s when Regex.IsMatch(s, "^[0-9]{1,2}(\,[0-9]{1,2}){0,}/[0-9]{1,2}$") -> parseRepeatAfterEnum s items
        | s when Regex.IsMatch(s, "^[0-9]{1,2}-[0-9]{1,2}(\,[0-9]{1,2}){1,}$") -> parseRangeWithEnumeration s items

        | _ -> raise templateIncorrectError

    /// Parse the schedule and calculate the time components allowed for firing
    let internal GetLaunchTable (template: string) =
        template.Split(' ')
        |> validateLength
        |> Seq.zip timeComponents
        |> Seq.map extractNext
