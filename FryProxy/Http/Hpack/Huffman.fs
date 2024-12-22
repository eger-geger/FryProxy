module FryProxy.Http.Hpack.Huffman

open System
open FryProxy.Extension
open Microsoft.FSharp.Core

/// Path and depth within Huffman tree.
[<Struct>]
type Code = { Value: uint32; Size: uint8 }

[<Struct>]
type CharCode = { Char: char; Code: Code }

/// Align (shift) the code to the left, placing most significant bit at the start.
let inline alignLeft code =
    { code with Value = code.Value <<< (32 - int code.Size) }

/// Table of static Huffman codes for each ASCII symbol + EOS.
let Table =
    [| { Char = char 000; Code = { Value = 0x00001ff8ul; Size = 13uy } }
       { Char = char 001; Code = { Value = 0x007fffd8ul; Size = 23uy } }
       { Char = char 002; Code = { Value = 0x0fffffe2ul; Size = 28uy } }
       { Char = char 003; Code = { Value = 0x0fffffe3ul; Size = 28uy } }
       { Char = char 004; Code = { Value = 0x0fffffe4ul; Size = 28uy } }
       { Char = char 005; Code = { Value = 0x0fffffe5ul; Size = 28uy } }
       { Char = char 006; Code = { Value = 0x0fffffe6ul; Size = 28uy } }
       { Char = char 007; Code = { Value = 0x0fffffe7ul; Size = 28uy } }
       { Char = char 008; Code = { Value = 0x0fffffe8ul; Size = 28uy } }
       { Char = char 009; Code = { Value = 0x00ffffeaul; Size = 24uy } }
       { Char = char 010; Code = { Value = 0x3ffffffcul; Size = 30uy } }
       { Char = char 011; Code = { Value = 0x0fffffe9ul; Size = 28uy } }
       { Char = char 012; Code = { Value = 0x0fffffeaul; Size = 28uy } }
       { Char = char 013; Code = { Value = 0x3ffffffdul; Size = 30uy } }
       { Char = char 014; Code = { Value = 0x0fffffebul; Size = 28uy } }
       { Char = char 015; Code = { Value = 0x0fffffecul; Size = 28uy } }
       { Char = char 016; Code = { Value = 0x0fffffedul; Size = 28uy } }
       { Char = char 017; Code = { Value = 0x0fffffeeul; Size = 28uy } }
       { Char = char 018; Code = { Value = 0x0fffffeful; Size = 28uy } }
       { Char = char 019; Code = { Value = 0x0ffffff0ul; Size = 28uy } }
       { Char = char 020; Code = { Value = 0x0ffffff1ul; Size = 28uy } }
       { Char = char 021; Code = { Value = 0x0ffffff2ul; Size = 28uy } }
       { Char = char 022; Code = { Value = 0x3ffffffeul; Size = 30uy } }
       { Char = char 023; Code = { Value = 0x0ffffff3ul; Size = 28uy } }
       { Char = char 024; Code = { Value = 0x0ffffff4ul; Size = 28uy } }
       { Char = char 025; Code = { Value = 0x0ffffff5ul; Size = 28uy } }
       { Char = char 026; Code = { Value = 0x0ffffff6ul; Size = 28uy } }
       { Char = char 027; Code = { Value = 0x0ffffff7ul; Size = 28uy } }
       { Char = char 028; Code = { Value = 0x0ffffff8ul; Size = 28uy } }
       { Char = char 029; Code = { Value = 0x0ffffff9ul; Size = 28uy } }
       { Char = char 030; Code = { Value = 0x0ffffffaul; Size = 28uy } }
       { Char = char 031; Code = { Value = 0x0ffffffbul; Size = 28uy } }
       { Char = char 032; Code = { Value = 0x00000014ul; Size = 6uy } }
       { Char = char 033; Code = { Value = 0x000003f8ul; Size = 10uy } }
       { Char = char 034; Code = { Value = 0x000003f9ul; Size = 10uy } }
       { Char = char 035; Code = { Value = 0x00000ffaul; Size = 12uy } }
       { Char = char 036; Code = { Value = 0x00001ff9ul; Size = 13uy } }
       { Char = char 037; Code = { Value = 0x00000015ul; Size = 6uy } }
       { Char = char 038; Code = { Value = 0x000000f8ul; Size = 8uy } }
       { Char = char 039; Code = { Value = 0x000007faul; Size = 11uy } }
       { Char = char 040; Code = { Value = 0x000003faul; Size = 10uy } }
       { Char = char 041; Code = { Value = 0x000003fbul; Size = 10uy } }
       { Char = char 042; Code = { Value = 0x000000f9ul; Size = 8uy } }
       { Char = char 043; Code = { Value = 0x000007fbul; Size = 11uy } }
       { Char = char 044; Code = { Value = 0x000000faul; Size = 8uy } }
       { Char = char 045; Code = { Value = 0x00000016ul; Size = 6uy } }
       { Char = char 046; Code = { Value = 0x00000017ul; Size = 6uy } }
       { Char = char 047; Code = { Value = 0x00000018ul; Size = 6uy } }
       { Char = char 048; Code = { Value = 0x00000000ul; Size = 5uy } }
       { Char = char 049; Code = { Value = 0x00000001ul; Size = 5uy } }
       { Char = char 050; Code = { Value = 0x00000002ul; Size = 5uy } }
       { Char = char 051; Code = { Value = 0x00000019ul; Size = 6uy } }
       { Char = char 052; Code = { Value = 0x0000001aul; Size = 6uy } }
       { Char = char 053; Code = { Value = 0x0000001bul; Size = 6uy } }
       { Char = char 054; Code = { Value = 0x0000001cul; Size = 6uy } }
       { Char = char 055; Code = { Value = 0x0000001dul; Size = 6uy } }
       { Char = char 056; Code = { Value = 0x0000001eul; Size = 6uy } }
       { Char = char 057; Code = { Value = 0x0000001ful; Size = 6uy } }
       { Char = char 058; Code = { Value = 0x0000005cul; Size = 7uy } }
       { Char = char 059; Code = { Value = 0x000000fbul; Size = 8uy } }
       { Char = char 060; Code = { Value = 0x00007ffcul; Size = 15uy } }
       { Char = char 061; Code = { Value = 0x00000020ul; Size = 6uy } }
       { Char = char 062; Code = { Value = 0x00000ffbul; Size = 12uy } }
       { Char = char 063; Code = { Value = 0x000003fcul; Size = 10uy } }
       { Char = char 064; Code = { Value = 0x00001ffaul; Size = 13uy } }
       { Char = char 065; Code = { Value = 0x00000021ul; Size = 6uy } }
       { Char = char 066; Code = { Value = 0x0000005dul; Size = 7uy } }
       { Char = char 067; Code = { Value = 0x0000005eul; Size = 7uy } }
       { Char = char 068; Code = { Value = 0x0000005ful; Size = 7uy } }
       { Char = char 069; Code = { Value = 0x00000060ul; Size = 7uy } }
       { Char = char 070; Code = { Value = 0x00000061ul; Size = 7uy } }
       { Char = char 071; Code = { Value = 0x00000062ul; Size = 7uy } }
       { Char = char 072; Code = { Value = 0x00000063ul; Size = 7uy } }
       { Char = char 073; Code = { Value = 0x00000064ul; Size = 7uy } }
       { Char = char 074; Code = { Value = 0x00000065ul; Size = 7uy } }
       { Char = char 075; Code = { Value = 0x00000066ul; Size = 7uy } }
       { Char = char 076; Code = { Value = 0x00000067ul; Size = 7uy } }
       { Char = char 077; Code = { Value = 0x00000068ul; Size = 7uy } }
       { Char = char 078; Code = { Value = 0x00000069ul; Size = 7uy } }
       { Char = char 079; Code = { Value = 0x0000006aul; Size = 7uy } }
       { Char = char 080; Code = { Value = 0x0000006bul; Size = 7uy } }
       { Char = char 081; Code = { Value = 0x0000006cul; Size = 7uy } }
       { Char = char 082; Code = { Value = 0x0000006dul; Size = 7uy } }
       { Char = char 083; Code = { Value = 0x0000006eul; Size = 7uy } }
       { Char = char 084; Code = { Value = 0x0000006ful; Size = 7uy } }
       { Char = char 085; Code = { Value = 0x00000070ul; Size = 7uy } }
       { Char = char 086; Code = { Value = 0x00000071ul; Size = 7uy } }
       { Char = char 087; Code = { Value = 0x00000072ul; Size = 7uy } }
       { Char = char 088; Code = { Value = 0x000000fcul; Size = 8uy } }
       { Char = char 089; Code = { Value = 0x00000073ul; Size = 7uy } }
       { Char = char 090; Code = { Value = 0x000000fdul; Size = 8uy } }
       { Char = char 091; Code = { Value = 0x00001ffbul; Size = 13uy } }
       { Char = char 092; Code = { Value = 0x0007fff0ul; Size = 19uy } }
       { Char = char 093; Code = { Value = 0x00001ffcul; Size = 13uy } }
       { Char = char 094; Code = { Value = 0x00003ffcul; Size = 14uy } }
       { Char = char 095; Code = { Value = 0x00000022ul; Size = 6uy } }
       { Char = char 096; Code = { Value = 0x00007ffdul; Size = 15uy } }
       { Char = char 097; Code = { Value = 0x00000003ul; Size = 5uy } }
       { Char = char 098; Code = { Value = 0x00000023ul; Size = 6uy } }
       { Char = char 099; Code = { Value = 0x00000004ul; Size = 5uy } }
       { Char = char 100; Code = { Value = 0x00000024ul; Size = 6uy } }
       { Char = char 101; Code = { Value = 0x00000005ul; Size = 5uy } }
       { Char = char 102; Code = { Value = 0x00000025ul; Size = 6uy } }
       { Char = char 103; Code = { Value = 0x00000026ul; Size = 6uy } }
       { Char = char 104; Code = { Value = 0x00000027ul; Size = 6uy } }
       { Char = char 105; Code = { Value = 0x00000006ul; Size = 5uy } }
       { Char = char 106; Code = { Value = 0x00000074ul; Size = 7uy } }
       { Char = char 107; Code = { Value = 0x00000075ul; Size = 7uy } }
       { Char = char 108; Code = { Value = 0x00000028ul; Size = 6uy } }
       { Char = char 109; Code = { Value = 0x00000029ul; Size = 6uy } }
       { Char = char 110; Code = { Value = 0x0000002aul; Size = 6uy } }
       { Char = char 111; Code = { Value = 0x00000007ul; Size = 5uy } }
       { Char = char 112; Code = { Value = 0x0000002bul; Size = 6uy } }
       { Char = char 113; Code = { Value = 0x00000076ul; Size = 7uy } }
       { Char = char 114; Code = { Value = 0x0000002cul; Size = 6uy } }
       { Char = char 115; Code = { Value = 0x00000008ul; Size = 5uy } }
       { Char = char 116; Code = { Value = 0x00000009ul; Size = 5uy } }
       { Char = char 117; Code = { Value = 0x0000002dul; Size = 6uy } }
       { Char = char 118; Code = { Value = 0x00000077ul; Size = 7uy } }
       { Char = char 119; Code = { Value = 0x00000078ul; Size = 7uy } }
       { Char = char 120; Code = { Value = 0x00000079ul; Size = 7uy } }
       { Char = char 121; Code = { Value = 0x0000007aul; Size = 7uy } }
       { Char = char 122; Code = { Value = 0x0000007bul; Size = 7uy } }
       { Char = char 123; Code = { Value = 0x00007ffeul; Size = 15uy } }
       { Char = char 124; Code = { Value = 0x000007fcul; Size = 11uy } }
       { Char = char 125; Code = { Value = 0x00003ffdul; Size = 14uy } }
       { Char = char 126; Code = { Value = 0x00001ffdul; Size = 13uy } }
       { Char = char 127; Code = { Value = 0x0ffffffcul; Size = 28uy } }
       { Char = char 128; Code = { Value = 0x000fffe6ul; Size = 20uy } }
       { Char = char 129; Code = { Value = 0x003fffd2ul; Size = 22uy } }
       { Char = char 130; Code = { Value = 0x000fffe7ul; Size = 20uy } }
       { Char = char 131; Code = { Value = 0x000fffe8ul; Size = 20uy } }
       { Char = char 132; Code = { Value = 0x003fffd3ul; Size = 22uy } }
       { Char = char 133; Code = { Value = 0x003fffd4ul; Size = 22uy } }
       { Char = char 134; Code = { Value = 0x003fffd5ul; Size = 22uy } }
       { Char = char 135; Code = { Value = 0x007fffd9ul; Size = 23uy } }
       { Char = char 136; Code = { Value = 0x003fffd6ul; Size = 22uy } }
       { Char = char 137; Code = { Value = 0x007fffdaul; Size = 23uy } }
       { Char = char 138; Code = { Value = 0x007fffdbul; Size = 23uy } }
       { Char = char 139; Code = { Value = 0x007fffdcul; Size = 23uy } }
       { Char = char 140; Code = { Value = 0x007fffddul; Size = 23uy } }
       { Char = char 141; Code = { Value = 0x007fffdeul; Size = 23uy } }
       { Char = char 142; Code = { Value = 0x00ffffebul; Size = 24uy } }
       { Char = char 143; Code = { Value = 0x007fffdful; Size = 23uy } }
       { Char = char 144; Code = { Value = 0x00ffffecul; Size = 24uy } }
       { Char = char 145; Code = { Value = 0x00ffffedul; Size = 24uy } }
       { Char = char 146; Code = { Value = 0x003fffd7ul; Size = 22uy } }
       { Char = char 147; Code = { Value = 0x007fffe0ul; Size = 23uy } }
       { Char = char 148; Code = { Value = 0x00ffffeeul; Size = 24uy } }
       { Char = char 149; Code = { Value = 0x007fffe1ul; Size = 23uy } }
       { Char = char 150; Code = { Value = 0x007fffe2ul; Size = 23uy } }
       { Char = char 151; Code = { Value = 0x007fffe3ul; Size = 23uy } }
       { Char = char 152; Code = { Value = 0x007fffe4ul; Size = 23uy } }
       { Char = char 153; Code = { Value = 0x001fffdcul; Size = 21uy } }
       { Char = char 154; Code = { Value = 0x003fffd8ul; Size = 22uy } }
       { Char = char 155; Code = { Value = 0x007fffe5ul; Size = 23uy } }
       { Char = char 156; Code = { Value = 0x003fffd9ul; Size = 22uy } }
       { Char = char 157; Code = { Value = 0x007fffe6ul; Size = 23uy } }
       { Char = char 158; Code = { Value = 0x007fffe7ul; Size = 23uy } }
       { Char = char 159; Code = { Value = 0x00ffffeful; Size = 24uy } }
       { Char = char 160; Code = { Value = 0x003fffdaul; Size = 22uy } }
       { Char = char 161; Code = { Value = 0x001fffddul; Size = 21uy } }
       { Char = char 162; Code = { Value = 0x000fffe9ul; Size = 20uy } }
       { Char = char 163; Code = { Value = 0x003fffdbul; Size = 22uy } }
       { Char = char 164; Code = { Value = 0x003fffdcul; Size = 22uy } }
       { Char = char 165; Code = { Value = 0x007fffe8ul; Size = 23uy } }
       { Char = char 166; Code = { Value = 0x007fffe9ul; Size = 23uy } }
       { Char = char 167; Code = { Value = 0x001fffdeul; Size = 21uy } }
       { Char = char 168; Code = { Value = 0x007fffeaul; Size = 23uy } }
       { Char = char 169; Code = { Value = 0x003fffddul; Size = 22uy } }
       { Char = char 170; Code = { Value = 0x003fffdeul; Size = 22uy } }
       { Char = char 171; Code = { Value = 0x00fffff0ul; Size = 24uy } }
       { Char = char 172; Code = { Value = 0x001fffdful; Size = 21uy } }
       { Char = char 173; Code = { Value = 0x003fffdful; Size = 22uy } }
       { Char = char 174; Code = { Value = 0x007fffebul; Size = 23uy } }
       { Char = char 175; Code = { Value = 0x007fffecul; Size = 23uy } }
       { Char = char 176; Code = { Value = 0x001fffe0ul; Size = 21uy } }
       { Char = char 177; Code = { Value = 0x001fffe1ul; Size = 21uy } }
       { Char = char 178; Code = { Value = 0x003fffe0ul; Size = 22uy } }
       { Char = char 179; Code = { Value = 0x001fffe2ul; Size = 21uy } }
       { Char = char 180; Code = { Value = 0x007fffedul; Size = 23uy } }
       { Char = char 181; Code = { Value = 0x003fffe1ul; Size = 22uy } }
       { Char = char 182; Code = { Value = 0x007fffeeul; Size = 23uy } }
       { Char = char 183; Code = { Value = 0x007fffeful; Size = 23uy } }
       { Char = char 184; Code = { Value = 0x000fffeaul; Size = 20uy } }
       { Char = char 185; Code = { Value = 0x003fffe2ul; Size = 22uy } }
       { Char = char 186; Code = { Value = 0x003fffe3ul; Size = 22uy } }
       { Char = char 187; Code = { Value = 0x003fffe4ul; Size = 22uy } }
       { Char = char 188; Code = { Value = 0x007ffff0ul; Size = 23uy } }
       { Char = char 189; Code = { Value = 0x003fffe5ul; Size = 22uy } }
       { Char = char 190; Code = { Value = 0x003fffe6ul; Size = 22uy } }
       { Char = char 191; Code = { Value = 0x007ffff1ul; Size = 23uy } }
       { Char = char 192; Code = { Value = 0x03ffffe0ul; Size = 26uy } }
       { Char = char 193; Code = { Value = 0x03ffffe1ul; Size = 26uy } }
       { Char = char 194; Code = { Value = 0x000fffebul; Size = 20uy } }
       { Char = char 195; Code = { Value = 0x0007fff1ul; Size = 19uy } }
       { Char = char 196; Code = { Value = 0x003fffe7ul; Size = 22uy } }
       { Char = char 197; Code = { Value = 0x007ffff2ul; Size = 23uy } }
       { Char = char 198; Code = { Value = 0x003fffe8ul; Size = 22uy } }
       { Char = char 199; Code = { Value = 0x01ffffecul; Size = 25uy } }
       { Char = char 200; Code = { Value = 0x03ffffe2ul; Size = 26uy } }
       { Char = char 201; Code = { Value = 0x03ffffe3ul; Size = 26uy } }
       { Char = char 202; Code = { Value = 0x03ffffe4ul; Size = 26uy } }
       { Char = char 203; Code = { Value = 0x07ffffdeul; Size = 27uy } }
       { Char = char 204; Code = { Value = 0x07ffffdful; Size = 27uy } }
       { Char = char 205; Code = { Value = 0x03ffffe5ul; Size = 26uy } }
       { Char = char 206; Code = { Value = 0x00fffff1ul; Size = 24uy } }
       { Char = char 207; Code = { Value = 0x01ffffedul; Size = 25uy } }
       { Char = char 208; Code = { Value = 0x0007fff2ul; Size = 19uy } }
       { Char = char 209; Code = { Value = 0x001fffe3ul; Size = 21uy } }
       { Char = char 210; Code = { Value = 0x03ffffe6ul; Size = 26uy } }
       { Char = char 211; Code = { Value = 0x07ffffe0ul; Size = 27uy } }
       { Char = char 212; Code = { Value = 0x07ffffe1ul; Size = 27uy } }
       { Char = char 213; Code = { Value = 0x03ffffe7ul; Size = 26uy } }
       { Char = char 214; Code = { Value = 0x07ffffe2ul; Size = 27uy } }
       { Char = char 215; Code = { Value = 0x00fffff2ul; Size = 24uy } }
       { Char = char 216; Code = { Value = 0x001fffe4ul; Size = 21uy } }
       { Char = char 217; Code = { Value = 0x001fffe5ul; Size = 21uy } }
       { Char = char 218; Code = { Value = 0x03ffffe8ul; Size = 26uy } }
       { Char = char 219; Code = { Value = 0x03ffffe9ul; Size = 26uy } }
       { Char = char 220; Code = { Value = 0x0ffffffdul; Size = 28uy } }
       { Char = char 221; Code = { Value = 0x07ffffe3ul; Size = 27uy } }
       { Char = char 222; Code = { Value = 0x07ffffe4ul; Size = 27uy } }
       { Char = char 223; Code = { Value = 0x07ffffe5ul; Size = 27uy } }
       { Char = char 224; Code = { Value = 0x000fffecul; Size = 20uy } }
       { Char = char 225; Code = { Value = 0x00fffff3ul; Size = 24uy } }
       { Char = char 226; Code = { Value = 0x000fffedul; Size = 20uy } }
       { Char = char 227; Code = { Value = 0x001fffe6ul; Size = 21uy } }
       { Char = char 228; Code = { Value = 0x003fffe9ul; Size = 22uy } }
       { Char = char 229; Code = { Value = 0x001fffe7ul; Size = 21uy } }
       { Char = char 230; Code = { Value = 0x001fffe8ul; Size = 21uy } }
       { Char = char 231; Code = { Value = 0x007ffff3ul; Size = 23uy } }
       { Char = char 232; Code = { Value = 0x003fffeaul; Size = 22uy } }
       { Char = char 233; Code = { Value = 0x003fffebul; Size = 22uy } }
       { Char = char 234; Code = { Value = 0x01ffffeeul; Size = 25uy } }
       { Char = char 235; Code = { Value = 0x01ffffeful; Size = 25uy } }
       { Char = char 236; Code = { Value = 0x00fffff4ul; Size = 24uy } }
       { Char = char 237; Code = { Value = 0x00fffff5ul; Size = 24uy } }
       { Char = char 238; Code = { Value = 0x03ffffeaul; Size = 26uy } }
       { Char = char 239; Code = { Value = 0x007ffff4ul; Size = 23uy } }
       { Char = char 240; Code = { Value = 0x03ffffebul; Size = 26uy } }
       { Char = char 241; Code = { Value = 0x07ffffe6ul; Size = 27uy } }
       { Char = char 242; Code = { Value = 0x03ffffecul; Size = 26uy } }
       { Char = char 243; Code = { Value = 0x03ffffedul; Size = 26uy } }
       { Char = char 244; Code = { Value = 0x07ffffe7ul; Size = 27uy } }
       { Char = char 245; Code = { Value = 0x07ffffe8ul; Size = 27uy } }
       { Char = char 246; Code = { Value = 0x07ffffe9ul; Size = 27uy } }
       { Char = char 247; Code = { Value = 0x07ffffeaul; Size = 27uy } }
       { Char = char 248; Code = { Value = 0x07ffffebul; Size = 27uy } }
       { Char = char 249; Code = { Value = 0x0ffffffeul; Size = 28uy } }
       { Char = char 250; Code = { Value = 0x07ffffecul; Size = 27uy } }
       { Char = char 251; Code = { Value = 0x07ffffedul; Size = 27uy } }
       { Char = char 252; Code = { Value = 0x07ffffeeul; Size = 27uy } }
       { Char = char 253; Code = { Value = 0x07ffffeful; Size = 27uy } }
       { Char = char 254; Code = { Value = 0x07fffff0ul; Size = 27uy } }
       { Char = char 255; Code = { Value = 0x03ffffeeul; Size = 26uy } }
       { Char = char 256; Code = { Value = 0x3ffffffful; Size = 30uy } } |]

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
    | Leaf of char
    | Node of Path: uint32 * Left: Tree * Right: Tree

