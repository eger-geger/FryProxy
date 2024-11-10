module FryProxy.Http.Hpack.Huffman

open FryProxy.Http
open Microsoft.FSharp.Core

[<Struct>]
type CharCode = { Char: char; Code: uint32; Len: uint8 }

/// Align (shift left) the character code to most significant bit.
let msbCode (code: CharCode) = code.Code <<< (32 - int code.Len)

let Table =
    [ { Char = char 0; Code = 0x1ff8ul; Len = 13uy }
      { Char = char 1; Code = 0x7fffd8ul; Len = 23uy }
      { Char = char 2; Code = 0xfffffe2ul; Len = 28uy }
      { Char = char 3; Code = 0xfffffe3ul; Len = 28uy }
      { Char = char 4; Code = 0xfffffe4ul; Len = 28uy }
      { Char = char 5; Code = 0xfffffe5ul; Len = 28uy }
      { Char = char 6; Code = 0xfffffe6ul; Len = 28uy }
      { Char = char 7; Code = 0xfffffe7ul; Len = 28uy }
      { Char = char 8; Code = 0xfffffe8ul; Len = 28uy }
      { Char = char 9; Code = 0xffffeaul; Len = 24uy }
      { Char = char 10; Code = 0x3ffffffcul; Len = 30uy }
      { Char = char 11; Code = 0xfffffe9ul; Len = 28uy }
      { Char = char 12; Code = 0xfffffeaul; Len = 28uy }
      { Char = char 13; Code = 0x3ffffffdul; Len = 30uy }
      { Char = char 14; Code = 0xfffffebul; Len = 28uy }
      { Char = char 15; Code = 0xfffffecul; Len = 28uy }
      { Char = char 16; Code = 0xfffffedul; Len = 28uy }
      { Char = char 17; Code = 0xfffffeeul; Len = 28uy }
      { Char = char 18; Code = 0xfffffeful; Len = 28uy }
      { Char = char 19; Code = 0xffffff0ul; Len = 28uy }
      { Char = char 20; Code = 0xffffff1ul; Len = 28uy }
      { Char = char 21; Code = 0xffffff2ul; Len = 28uy }
      { Char = char 22; Code = 0x3ffffffeul; Len = 30uy }
      { Char = char 23; Code = 0xffffff3ul; Len = 28uy }
      { Char = char 24; Code = 0xffffff4ul; Len = 28uy }
      { Char = char 25; Code = 0xffffff5ul; Len = 28uy }
      { Char = char 26; Code = 0xffffff6ul; Len = 28uy }
      { Char = char 27; Code = 0xffffff7ul; Len = 28uy }
      { Char = char 28; Code = 0xffffff8ul; Len = 28uy }
      { Char = char 29; Code = 0xffffff9ul; Len = 28uy }
      { Char = char 30; Code = 0xffffffaul; Len = 28uy }
      { Char = char 31; Code = 0xffffffbul; Len = 28uy }
      { Char = char 32; Code = 0x14ul; Len = 6uy }
      { Char = char 33; Code = 0x3f8ul; Len = 10uy }
      { Char = char 34; Code = 0x3f9ul; Len = 10uy }
      { Char = char 35; Code = 0xffaul; Len = 12uy }
      { Char = char 36; Code = 0x1ff9ul; Len = 13uy }
      { Char = char 37; Code = 0x15ul; Len = 6uy }
      { Char = char 38; Code = 0xf8ul; Len = 8uy }
      { Char = char 39; Code = 0x7faul; Len = 11uy }
      { Char = char 40; Code = 0x3faul; Len = 10uy }
      { Char = char 41; Code = 0x3fbul; Len = 10uy }
      { Char = char 42; Code = 0xf9ul; Len = 8uy }
      { Char = char 43; Code = 0x7fbul; Len = 11uy }
      { Char = char 44; Code = 0xfaul; Len = 8uy }
      { Char = char 45; Code = 0x16ul; Len = 6uy }
      { Char = char 46; Code = 0x17ul; Len = 6uy }
      { Char = char 47; Code = 0x18ul; Len = 6uy }
      { Char = char 48; Code = 0x0ul; Len = 5uy }
      { Char = char 49; Code = 0x1ul; Len = 5uy }
      { Char = char 50; Code = 0x2ul; Len = 5uy }
      { Char = char 51; Code = 0x19ul; Len = 6uy }
      { Char = char 52; Code = 0x1aul; Len = 6uy }
      { Char = char 53; Code = 0x1bul; Len = 6uy }
      { Char = char 54; Code = 0x1cul; Len = 6uy }
      { Char = char 55; Code = 0x1dul; Len = 6uy }
      { Char = char 56; Code = 0x1eul; Len = 6uy }
      { Char = char 57; Code = 0x1ful; Len = 6uy }
      { Char = char 58; Code = 0x5cul; Len = 7uy }
      { Char = char 59; Code = 0xfbul; Len = 8uy }
      { Char = char 60; Code = 0x7ffcul; Len = 15uy }
      { Char = char 61; Code = 0x20ul; Len = 6uy }
      { Char = char 62; Code = 0xffbul; Len = 12uy }
      { Char = char 63; Code = 0x3fcul; Len = 10uy }
      { Char = char 64; Code = 0x1ffaul; Len = 13uy }
      { Char = char 65; Code = 0x21ul; Len = 6uy }
      { Char = char 66; Code = 0x5dul; Len = 7uy }
      { Char = char 67; Code = 0x5eul; Len = 7uy }
      { Char = char 68; Code = 0x5ful; Len = 7uy }
      { Char = char 69; Code = 0x60ul; Len = 7uy }
      { Char = char 70; Code = 0x61ul; Len = 7uy }
      { Char = char 71; Code = 0x62ul; Len = 7uy }
      { Char = char 72; Code = 0x63ul; Len = 7uy }
      { Char = char 73; Code = 0x64ul; Len = 7uy }
      { Char = char 74; Code = 0x65ul; Len = 7uy }
      { Char = char 75; Code = 0x66ul; Len = 7uy }
      { Char = char 76; Code = 0x67ul; Len = 7uy }
      { Char = char 77; Code = 0x68ul; Len = 7uy }
      { Char = char 78; Code = 0x69ul; Len = 7uy }
      { Char = char 79; Code = 0x6aul; Len = 7uy }
      { Char = char 80; Code = 0x6bul; Len = 7uy }
      { Char = char 81; Code = 0x6cul; Len = 7uy }
      { Char = char 82; Code = 0x6dul; Len = 7uy }
      { Char = char 83; Code = 0x6eul; Len = 7uy }
      { Char = char 84; Code = 0x6ful; Len = 7uy }
      { Char = char 85; Code = 0x70ul; Len = 7uy }
      { Char = char 86; Code = 0x71ul; Len = 7uy }
      { Char = char 87; Code = 0x72ul; Len = 7uy }
      { Char = char 88; Code = 0xfcul; Len = 8uy }
      { Char = char 89; Code = 0x73ul; Len = 7uy }
      { Char = char 90; Code = 0xfdul; Len = 8uy }
      { Char = char 91; Code = 0x1ffbul; Len = 13uy }
      { Char = char 92; Code = 0x7fff0ul; Len = 19uy }
      { Char = char 93; Code = 0x1ffcul; Len = 13uy }
      { Char = char 94; Code = 0x3ffcul; Len = 14uy }
      { Char = char 95; Code = 0x22ul; Len = 6uy }
      { Char = char 96; Code = 0x7ffdul; Len = 15uy }
      { Char = char 97; Code = 0x3ul; Len = 5uy }
      { Char = char 98; Code = 0x23ul; Len = 6uy }
      { Char = char 99; Code = 0x4ul; Len = 5uy }
      { Char = char 100; Code = 0x24ul; Len = 6uy }
      { Char = char 101; Code = 0x5ul; Len = 5uy }
      { Char = char 102; Code = 0x25ul; Len = 6uy }
      { Char = char 103; Code = 0x26ul; Len = 6uy }
      { Char = char 104; Code = 0x27ul; Len = 6uy }
      { Char = char 105; Code = 0x6ul; Len = 5uy }
      { Char = char 106; Code = 0x74ul; Len = 7uy }
      { Char = char 107; Code = 0x75ul; Len = 7uy }
      { Char = char 108; Code = 0x28ul; Len = 6uy }
      { Char = char 109; Code = 0x29ul; Len = 6uy }
      { Char = char 110; Code = 0x2aul; Len = 6uy }
      { Char = char 111; Code = 0x7ul; Len = 5uy }
      { Char = char 112; Code = 0x2bul; Len = 6uy }
      { Char = char 113; Code = 0x76ul; Len = 7uy }
      { Char = char 114; Code = 0x2cul; Len = 6uy }
      { Char = char 115; Code = 0x8ul; Len = 5uy }
      { Char = char 116; Code = 0x9ul; Len = 5uy }
      { Char = char 117; Code = 0x2dul; Len = 6uy }
      { Char = char 118; Code = 0x77ul; Len = 7uy }
      { Char = char 119; Code = 0x78ul; Len = 7uy }
      { Char = char 120; Code = 0x79ul; Len = 7uy }
      { Char = char 121; Code = 0x7aul; Len = 7uy }
      { Char = char 122; Code = 0x7bul; Len = 7uy }
      { Char = char 123; Code = 0x7ffeul; Len = 15uy }
      { Char = char 124; Code = 0x7fcul; Len = 11uy }
      { Char = char 125; Code = 0x3ffdul; Len = 14uy }
      { Char = char 126; Code = 0x1ffdul; Len = 13uy }
      { Char = char 127; Code = 0xffffffcul; Len = 28uy }
      { Char = char 128; Code = 0xfffe6ul; Len = 20uy }
      { Char = char 129; Code = 0x3fffd2ul; Len = 22uy }
      { Char = char 130; Code = 0xfffe7ul; Len = 20uy }
      { Char = char 131; Code = 0xfffe8ul; Len = 20uy }
      { Char = char 132; Code = 0x3fffd3ul; Len = 22uy }
      { Char = char 133; Code = 0x3fffd4ul; Len = 22uy }
      { Char = char 134; Code = 0x3fffd5ul; Len = 22uy }
      { Char = char 135; Code = 0x7fffd9ul; Len = 23uy }
      { Char = char 136; Code = 0x3fffd6ul; Len = 22uy }
      { Char = char 137; Code = 0x7fffdaul; Len = 23uy }
      { Char = char 138; Code = 0x7fffdbul; Len = 23uy }
      { Char = char 139; Code = 0x7fffdcul; Len = 23uy }
      { Char = char 140; Code = 0x7fffddul; Len = 23uy }
      { Char = char 141; Code = 0x7fffdeul; Len = 23uy }
      { Char = char 142; Code = 0xffffebul; Len = 24uy }
      { Char = char 143; Code = 0x7fffdful; Len = 23uy }
      { Char = char 144; Code = 0xffffecul; Len = 24uy }
      { Char = char 145; Code = 0xffffedul; Len = 24uy }
      { Char = char 146; Code = 0x3fffd7ul; Len = 22uy }
      { Char = char 147; Code = 0x7fffe0ul; Len = 23uy }
      { Char = char 148; Code = 0xffffeeul; Len = 24uy }
      { Char = char 149; Code = 0x7fffe1ul; Len = 23uy }
      { Char = char 150; Code = 0x7fffe2ul; Len = 23uy }
      { Char = char 151; Code = 0x7fffe3ul; Len = 23uy }
      { Char = char 152; Code = 0x7fffe4ul; Len = 23uy }
      { Char = char 153; Code = 0x1fffdcul; Len = 21uy }
      { Char = char 154; Code = 0x3fffd8ul; Len = 22uy }
      { Char = char 155; Code = 0x7fffe5ul; Len = 23uy }
      { Char = char 156; Code = 0x3fffd9ul; Len = 22uy }
      { Char = char 157; Code = 0x7fffe6ul; Len = 23uy }
      { Char = char 158; Code = 0x7fffe7ul; Len = 23uy }
      { Char = char 159; Code = 0xffffeful; Len = 24uy }
      { Char = char 160; Code = 0x3fffdaul; Len = 22uy }
      { Char = char 161; Code = 0x1fffddul; Len = 21uy }
      { Char = char 162; Code = 0xfffe9ul; Len = 20uy }
      { Char = char 163; Code = 0x3fffdbul; Len = 22uy }
      { Char = char 164; Code = 0x3fffdcul; Len = 22uy }
      { Char = char 165; Code = 0x7fffe8ul; Len = 23uy }
      { Char = char 166; Code = 0x7fffe9ul; Len = 23uy }
      { Char = char 167; Code = 0x1fffdeul; Len = 21uy }
      { Char = char 168; Code = 0x7fffeaul; Len = 23uy }
      { Char = char 169; Code = 0x3fffddul; Len = 22uy }
      { Char = char 170; Code = 0x3fffdeul; Len = 22uy }
      { Char = char 171; Code = 0xfffff0ul; Len = 24uy }
      { Char = char 172; Code = 0x1fffdful; Len = 21uy }
      { Char = char 173; Code = 0x3fffdful; Len = 22uy }
      { Char = char 174; Code = 0x7fffebul; Len = 23uy }
      { Char = char 175; Code = 0x7fffecul; Len = 23uy }
      { Char = char 176; Code = 0x1fffe0ul; Len = 21uy }
      { Char = char 177; Code = 0x1fffe1ul; Len = 21uy }
      { Char = char 178; Code = 0x3fffe0ul; Len = 22uy }
      { Char = char 179; Code = 0x1fffe2ul; Len = 21uy }
      { Char = char 180; Code = 0x7fffedul; Len = 23uy }
      { Char = char 181; Code = 0x3fffe1ul; Len = 22uy }
      { Char = char 182; Code = 0x7fffeeul; Len = 23uy }
      { Char = char 183; Code = 0x7fffeful; Len = 23uy }
      { Char = char 184; Code = 0xfffeaul; Len = 20uy }
      { Char = char 185; Code = 0x3fffe2ul; Len = 22uy }
      { Char = char 186; Code = 0x3fffe3ul; Len = 22uy }
      { Char = char 187; Code = 0x3fffe4ul; Len = 22uy }
      { Char = char 188; Code = 0x7ffff0ul; Len = 23uy }
      { Char = char 189; Code = 0x3fffe5ul; Len = 22uy }
      { Char = char 190; Code = 0x3fffe6ul; Len = 22uy }
      { Char = char 191; Code = 0x7ffff1ul; Len = 23uy }
      { Char = char 192; Code = 0x3ffffe0ul; Len = 26uy }
      { Char = char 193; Code = 0x3ffffe1ul; Len = 26uy }
      { Char = char 194; Code = 0xfffebul; Len = 20uy }
      { Char = char 195; Code = 0x7fff1ul; Len = 19uy }
      { Char = char 196; Code = 0x3fffe7ul; Len = 22uy }
      { Char = char 197; Code = 0x7ffff2ul; Len = 23uy }
      { Char = char 198; Code = 0x3fffe8ul; Len = 22uy }
      { Char = char 199; Code = 0x1ffffecul; Len = 25uy }
      { Char = char 200; Code = 0x3ffffe2ul; Len = 26uy }
      { Char = char 201; Code = 0x3ffffe3ul; Len = 26uy }
      { Char = char 202; Code = 0x3ffffe4ul; Len = 26uy }
      { Char = char 203; Code = 0x7ffffdeul; Len = 27uy }
      { Char = char 204; Code = 0x7ffffdful; Len = 27uy }
      { Char = char 205; Code = 0x3ffffe5ul; Len = 26uy }
      { Char = char 206; Code = 0xfffff1ul; Len = 24uy }
      { Char = char 207; Code = 0x1ffffedul; Len = 25uy }
      { Char = char 208; Code = 0x7fff2ul; Len = 19uy }
      { Char = char 209; Code = 0x1fffe3ul; Len = 21uy }
      { Char = char 210; Code = 0x3ffffe6ul; Len = 26uy }
      { Char = char 211; Code = 0x7ffffe0ul; Len = 27uy }
      { Char = char 212; Code = 0x7ffffe1ul; Len = 27uy }
      { Char = char 213; Code = 0x3ffffe7ul; Len = 26uy }
      { Char = char 214; Code = 0x7ffffe2ul; Len = 27uy }
      { Char = char 215; Code = 0xfffff2ul; Len = 24uy }
      { Char = char 216; Code = 0x1fffe4ul; Len = 21uy }
      { Char = char 217; Code = 0x1fffe5ul; Len = 21uy }
      { Char = char 218; Code = 0x3ffffe8ul; Len = 26uy }
      { Char = char 219; Code = 0x3ffffe9ul; Len = 26uy }
      { Char = char 220; Code = 0xffffffdul; Len = 28uy }
      { Char = char 221; Code = 0x7ffffe3ul; Len = 27uy }
      { Char = char 222; Code = 0x7ffffe4ul; Len = 27uy }
      { Char = char 223; Code = 0x7ffffe5ul; Len = 27uy }
      { Char = char 224; Code = 0xfffecul; Len = 20uy }
      { Char = char 225; Code = 0xfffff3ul; Len = 24uy }
      { Char = char 226; Code = 0xfffedul; Len = 20uy }
      { Char = char 227; Code = 0x1fffe6ul; Len = 21uy }
      { Char = char 228; Code = 0x3fffe9ul; Len = 22uy }
      { Char = char 229; Code = 0x1fffe7ul; Len = 21uy }
      { Char = char 230; Code = 0x1fffe8ul; Len = 21uy }
      { Char = char 231; Code = 0x7ffff3ul; Len = 23uy }
      { Char = char 232; Code = 0x3fffeaul; Len = 22uy }
      { Char = char 233; Code = 0x3fffebul; Len = 22uy }
      { Char = char 234; Code = 0x1ffffeeul; Len = 25uy }
      { Char = char 235; Code = 0x1ffffeful; Len = 25uy }
      { Char = char 236; Code = 0xfffff4ul; Len = 24uy }
      { Char = char 237; Code = 0xfffff5ul; Len = 24uy }
      { Char = char 238; Code = 0x3ffffeaul; Len = 26uy }
      { Char = char 239; Code = 0x7ffff4ul; Len = 23uy }
      { Char = char 240; Code = 0x3ffffebul; Len = 26uy }
      { Char = char 241; Code = 0x7ffffe6ul; Len = 27uy }
      { Char = char 242; Code = 0x3ffffecul; Len = 26uy }
      { Char = char 243; Code = 0x3ffffedul; Len = 26uy }
      { Char = char 244; Code = 0x7ffffe7ul; Len = 27uy }
      { Char = char 245; Code = 0x7ffffe8ul; Len = 27uy }
      { Char = char 246; Code = 0x7ffffe9ul; Len = 27uy }
      { Char = char 247; Code = 0x7ffffeaul; Len = 27uy }
      { Char = char 248; Code = 0x7ffffebul; Len = 27uy }
      { Char = char 249; Code = 0xffffffeul; Len = 28uy }
      { Char = char 250; Code = 0x7ffffecul; Len = 27uy }
      { Char = char 251; Code = 0x7ffffedul; Len = 27uy }
      { Char = char 252; Code = 0x7ffffeeul; Len = 27uy }
      { Char = char 253; Code = 0x7ffffeful; Len = 27uy }
      { Char = char 254; Code = 0x7fffff0ul; Len = 27uy }
      { Char = char 255; Code = 0x3ffffeeul; Len = 26uy }
      { Char = Tokens.EOS; Code = 0x3ffffffful; Len = 30uy } ]

