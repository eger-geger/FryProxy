module System.Tuple

let inline create2 a b = a, b

let inline append1 b a = create2 a b

let inline append2 c (a, b) = a, b, c

let inline map2of3 fn (a, b, c) = a, fn(b), c