let private empty = Leaf(char 0xffffff)

let inline private chose zero code mask (left, right) =
    if (code &&& mask) = zero then left else right

let inline private chose8 code mask opts = chose 0uy code mask opts

let inline private chose32 code mask opts = chose 0ul code mask opts

[<TailCall>]
let rec private insertAt (loc: Code) code mask tree wrap : Tree =
    if mask = 0ul then
        Leaf(code.Char) |> wrap
    else
        let path, lt, rt =
            match tree with
            | Node(path, left, right) -> path, left, right
            | Leaf _ -> (alignLeft loc).Value, empty, empty

        let ll = { Value = loc.Value <<< 1; Size = loc.Size + 1uy }
        let rl = { Value = 1ul + (loc.Value <<< 1); Size = loc.Size + 1uy }

        let inline insertLt lt' = Node(path, lt', rt)
        let inline insertRt rt' = Node(path, lt, rt')

        let tree', loc', cont =
            ((lt, ll, insertLt), (rt, rl, insertRt)) |> chose32 code.Code.Value mask

        insertAt loc' code (mask >>> 1) tree' (cont >> wrap)

let inline private insertRoot tree code =
    let mask = 1ul <<< (int code.Code.Size - 1) in insertAt { Value = 0ul; Size = 0uy } code mask tree id