//                                                     code
//                        code as bits                 as hex   len
//      sym              aligned to MSB                aligned   in
//                                                     to LSB   bits
//     (  0)  |11111111|11000                             1ff8  [13]
//     (  1)  |11111111|11111111|1011000                7fffd8  [23]
//     (  2)  |11111111|11111111|11111110|0010         fffffe2  [28]
//     (  3)  |11111111|11111111|11111110|0011         fffffe3  [28]
//     (  4)  |11111111|11111111|11111110|0100         fffffe4  [28]
//     (  5)  |11111111|11111111|11111110|0101         fffffe5  [28]
//     (  6)  |11111111|11111111|11111110|0110         fffffe6  [28]
//     (  7)  |11111111|11111111|11111110|0111         fffffe7  [28]
//     (  8)  |11111111|11111111|11111110|1000         fffffe8  [28]
//     (  9)  |11111111|11111111|11101010               ffffea  [24]
//     ( 10)  |11111111|11111111|11111111|111100      3ffffffc  [30]
//     ( 11)  |11111111|11111111|11111110|1001         fffffe9  [28]
//     ( 12)  |11111111|11111111|11111110|1010         fffffea  [28]
//     ( 13)  |11111111|11111111|11111111|111101      3ffffffd  [30]
//     ( 14)  |11111111|11111111|11111110|1011         fffffeb  [28]
//     ( 15)  |11111111|11111111|11111110|1100         fffffec  [28]
//     ( 16)  |11111111|11111111|11111110|1101         fffffed  [28]
//     ( 17)  |11111111|11111111|11111110|1110         fffffee  [28]
//     ( 18)  |11111111|11111111|11111110|1111         fffffef  [28]
//     ( 19)  |11111111|11111111|11111111|0000         ffffff0  [28]
//     ( 20)  |11111111|11111111|11111111|0001         ffffff1  [28]
//     ( 21)  |11111111|11111111|11111111|0010         ffffff2  [28]
//     ( 22)  |11111111|11111111|11111111|111110      3ffffffe  [30]
//     ( 23)  |11111111|11111111|11111111|0011         ffffff3  [28]
//     ( 24)  |11111111|11111111|11111111|0100         ffffff4  [28]
//     ( 25)  |11111111|11111111|11111111|0101         ffffff5  [28]
//     ( 26)  |11111111|11111111|11111111|0110         ffffff6  [28]
//     ( 27)  |11111111|11111111|11111111|0111         ffffff7  [28]
//     ( 28)  |11111111|11111111|11111111|1000         ffffff8  [28]
//     ( 29)  |11111111|11111111|11111111|1001         ffffff9  [28]
//     ( 30)  |11111111|11111111|11111111|1010         ffffffa  [28]
//     ( 31)  |11111111|11111111|11111111|1011         ffffffb  [28]
// ' ' ( 32)  |010100                                       14  [ 6]
// '!' ( 33)  |11111110|00                                 3f8  [10]
// '"' ( 34)  |11111110|01                                 3f9  [10]
// '#' ( 35)  |11111111|1010                               ffa  [12]
// '$' ( 36)  |11111111|11001                             1ff9  [13]
// '%' ( 37)  |010101                                       15  [ 6]
// '&' ( 38)  |11111000                                     f8  [ 8]
// ''' ( 39)  |11111111|010                                7fa  [11]
// '(' ( 40)  |11111110|10                                 3fa  [10]
// ')' ( 41)  |11111110|11                                 3fb  [10]
// '*' ( 42)  |11111001                                     f9  [ 8]
// '+' ( 43)  |11111111|011                                7fb  [11]
// ',' ( 44)  |11111010                                     fa  [ 8]
// '-' ( 45)  |010110                                       16  [ 6]
// '.' ( 46)  |010111                                       17  [ 6]
// '/' ( 47)  |011000                                       18  [ 6]
// '0' ( 48)  |00000                                         0  [ 5]
// '1' ( 49)  |00001                                         1  [ 5]
// '2' ( 50)  |00010                                         2  [ 5]
// '3' ( 51)  |011001                                       19  [ 6]
// '4' ( 52)  |011010                                       1a  [ 6]
// '5' ( 53)  |011011                                       1b  [ 6]
// '6' ( 54)  |011100                                       1c  [ 6]
// '7' ( 55)  |011101                                       1d  [ 6]
// '8' ( 56)  |011110                                       1e  [ 6]
// '9' ( 57)  |011111                                       1f  [ 6]
// ':' ( 58)  |1011100                                      5c  [ 7]
// ';' ( 59)  |11111011                                     fb  [ 8]
// '<' ( 60)  |11111111|1111100                           7ffc  [15]
// '=' ( 61)  |100000                                       20  [ 6]
// '>' ( 62)  |11111111|1011                               ffb  [12]
// '?' ( 63)  |11111111|00                                 3fc  [10]
// '@' ( 64)  |11111111|11010                             1ffa  [13]
// 'A' ( 65)  |100001                                       21  [ 6]
// 'B' ( 66)  |1011101                                      5d  [ 7]
// 'C' ( 67)  |1011110                                      5e  [ 7]
// 'D' ( 68)  |1011111                                      5f  [ 7]
// 'E' ( 69)  |1100000                                      60  [ 7]
// 'F' ( 70)  |1100001                                      61  [ 7]
// 'G' ( 71)  |1100010                                      62  [ 7]
// 'H' ( 72)  |1100011                                      63  [ 7]
// 'I' ( 73)  |1100100                                      64  [ 7]
// 'J' ( 74)  |1100101                                      65  [ 7]
// 'K' ( 75)  |1100110                                      66  [ 7]
// 'L' ( 76)  |1100111                                      67  [ 7]
// 'M' ( 77)  |1101000                                      68  [ 7]
// 'N' ( 78)  |1101001                                      69  [ 7]
// 'O' ( 79)  |1101010                                      6a  [ 7]
// 'P' ( 80)  |1101011                                      6b  [ 7]
// 'Q' ( 81)  |1101100                                      6c  [ 7]
// 'R' ( 82)  |1101101                                      6d  [ 7]
// 'S' ( 83)  |1101110                                      6e  [ 7]
// 'T' ( 84)  |1101111                                      6f  [ 7]
// 'U' ( 85)  |1110000                                      70  [ 7]
// 'V' ( 86)  |1110001                                      71  [ 7]
// 'W' ( 87)  |1110010                                      72  [ 7]
// 'X' ( 88)  |11111100                                     fc  [ 8]
// 'Y' ( 89)  |1110011                                      73  [ 7]
// 'Z' ( 90)  |11111101                                     fd  [ 8]
// '[' ( 91)  |11111111|11011                             1ffb  [13]
// '\' ( 92)  |11111111|11111110|000                     7fff0  [19]
// ']' ( 93)  |11111111|11100                             1ffc  [13]
// '^' ( 94)  |11111111|111100                            3ffc  [14]
// '_' ( 95)  |100010                                       22  [ 6]
// '`' ( 96)  |11111111|1111101                           7ffd  [15]
// 'a' ( 97)  |00011                                         3  [ 5]
// 'b' ( 98)  |100011                                       23  [ 6]
// 'c' ( 99)  |00100                                         4  [ 5]
// 'd' (100)  |100100                                       24  [ 6]
// 'e' (101)  |00101                                         5  [ 5]
// 'f' (102)  |100101                                       25  [ 6]
// 'g' (103)  |100110                                       26  [ 6]
// 'h' (104)  |100111                                       27  [ 6]
// 'i' (105)  |00110                                         6  [ 5]
// 'j' (106)  |1110100                                      74  [ 7]
// 'k' (107)  |1110101                                      75  [ 7]
// 'l' (108)  |101000                                       28  [ 6]
// 'm' (109)  |101001                                       29  [ 6]
// 'n' (110)  |101010                                       2a  [ 6]
// 'o' (111)  |00111                                         7  [ 5]
// 'p' (112)  |101011                                       2b  [ 6]
// 'q' (113)  |1110110                                      76  [ 7]
// 'r' (114)  |101100                                       2c  [ 6]
// 's' (115)  |01000                                         8  [ 5]
// 't' (116)  |01001                                         9  [ 5]
// 'u' (117)  |101101                                       2d  [ 6]
// 'v' (118)  |1110111                                      77  [ 7]
// 'w' (119)  |1111000                                      78  [ 7]
// 'x' (120)  |1111001                                      79  [ 7]
// 'y' (121)  |1111010                                      7a  [ 7]
// 'z' (122)  |1111011                                      7b  [ 7]
// '{' (123)  |11111111|1111110                           7ffe  [15]
// '|' (124)  |11111111|100                                7fc  [11]
// '}' (125)  |11111111|111101                            3ffd  [14]
// '~' (126)  |11111111|11101                             1ffd  [13]
//     (127)  |11111111|11111111|11111111|1100         ffffffc  [28]
//     (128)  |11111111|11111110|0110                    fffe6  [20]
//     (129)  |11111111|11111111|010010                 3fffd2  [22]
//     (130)  |11111111|11111110|0111                    fffe7  [20]
//     (131)  |11111111|11111110|1000                    fffe8  [20]
//     (132)  |11111111|11111111|010011                 3fffd3  [22]
//     (133)  |11111111|11111111|010100                 3fffd4  [22]
//     (134)  |11111111|11111111|010101                 3fffd5  [22]
//     (135)  |11111111|11111111|1011001                7fffd9  [23]
//     (136)  |11111111|11111111|010110                 3fffd6  [22]
//     (137)  |11111111|11111111|1011010                7fffda  [23]
//     (138)  |11111111|11111111|1011011                7fffdb  [23]
//     (139)  |11111111|11111111|1011100                7fffdc  [23]
//     (140)  |11111111|11111111|1011101                7fffdd  [23]
//     (141)  |11111111|11111111|1011110                7fffde  [23]
//     (142)  |11111111|11111111|11101011               ffffeb  [24]
//     (143)  |11111111|11111111|1011111                7fffdf  [23]
//     (144)  |11111111|11111111|11101100               ffffec  [24]
//     (145)  |11111111|11111111|11101101               ffffed  [24]
//     (146)  |11111111|11111111|010111                 3fffd7  [22]
//     (147)  |11111111|11111111|1100000                7fffe0  [23]
//     (148)  |11111111|11111111|11101110               ffffee  [24]
//     (149)  |11111111|11111111|1100001                7fffe1  [23]
//     (150)  |11111111|11111111|1100010                7fffe2  [23]
//     (151)  |11111111|11111111|1100011                7fffe3  [23]
//     (152)  |11111111|11111111|1100100                7fffe4  [23]
//     (153)  |11111111|11111110|11100                  1fffdc  [21]
//     (154)  |11111111|11111111|011000                 3fffd8  [22]
//     (155)  |11111111|11111111|1100101                7fffe5  [23]
//     (156)  |11111111|11111111|011001                 3fffd9  [22]
//     (157)  |11111111|11111111|1100110                7fffe6  [23]
//     (158)  |11111111|11111111|1100111                7fffe7  [23]
//     (159)  |11111111|11111111|11101111               ffffef  [24]
//     (160)  |11111111|11111111|011010                 3fffda  [22]
//     (161)  |11111111|11111110|11101                  1fffdd  [21]
//     (162)  |11111111|11111110|1001                    fffe9  [20]
//     (163)  |11111111|11111111|011011                 3fffdb  [22]
//     (164)  |11111111|11111111|011100                 3fffdc  [22]
//     (165)  |11111111|11111111|1101000                7fffe8  [23]
//     (166)  |11111111|11111111|1101001                7fffe9  [23]
//     (167)  |11111111|11111110|11110                  1fffde  [21]
//     (168)  |11111111|11111111|1101010                7fffea  [23]
//     (169)  |11111111|11111111|011101                 3fffdd  [22]
//     (170)  |11111111|11111111|011110                 3fffde  [22]
//     (171)  |11111111|11111111|11110000               fffff0  [24]
//     (172)  |11111111|11111110|11111                  1fffdf  [21]
//     (173)  |11111111|11111111|011111                 3fffdf  [22]
//     (174)  |11111111|11111111|1101011                7fffeb  [23]
//     (175)  |11111111|11111111|1101100                7fffec  [23]
//     (176)  |11111111|11111111|00000                  1fffe0  [21]
//     (177)  |11111111|11111111|00001                  1fffe1  [21]
//     (178)  |11111111|11111111|100000                 3fffe0  [22]
//     (179)  |11111111|11111111|00010                  1fffe2  [21]
//     (180)  |11111111|11111111|1101101                7fffed  [23]
//     (181)  |11111111|11111111|100001                 3fffe1  [22]
//     (182)  |11111111|11111111|1101110                7fffee  [23]
//     (183)  |11111111|11111111|1101111                7fffef  [23]
//     (184)  |11111111|11111110|1010                    fffea  [20]
//     (185)  |11111111|11111111|100010                 3fffe2  [22]
//     (186)  |11111111|11111111|100011                 3fffe3  [22]
//     (187)  |11111111|11111111|100100                 3fffe4  [22]
//     (188)  |11111111|11111111|1110000                7ffff0  [23]
//     (189)  |11111111|11111111|100101                 3fffe5  [22]
//     (190)  |11111111|11111111|100110                 3fffe6  [22]
//     (191)  |11111111|11111111|1110001                7ffff1  [23]
//     (192)  |11111111|11111111|11111000|00           3ffffe0  [26]
//     (193)  |11111111|11111111|11111000|01           3ffffe1  [26]
//     (194)  |11111111|11111110|1011                    fffeb  [20]
//     (195)  |11111111|11111110|001                     7fff1  [19]
//     (196)  |11111111|11111111|100111                 3fffe7  [22]
//     (197)  |11111111|11111111|1110010                7ffff2  [23]
//     (198)  |11111111|11111111|101000                 3fffe8  [22]
//     (199)  |11111111|11111111|11110110|0            1ffffec  [25]
//     (200)  |11111111|11111111|11111000|10           3ffffe2  [26]
//     (201)  |11111111|11111111|11111000|11           3ffffe3  [26]
//     (202)  |11111111|11111111|11111001|00           3ffffe4  [26]
//     (203)  |11111111|11111111|11111011|110          7ffffde  [27]
//     (204)  |11111111|11111111|11111011|111          7ffffdf  [27]
//     (205)  |11111111|11111111|11111001|01           3ffffe5  [26]
//     (206)  |11111111|11111111|11110001               fffff1  [24]
//     (207)  |11111111|11111111|11110110|1            1ffffed  [25]
//     (208)  |11111111|11111110|010                     7fff2  [19]
//     (209)  |11111111|11111111|00011                  1fffe3  [21]
//     (210)  |11111111|11111111|11111001|10           3ffffe6  [26]
//     (211)  |11111111|11111111|11111100|000          7ffffe0  [27]
//     (212)  |11111111|11111111|11111100|001          7ffffe1  [27]
//     (213)  |11111111|11111111|11111001|11           3ffffe7  [26]
//     (214)  |11111111|11111111|11111100|010          7ffffe2  [27]
//     (215)  |11111111|11111111|11110010               fffff2  [24]
//     (216)  |11111111|11111111|00100                  1fffe4  [21]
//     (217)  |11111111|11111111|00101                  1fffe5  [21]
//     (218)  |11111111|11111111|11111010|00           3ffffe8  [26]
//     (219)  |11111111|11111111|11111010|01           3ffffe9  [26]
//     (220)  |11111111|11111111|11111111|1101         ffffffd  [28]
//     (221)  |11111111|11111111|11111100|011          7ffffe3  [27]
//     (222)  |11111111|11111111|11111100|100          7ffffe4  [27]
//     (223)  |11111111|11111111|11111100|101          7ffffe5  [27]
//     (224)  |11111111|11111110|1100                    fffec  [20]
//     (225)  |11111111|11111111|11110011               fffff3  [24]
//     (226)  |11111111|11111110|1101                    fffed  [20]
//     (227)  |11111111|11111111|00110                  1fffe6  [21]
//     (228)  |11111111|11111111|101001                 3fffe9  [22]
//     (229)  |11111111|11111111|00111                  1fffe7  [21]
//     (230)  |11111111|11111111|01000                  1fffe8  [21]
//     (231)  |11111111|11111111|1110011                7ffff3  [23]
//     (232)  |11111111|11111111|101010                 3fffea  [22]
//     (233)  |11111111|11111111|101011                 3fffeb  [22]
//     (234)  |11111111|11111111|11110111|0            1ffffee  [25]
//     (235)  |11111111|11111111|11110111|1            1ffffef  [25]
//     (236)  |11111111|11111111|11110100               fffff4  [24]
//     (237)  |11111111|11111111|11110101               fffff5  [24]
//     (238)  |11111111|11111111|11111010|10           3ffffea  [26]
//     (239)  |11111111|11111111|1110100                7ffff4  [23]
//     (240)  |11111111|11111111|11111010|11           3ffffeb  [26]
//     (241)  |11111111|11111111|11111100|110          7ffffe6  [27]
//     (242)  |11111111|11111111|11111011|00           3ffffec  [26]
//     (243)  |11111111|11111111|11111011|01           3ffffed  [26]
//     (244)  |11111111|11111111|11111100|111          7ffffe7  [27]
//     (245)  |11111111|11111111|11111101|000          7ffffe8  [27]
//     (246)  |11111111|11111111|11111101|001          7ffffe9  [27]
//     (247)  |11111111|11111111|11111101|010          7ffffea  [27]
//     (248)  |11111111|11111111|11111101|011          7ffffeb  [27]
//     (249)  |11111111|11111111|11111111|1110         ffffffe  [28]
//     (250)  |11111111|11111111|11111101|100          7ffffec  [27]
//     (251)  |11111111|11111111|11111101|101          7ffffed  [27]
//     (252)  |11111111|11111111|11111101|110          7ffffee  [27]
//     (253)  |11111111|11111111|11111101|111          7ffffef  [27]
//     (254)  |11111111|11111111|11111110|000          7fffff0  [27]
//     (255)  |11111111|11111111|11111011|10           3ffffee  [26]
// EOS (256)  |11111111|11111111|11111111|111111      3fffffff  [30]

