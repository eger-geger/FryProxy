namespace FryProxy.Http.Fields

open System
open FryProxy.Http
open Comment

[<Struct>]
type Hop =
    { Protocol: string
      Identifier: string
      Comment: string }

    member hop.Encode() =
        let prefix = $"{hop.Protocol} {hop.Identifier}"

        if String.IsNullOrEmpty(hop.Comment) then
            prefix
        else
            $"{prefix} ({hop.Comment})"

    static member TryDecode(str: string) =
        match str.Split(Tokens.WS, 3, StringSplitOptions.TrimEntries ||| StringSplitOptions.RemoveEmptyEntries) with
        | [| proto; identifier; Comment comment |] ->
            Some { Protocol = proto; Identifier = identifier; Comment = comment }
        | [| proto; identifier |] -> Some { Protocol = proto; Identifier = identifier; Comment = String.Empty }
        | _ -> None

[<Struct>]
type Via =
    { Via: Hop List }

    interface IFieldModel<Via> with

        static member val Name = "Via"
        member this.Encode() = this.Via |> List.map(_.Encode())

        static member TryDecode values =
            values
            |> List.map Hop.TryDecode
            |> List.fold (Option.map2(fun acc hop -> hop :: acc)) (Some [])
            |> Option.map(fun hops -> { Via = List.rev hops })

module Via =

    let append (hop: Hop) =
        Field.upsert
        <| (fun fieldOpt ->
            match fieldOpt with
            | Some f when f.Name = NameOf<Via> -> Some { f with Values = f.Values @ [ hop.Encode() ] }
            | None -> FieldOf { Via = [ hop ] } |> Some
            | _ -> None)