/// Huffman tree used for decoding a character.
let Root = (empty, Table) ||> Array.fold insertRoot

/// Decode a character from code aligned to most significant bit.
let decodeChar msb =
    let rec loop mask tree =
        match tree with
        | Leaf code -> code
        | Node(_, l, r) -> chose32 msb mask (l, r) |> loop(mask >>> 1)

    loop 0x80000000ul Root

/// Result of making up to 8 steps down the tree. Can either be
/// a character code located at the leaf node along with bit-mask pointing to the next element in octet,
/// or an intermediate node with its path.
[<Struct>]
type Milestone =
    | Final of Code: char * Mask: byte
    | Inter of Path: uint32 * Tree: Tree

/// Make up to 8 steps down the Huffman tree along the given path starting from masked position.
[<TailCall>]
let rec walk path mask tree =
    match tree, mask with
    | Leaf char, mask -> Final(char, mask)
    | Node(msb, _, _) as node, 0uy -> Inter(msb, node)
    | Node(_, left, right), mask -> chose8 path mask (left, right) |> walk path (mask >>> 1)

/// Confirm padding correctness of the last encoded octet.
let inline checkPadding pad a =
    match pad with
    | 0xfe00_0000ul
    | 0xfc00_0000ul
    | 0xf800_0000ul
    | 0xf000_0000ul
    | 0xe000_0000ul
    | 0xc000_0000ul
    | 0x8000_0000ul -> Ok a
    | p when p > 0xfe00_0000ul -> Error $"Padding length exceeds 7 bits: %B{p}"
    | p -> Error $"Invalid padding: %B{p}"

