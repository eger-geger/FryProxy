namespace FryProxy.Lib

/// A closed range [Start, End] over a comparable type represented as a struct tuple.
type range<'a when 'a: comparison> = (struct ('a * 'a))

/// A sorted, disjoint list of closed ranges over a totally ordered type.
///
/// <para>
/// The list is ordered by range start and no two adjacent ranges can be merged
/// (there is at least one gap between each pair). Supports efficient membership testing
/// and insertion with automatic merging of adjacent or overlapping ranges.
/// </para>
///
/// <para>
/// Typical use case: tracking which discrete values have been "seen" on a monotonic
/// sequence, where consecutive values can be represented as a single range rather
/// than individual entries. For example, HTTP/2 stream IDs (1, 3, 5, 7) can be stored
/// as one range [1, 7] instead of four separate entries.
/// </para>
module RangeList =

    /// Returns an empty range list.
    let empty: range<'T> list = []

    /// Returns true if the value falls within any range in the list.
    ///
    /// <para>Uses early termination: since the list is sorted, stops scanning once
    /// the current range's Start exceeds the target value.</para>
    [<TailCall>]
    let rec contains (ranges: range<'T> list) (value: 'T) : bool =
        match ranges with
        | [] -> false
        | struct (s, e) :: _ when value >= s && value <= e -> true
        | struct (s, _) :: _ when value < s -> false // sorted: can stop early
        | _ :: tail -> contains tail value

    let inline private isRightAdjacentRange isValueAdjacent (struct (_, lr)) (struct (rl, _)) =
        lr = rl || isValueAdjacent lr rl

    let inline private merge struct (ll, lr) struct (_, rr) = struct (ll, max lr rr)

    [<TailCall>]
    let rec private mergeList isAdjacent prev =
        function
        | [] -> [ prev ]
        | head :: tail when isRightAdjacentRange isAdjacent prev head -> mergeList isAdjacent (merge prev head) tail
        | head :: tail when isRightAdjacentRange isAdjacent head prev -> mergeList isAdjacent (merge head prev) tail
        | head :: tail -> prev :: head :: tail

    [<TailCall>]
    let rec private addValue isAdjacent v stack =
        function
        | [] -> List.rev <| struct (v, v) :: stack
        | h as struct (l, r) :: tail when l <= v && v <= r -> (List.rev stack) @ h :: tail
        | struct (l, r) :: tail when v < l && isAdjacent v l -> (List.rev stack) @ struct (v, r) :: tail
        | struct (l, r) :: tail when v > r && isAdjacent v r ->
            (List.rev stack) @ (mergeList isAdjacent struct (l, v) tail)
        | struct (l, r) :: tail when v < l -> (List.rev stack) @ struct (v, v) :: struct (l, r) :: tail
        | h :: tail -> addValue isAdjacent v (h :: stack) tail


    /// Adds a value to the range list, merging it with any adjacent or overlapping ranges.
    ///
    /// <para>Two ranges are merged when they overlap or are adjacent (i.e., the gap
    /// between them is at most 1 in the discrete domain). For example, [1,3] and [5,7]
    /// merge into [1,7] when value 4 is added.</para>
    ///
    /// <para>Time complexity: O(k) where k is the number of ranges. For consecutive
    /// values, k remains 1 regardless of how many values are added.</para>
    ///
    /// <para>The <paramref name="isAdjacent"/> function determines whether two values are
    /// considered adjacent (i.e., should be merged into one range). For integers, this
    /// typically means the absolute difference is 1. For HTTP/2 stream IDs mapped to
    /// consecutive indices, this also means difference of 1.</para>
    let add isAdjacent ranges value = addValue isAdjacent value [] ranges