type Tree =
    | Leaf of CharCode
    | Node of Left: Tree * Right: Tree

let private empty = Leaf({ Char = char 0xffffff; Code = 0xffffffu; Len = 0xffuy })

let inline private pickSide code bit (left, right) =
    match code &&& (1ul <<< bit - 1) with
    | 0ul -> left
    | _ -> right

[<TailCall>]
let rec private insertAt code depth tree wrap : Tree =
    if depth = 0 then
        Leaf(code) |> wrap
    else
        let lt, rt =
            match tree with
            | Node(left, right) -> left, right
            | Leaf _ -> empty, empty

        let inline insertLt lt = Node(lt, rt)
        let inline insertRt rt = Node(lt, rt)

        let next, cont = ((lt, insertLt), (rt, insertRt)) |> pickSide code.Code depth

        insertAt code (depth - 1) next (cont >> wrap)

let inline private insert tree code = insertAt code (int code.Len) tree id

/// Huffman tree used for decoding a character.
let Root = (empty, Table) ||> List.fold insert

/// Decode a character from code aligned to most significant bit.
let decodeChar code =
    let rec loop offset tree =
        match tree with
        | Leaf value -> value
        | Node(l, r) -> pickSide code (32 - offset) (l, r) |> loop(offset + 1)

    loop 0 Root

/// Encode ASCII character or EOS.
let encodeChar (char: char) = Table[int char]