/// Decode complete octet sequence to string using static Huffman code.
let decodeStr (octets: byte array) =
    let last = octets.Length - 1

    let rec loop tree mask i acc =
        match walk octets[i] mask tree with
        | Final(char, 0uy) when i = last -> Ok(char :: acc)
        | Final(char, 0uy) -> char :: acc |> loop Root 128uy (i + 1)
        | Final(char, msk) -> char :: acc |> loop Root msk i
        | Inter(path, _) when i = last -> checkPadding path acc
        | Inter(_, tree) -> loop tree 128uy (i + 1) acc

    [] |> loop Root 128uy 0 |> Result.map(List.rev >> List.toArray >> String)

/// Encode a single extended ASCII character or EOS.
let inline encodeChar (char: char) = Table[int char]

let rec private encodeStrLoop (str: string) (buff: byte Span) i j (stack, size) =
    if size >= 8uy then
        buff[j] <- byte(stack >>> 56)
        encodeStrLoop str buff i (j + 1) (stack <<< 8, size - 8uy)
    elif i < str.Length then
        let { Code = code } = encodeChar str[i]
        let stack' = uint64 code.Value <<< 64 - int(code.Size + size) ||| stack
        encodeStrLoop str buff (i + 1) j (stack', size + code.Size)
    else
        buff[j] <- 0xffuy >>> int size ||| byte(stack >>> 56)
        j + 1

/// Encode a sequence of extended ASCII characters.
let encodeStr (str: string) (buf: byte Span) = encodeStrLoop str buf 0 0 (0UL, 0uy)
