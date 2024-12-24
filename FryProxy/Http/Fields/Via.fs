namespace FryProxy.Http.Fields

open System
open FryProxy.Http
open Comment

[<Struct>]
type Hop =
    { Protocol: string
      Name: string
      Comment: string }

    member hop.Encode() =
        let prefix = $"{hop.Protocol} {hop.Name}"

        if String.IsNullOrEmpty(hop.Comment) then
            prefix
        else
            $"{prefix} ({hop.Comment})"

    static member TryDecode(str: string) =
        match str.Split(Tokens.WS, 3, StringSplitOptions.TrimEntries ||| StringSplitOptions.RemoveEmptyEntries) with
        | [| proto; name; Comment comment |] -> Some { Protocol = proto; Name = name; Comment = comment }
        | [| proto; name |] -> Some { Protocol = proto; Name = name; Comment = String.Empty }
        | _ -> None

[<Struct>]
type Via =
    { Via: Hop List }

    interface IFieldModel<Via> with

        static member Name = "Via"

        member this.Encode() =
            this.Via |> List.map(_.Encode()) |> Field.joinValues

        static member TryDecode values =
            values
            |> Field.splitValues
            |> List.map Hop.TryDecode
            |> List.fold (Option.map2(fun acc hop -> hop :: acc)) (Some [])
            |> Option.map(fun hops -> { Via = List.rev hops })

module Via =

    let inline private appendHop value (hop: Hop) =
        value |> Field.splitValues |> List.append [ hop.Encode() ] |> Field.joinValues

    let append (hop: Hop) =
        Field.upsert
        <| (fun fieldOpt ->
            match fieldOpt with
            | Some f when f.Name = NameOf<Via> -> Some { f with Value = appendHop f.Value hop }
            | None -> FieldOf { Via = [ hop ] } |> Some
            | _ -> None)
