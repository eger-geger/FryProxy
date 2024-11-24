module FryProxy.Http.Hpack.Huffman

open System
open Microsoft.FSharp.Core

/// Path and depth within Huffman tree.
[<Struct>]
type Location = { LSB: uint32; Len: uint8 }

[<Struct>]
type CharCode = { Char: char; Loc: Location }

/// Align (shift left) node location to most significant bit.
let toMSB { LSB = lsb; Len = len } = lsb <<< (32 - int len)

/// Table of static Huffman codes for each ASCII symbol + EOS.
let Table =
    [ { Char = char 000; Loc = { LSB = 0x00001ff8ul; Len = 13uy } }
      { Char = char 001; Loc = { LSB = 0x007fffd8ul; Len = 23uy } }
      { Char = char 002; Loc = { LSB = 0x0fffffe2ul; Len = 28uy } }
      { Char = char 003; Loc = { LSB = 0x0fffffe3ul; Len = 28uy } }
      { Char = char 004; Loc = { LSB = 0x0fffffe4ul; Len = 28uy } }
      { Char = char 005; Loc = { LSB = 0x0fffffe5ul; Len = 28uy } }
      { Char = char 006; Loc = { LSB = 0x0fffffe6ul; Len = 28uy } }
      { Char = char 007; Loc = { LSB = 0x0fffffe7ul; Len = 28uy } }
      { Char = char 008; Loc = { LSB = 0x0fffffe8ul; Len = 28uy } }
      { Char = char 009; Loc = { LSB = 0x00ffffeaul; Len = 24uy } }
      { Char = char 010; Loc = { LSB = 0x3ffffffcul; Len = 30uy } }
      { Char = char 011; Loc = { LSB = 0x0fffffe9ul; Len = 28uy } }
      { Char = char 012; Loc = { LSB = 0x0fffffeaul; Len = 28uy } }
      { Char = char 013; Loc = { LSB = 0x3ffffffdul; Len = 30uy } }
      { Char = char 014; Loc = { LSB = 0x0fffffebul; Len = 28uy } }
      { Char = char 015; Loc = { LSB = 0x0fffffecul; Len = 28uy } }
      { Char = char 016; Loc = { LSB = 0x0fffffedul; Len = 28uy } }
      { Char = char 017; Loc = { LSB = 0x0fffffeeul; Len = 28uy } }
      { Char = char 018; Loc = { LSB = 0x0fffffeful; Len = 28uy } }
      { Char = char 019; Loc = { LSB = 0x0ffffff0ul; Len = 28uy } }
      { Char = char 020; Loc = { LSB = 0x0ffffff1ul; Len = 28uy } }
      { Char = char 021; Loc = { LSB = 0x0ffffff2ul; Len = 28uy } }
      { Char = char 022; Loc = { LSB = 0x3ffffffeul; Len = 30uy } }
      { Char = char 023; Loc = { LSB = 0x0ffffff3ul; Len = 28uy } }
      { Char = char 024; Loc = { LSB = 0x0ffffff4ul; Len = 28uy } }
      { Char = char 025; Loc = { LSB = 0x0ffffff5ul; Len = 28uy } }
      { Char = char 026; Loc = { LSB = 0x0ffffff6ul; Len = 28uy } }
      { Char = char 027; Loc = { LSB = 0x0ffffff7ul; Len = 28uy } }
      { Char = char 028; Loc = { LSB = 0x0ffffff8ul; Len = 28uy } }
      { Char = char 029; Loc = { LSB = 0x0ffffff9ul; Len = 28uy } }
      { Char = char 030; Loc = { LSB = 0x0ffffffaul; Len = 28uy } }
      { Char = char 031; Loc = { LSB = 0x0ffffffbul; Len = 28uy } }
      { Char = char 032; Loc = { LSB = 0x00000014ul; Len = 6uy } }
      { Char = char 033; Loc = { LSB = 0x000003f8ul; Len = 10uy } }
      { Char = char 034; Loc = { LSB = 0x000003f9ul; Len = 10uy } }
      { Char = char 035; Loc = { LSB = 0x00000ffaul; Len = 12uy } }
      { Char = char 036; Loc = { LSB = 0x00001ff9ul; Len = 13uy } }
      { Char = char 037; Loc = { LSB = 0x00000015ul; Len = 6uy } }
      { Char = char 038; Loc = { LSB = 0x000000f8ul; Len = 8uy } }
      { Char = char 039; Loc = { LSB = 0x000007faul; Len = 11uy } }
      { Char = char 040; Loc = { LSB = 0x000003faul; Len = 10uy } }
      { Char = char 041; Loc = { LSB = 0x000003fbul; Len = 10uy } }
      { Char = char 042; Loc = { LSB = 0x000000f9ul; Len = 8uy } }
      { Char = char 043; Loc = { LSB = 0x000007fbul; Len = 11uy } }
      { Char = char 044; Loc = { LSB = 0x000000faul; Len = 8uy } }
      { Char = char 045; Loc = { LSB = 0x00000016ul; Len = 6uy } }
      { Char = char 046; Loc = { LSB = 0x00000017ul; Len = 6uy } }
      { Char = char 047; Loc = { LSB = 0x00000018ul; Len = 6uy } }
      { Char = char 048; Loc = { LSB = 0x00000000ul; Len = 5uy } }
      { Char = char 049; Loc = { LSB = 0x00000001ul; Len = 5uy } }
      { Char = char 050; Loc = { LSB = 0x00000002ul; Len = 5uy } }
      { Char = char 051; Loc = { LSB = 0x00000019ul; Len = 6uy } }
      { Char = char 052; Loc = { LSB = 0x0000001aul; Len = 6uy } }
      { Char = char 053; Loc = { LSB = 0x0000001bul; Len = 6uy } }
      { Char = char 054; Loc = { LSB = 0x0000001cul; Len = 6uy } }
      { Char = char 055; Loc = { LSB = 0x0000001dul; Len = 6uy } }
      { Char = char 056; Loc = { LSB = 0x0000001eul; Len = 6uy } }
      { Char = char 057; Loc = { LSB = 0x0000001ful; Len = 6uy } }
      { Char = char 058; Loc = { LSB = 0x0000005cul; Len = 7uy } }
      { Char = char 059; Loc = { LSB = 0x000000fbul; Len = 8uy } }
      { Char = char 060; Loc = { LSB = 0x00007ffcul; Len = 15uy } }
      { Char = char 061; Loc = { LSB = 0x00000020ul; Len = 6uy } }
      { Char = char 062; Loc = { LSB = 0x00000ffbul; Len = 12uy } }
      { Char = char 063; Loc = { LSB = 0x000003fcul; Len = 10uy } }
      { Char = char 064; Loc = { LSB = 0x00001ffaul; Len = 13uy } }
      { Char = char 065; Loc = { LSB = 0x00000021ul; Len = 6uy } }
      { Char = char 066; Loc = { LSB = 0x0000005dul; Len = 7uy } }
      { Char = char 067; Loc = { LSB = 0x0000005eul; Len = 7uy } }
      { Char = char 068; Loc = { LSB = 0x0000005ful; Len = 7uy } }
      { Char = char 069; Loc = { LSB = 0x00000060ul; Len = 7uy } }
      { Char = char 070; Loc = { LSB = 0x00000061ul; Len = 7uy } }
      { Char = char 071; Loc = { LSB = 0x00000062ul; Len = 7uy } }
      { Char = char 072; Loc = { LSB = 0x00000063ul; Len = 7uy } }
      { Char = char 073; Loc = { LSB = 0x00000064ul; Len = 7uy } }
      { Char = char 074; Loc = { LSB = 0x00000065ul; Len = 7uy } }
      { Char = char 075; Loc = { LSB = 0x00000066ul; Len = 7uy } }
      { Char = char 076; Loc = { LSB = 0x00000067ul; Len = 7uy } }
      { Char = char 077; Loc = { LSB = 0x00000068ul; Len = 7uy } }
      { Char = char 078; Loc = { LSB = 0x00000069ul; Len = 7uy } }
      { Char = char 079; Loc = { LSB = 0x0000006aul; Len = 7uy } }
      { Char = char 080; Loc = { LSB = 0x0000006bul; Len = 7uy } }
      { Char = char 081; Loc = { LSB = 0x0000006cul; Len = 7uy } }
      { Char = char 082; Loc = { LSB = 0x0000006dul; Len = 7uy } }
      { Char = char 083; Loc = { LSB = 0x0000006eul; Len = 7uy } }
      { Char = char 084; Loc = { LSB = 0x0000006ful; Len = 7uy } }
      { Char = char 085; Loc = { LSB = 0x00000070ul; Len = 7uy } }
      { Char = char 086; Loc = { LSB = 0x00000071ul; Len = 7uy } }
      { Char = char 087; Loc = { LSB = 0x00000072ul; Len = 7uy } }
      { Char = char 088; Loc = { LSB = 0x000000fcul; Len = 8uy } }
      { Char = char 089; Loc = { LSB = 0x00000073ul; Len = 7uy } }
      { Char = char 090; Loc = { LSB = 0x000000fdul; Len = 8uy } }
      { Char = char 091; Loc = { LSB = 0x00001ffbul; Len = 13uy } }
      { Char = char 092; Loc = { LSB = 0x0007fff0ul; Len = 19uy } }
      { Char = char 093; Loc = { LSB = 0x00001ffcul; Len = 13uy } }
      { Char = char 094; Loc = { LSB = 0x00003ffcul; Len = 14uy } }
      { Char = char 095; Loc = { LSB = 0x00000022ul; Len = 6uy } }
      { Char = char 096; Loc = { LSB = 0x00007ffdul; Len = 15uy } }
      { Char = char 097; Loc = { LSB = 0x00000003ul; Len = 5uy } }
      { Char = char 098; Loc = { LSB = 0x00000023ul; Len = 6uy } }
      { Char = char 099; Loc = { LSB = 0x00000004ul; Len = 5uy } }
      { Char = char 100; Loc = { LSB = 0x00000024ul; Len = 6uy } }
      { Char = char 101; Loc = { LSB = 0x00000005ul; Len = 5uy } }
      { Char = char 102; Loc = { LSB = 0x00000025ul; Len = 6uy } }
      { Char = char 103; Loc = { LSB = 0x00000026ul; Len = 6uy } }
      { Char = char 104; Loc = { LSB = 0x00000027ul; Len = 6uy } }
      { Char = char 105; Loc = { LSB = 0x00000006ul; Len = 5uy } }
      { Char = char 106; Loc = { LSB = 0x00000074ul; Len = 7uy } }
      { Char = char 107; Loc = { LSB = 0x00000075ul; Len = 7uy } }
      { Char = char 108; Loc = { LSB = 0x00000028ul; Len = 6uy } }
      { Char = char 109; Loc = { LSB = 0x00000029ul; Len = 6uy } }
      { Char = char 110; Loc = { LSB = 0x0000002aul; Len = 6uy } }
      { Char = char 111; Loc = { LSB = 0x00000007ul; Len = 5uy } }
      { Char = char 112; Loc = { LSB = 0x0000002bul; Len = 6uy } }
      { Char = char 113; Loc = { LSB = 0x00000076ul; Len = 7uy } }
      { Char = char 114; Loc = { LSB = 0x0000002cul; Len = 6uy } }
      { Char = char 115; Loc = { LSB = 0x00000008ul; Len = 5uy } }
      { Char = char 116; Loc = { LSB = 0x00000009ul; Len = 5uy } }
      { Char = char 117; Loc = { LSB = 0x0000002dul; Len = 6uy } }
      { Char = char 118; Loc = { LSB = 0x00000077ul; Len = 7uy } }
      { Char = char 119; Loc = { LSB = 0x00000078ul; Len = 7uy } }
      { Char = char 120; Loc = { LSB = 0x00000079ul; Len = 7uy } }
      { Char = char 121; Loc = { LSB = 0x0000007aul; Len = 7uy } }
      { Char = char 122; Loc = { LSB = 0x0000007bul; Len = 7uy } }
      { Char = char 123; Loc = { LSB = 0x00007ffeul; Len = 15uy } }
      { Char = char 124; Loc = { LSB = 0x000007fcul; Len = 11uy } }
      { Char = char 125; Loc = { LSB = 0x00003ffdul; Len = 14uy } }
      { Char = char 126; Loc = { LSB = 0x00001ffdul; Len = 13uy } }
      { Char = char 127; Loc = { LSB = 0x0ffffffcul; Len = 28uy } }
      { Char = char 128; Loc = { LSB = 0x000fffe6ul; Len = 20uy } }
      { Char = char 129; Loc = { LSB = 0x003fffd2ul; Len = 22uy } }
      { Char = char 130; Loc = { LSB = 0x000fffe7ul; Len = 20uy } }
      { Char = char 131; Loc = { LSB = 0x000fffe8ul; Len = 20uy } }
      { Char = char 132; Loc = { LSB = 0x003fffd3ul; Len = 22uy } }
      { Char = char 133; Loc = { LSB = 0x003fffd4ul; Len = 22uy } }
      { Char = char 134; Loc = { LSB = 0x003fffd5ul; Len = 22uy } }
      { Char = char 135; Loc = { LSB = 0x007fffd9ul; Len = 23uy } }
      { Char = char 136; Loc = { LSB = 0x003fffd6ul; Len = 22uy } }
      { Char = char 137; Loc = { LSB = 0x007fffdaul; Len = 23uy } }
      { Char = char 138; Loc = { LSB = 0x007fffdbul; Len = 23uy } }
      { Char = char 139; Loc = { LSB = 0x007fffdcul; Len = 23uy } }
      { Char = char 140; Loc = { LSB = 0x007fffddul; Len = 23uy } }
      { Char = char 141; Loc = { LSB = 0x007fffdeul; Len = 23uy } }
      { Char = char 142; Loc = { LSB = 0x00ffffebul; Len = 24uy } }
      { Char = char 143; Loc = { LSB = 0x007fffdful; Len = 23uy } }
      { Char = char 144; Loc = { LSB = 0x00ffffecul; Len = 24uy } }
      { Char = char 145; Loc = { LSB = 0x00ffffedul; Len = 24uy } }
      { Char = char 146; Loc = { LSB = 0x003fffd7ul; Len = 22uy } }
      { Char = char 147; Loc = { LSB = 0x007fffe0ul; Len = 23uy } }
      { Char = char 148; Loc = { LSB = 0x00ffffeeul; Len = 24uy } }
      { Char = char 149; Loc = { LSB = 0x007fffe1ul; Len = 23uy } }
      { Char = char 150; Loc = { LSB = 0x007fffe2ul; Len = 23uy } }
      { Char = char 151; Loc = { LSB = 0x007fffe3ul; Len = 23uy } }
      { Char = char 152; Loc = { LSB = 0x007fffe4ul; Len = 23uy } }
      { Char = char 153; Loc = { LSB = 0x001fffdcul; Len = 21uy } }
      { Char = char 154; Loc = { LSB = 0x003fffd8ul; Len = 22uy } }
      { Char = char 155; Loc = { LSB = 0x007fffe5ul; Len = 23uy } }
      { Char = char 156; Loc = { LSB = 0x003fffd9ul; Len = 22uy } }
      { Char = char 157; Loc = { LSB = 0x007fffe6ul; Len = 23uy } }
      { Char = char 158; Loc = { LSB = 0x007fffe7ul; Len = 23uy } }
      { Char = char 159; Loc = { LSB = 0x00ffffeful; Len = 24uy } }
      { Char = char 160; Loc = { LSB = 0x003fffdaul; Len = 22uy } }
      { Char = char 161; Loc = { LSB = 0x001fffddul; Len = 21uy } }
      { Char = char 162; Loc = { LSB = 0x000fffe9ul; Len = 20uy } }
      { Char = char 163; Loc = { LSB = 0x003fffdbul; Len = 22uy } }
      { Char = char 164; Loc = { LSB = 0x003fffdcul; Len = 22uy } }
      { Char = char 165; Loc = { LSB = 0x007fffe8ul; Len = 23uy } }
      { Char = char 166; Loc = { LSB = 0x007fffe9ul; Len = 23uy } }
      { Char = char 167; Loc = { LSB = 0x001fffdeul; Len = 21uy } }
      { Char = char 168; Loc = { LSB = 0x007fffeaul; Len = 23uy } }
      { Char = char 169; Loc = { LSB = 0x003fffddul; Len = 22uy } }
      { Char = char 170; Loc = { LSB = 0x003fffdeul; Len = 22uy } }
      { Char = char 171; Loc = { LSB = 0x00fffff0ul; Len = 24uy } }
      { Char = char 172; Loc = { LSB = 0x001fffdful; Len = 21uy } }
      { Char = char 173; Loc = { LSB = 0x003fffdful; Len = 22uy } }
      { Char = char 174; Loc = { LSB = 0x007fffebul; Len = 23uy } }
      { Char = char 175; Loc = { LSB = 0x007fffecul; Len = 23uy } }
      { Char = char 176; Loc = { LSB = 0x001fffe0ul; Len = 21uy } }
      { Char = char 177; Loc = { LSB = 0x001fffe1ul; Len = 21uy } }
      { Char = char 178; Loc = { LSB = 0x003fffe0ul; Len = 22uy } }
      { Char = char 179; Loc = { LSB = 0x001fffe2ul; Len = 21uy } }
      { Char = char 180; Loc = { LSB = 0x007fffedul; Len = 23uy } }
      { Char = char 181; Loc = { LSB = 0x003fffe1ul; Len = 22uy } }
      { Char = char 182; Loc = { LSB = 0x007fffeeul; Len = 23uy } }
      { Char = char 183; Loc = { LSB = 0x007fffeful; Len = 23uy } }
      { Char = char 184; Loc = { LSB = 0x000fffeaul; Len = 20uy } }
      { Char = char 185; Loc = { LSB = 0x003fffe2ul; Len = 22uy } }
      { Char = char 186; Loc = { LSB = 0x003fffe3ul; Len = 22uy } }
      { Char = char 187; Loc = { LSB = 0x003fffe4ul; Len = 22uy } }
      { Char = char 188; Loc = { LSB = 0x007ffff0ul; Len = 23uy } }
      { Char = char 189; Loc = { LSB = 0x003fffe5ul; Len = 22uy } }
      { Char = char 190; Loc = { LSB = 0x003fffe6ul; Len = 22uy } }
      { Char = char 191; Loc = { LSB = 0x007ffff1ul; Len = 23uy } }
      { Char = char 192; Loc = { LSB = 0x03ffffe0ul; Len = 26uy } }
      { Char = char 193; Loc = { LSB = 0x03ffffe1ul; Len = 26uy } }
      { Char = char 194; Loc = { LSB = 0x000fffebul; Len = 20uy } }
      { Char = char 195; Loc = { LSB = 0x0007fff1ul; Len = 19uy } }
      { Char = char 196; Loc = { LSB = 0x003fffe7ul; Len = 22uy } }
      { Char = char 197; Loc = { LSB = 0x007ffff2ul; Len = 23uy } }
      { Char = char 198; Loc = { LSB = 0x003fffe8ul; Len = 22uy } }
      { Char = char 199; Loc = { LSB = 0x01ffffecul; Len = 25uy } }
      { Char = char 200; Loc = { LSB = 0x03ffffe2ul; Len = 26uy } }
      { Char = char 201; Loc = { LSB = 0x03ffffe3ul; Len = 26uy } }
      { Char = char 202; Loc = { LSB = 0x03ffffe4ul; Len = 26uy } }
      { Char = char 203; Loc = { LSB = 0x07ffffdeul; Len = 27uy } }
      { Char = char 204; Loc = { LSB = 0x07ffffdful; Len = 27uy } }
      { Char = char 205; Loc = { LSB = 0x03ffffe5ul; Len = 26uy } }
      { Char = char 206; Loc = { LSB = 0x00fffff1ul; Len = 24uy } }
      { Char = char 207; Loc = { LSB = 0x01ffffedul; Len = 25uy } }
      { Char = char 208; Loc = { LSB = 0x0007fff2ul; Len = 19uy } }
      { Char = char 209; Loc = { LSB = 0x001fffe3ul; Len = 21uy } }
      { Char = char 210; Loc = { LSB = 0x03ffffe6ul; Len = 26uy } }
      { Char = char 211; Loc = { LSB = 0x07ffffe0ul; Len = 27uy } }
      { Char = char 212; Loc = { LSB = 0x07ffffe1ul; Len = 27uy } }
      { Char = char 213; Loc = { LSB = 0x03ffffe7ul; Len = 26uy } }
      { Char = char 214; Loc = { LSB = 0x07ffffe2ul; Len = 27uy } }
      { Char = char 215; Loc = { LSB = 0x00fffff2ul; Len = 24uy } }
      { Char = char 216; Loc = { LSB = 0x001fffe4ul; Len = 21uy } }
      { Char = char 217; Loc = { LSB = 0x001fffe5ul; Len = 21uy } }
      { Char = char 218; Loc = { LSB = 0x03ffffe8ul; Len = 26uy } }
      { Char = char 219; Loc = { LSB = 0x03ffffe9ul; Len = 26uy } }
      { Char = char 220; Loc = { LSB = 0x0ffffffdul; Len = 28uy } }
      { Char = char 221; Loc = { LSB = 0x07ffffe3ul; Len = 27uy } }
      { Char = char 222; Loc = { LSB = 0x07ffffe4ul; Len = 27uy } }
      { Char = char 223; Loc = { LSB = 0x07ffffe5ul; Len = 27uy } }
      { Char = char 224; Loc = { LSB = 0x000fffecul; Len = 20uy } }
      { Char = char 225; Loc = { LSB = 0x00fffff3ul; Len = 24uy } }
      { Char = char 226; Loc = { LSB = 0x000fffedul; Len = 20uy } }
      { Char = char 227; Loc = { LSB = 0x001fffe6ul; Len = 21uy } }
      { Char = char 228; Loc = { LSB = 0x003fffe9ul; Len = 22uy } }
      { Char = char 229; Loc = { LSB = 0x001fffe7ul; Len = 21uy } }
      { Char = char 230; Loc = { LSB = 0x001fffe8ul; Len = 21uy } }
      { Char = char 231; Loc = { LSB = 0x007ffff3ul; Len = 23uy } }
      { Char = char 232; Loc = { LSB = 0x003fffeaul; Len = 22uy } }
      { Char = char 233; Loc = { LSB = 0x003fffebul; Len = 22uy } }
      { Char = char 234; Loc = { LSB = 0x01ffffeeul; Len = 25uy } }
      { Char = char 235; Loc = { LSB = 0x01ffffeful; Len = 25uy } }
      { Char = char 236; Loc = { LSB = 0x00fffff4ul; Len = 24uy } }
      { Char = char 237; Loc = { LSB = 0x00fffff5ul; Len = 24uy } }
      { Char = char 238; Loc = { LSB = 0x03ffffeaul; Len = 26uy } }
      { Char = char 239; Loc = { LSB = 0x007ffff4ul; Len = 23uy } }
      { Char = char 240; Loc = { LSB = 0x03ffffebul; Len = 26uy } }
      { Char = char 241; Loc = { LSB = 0x07ffffe6ul; Len = 27uy } }
      { Char = char 242; Loc = { LSB = 0x03ffffecul; Len = 26uy } }
      { Char = char 243; Loc = { LSB = 0x03ffffedul; Len = 26uy } }
      { Char = char 244; Loc = { LSB = 0x07ffffe7ul; Len = 27uy } }
      { Char = char 245; Loc = { LSB = 0x07ffffe8ul; Len = 27uy } }
      { Char = char 246; Loc = { LSB = 0x07ffffe9ul; Len = 27uy } }
      { Char = char 247; Loc = { LSB = 0x07ffffeaul; Len = 27uy } }
      { Char = char 248; Loc = { LSB = 0x07ffffebul; Len = 27uy } }
      { Char = char 249; Loc = { LSB = 0x0ffffffeul; Len = 28uy } }
      { Char = char 250; Loc = { LSB = 0x07ffffecul; Len = 27uy } }
      { Char = char 251; Loc = { LSB = 0x07ffffedul; Len = 27uy } }
      { Char = char 252; Loc = { LSB = 0x07ffffeeul; Len = 27uy } }
      { Char = char 253; Loc = { LSB = 0x07ffffeful; Len = 27uy } }
      { Char = char 254; Loc = { LSB = 0x07fffff0ul; Len = 27uy } }
      { Char = char 255; Loc = { LSB = 0x03ffffeeul; Len = 26uy } }
      { Char = char 256; Loc = { LSB = 0x3ffffffful; Len = 30uy } } ]

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
    | Node of Path: uint32 * Left: Tree * Right: Tree

let private empty =
    Leaf({ Char = char 0xffffff; Loc = { LSB = 0xffffffu; Len = 0xffuy } })

let inline private chose zero code mask (left, right) =
    if (code &&& mask) = zero then left else right

let inline private chose8 code mask opts = chose 0uy code mask opts

let inline private chose32 code mask opts = chose 0ul code mask opts

[<TailCall>]
let rec private insertAt (loc: Location) code mask tree wrap : Tree =
    if mask = 0ul then
        Leaf(code) |> wrap
    else
        let path, lt, rt =
            match tree with
            | Node(path, left, right) -> path, left, right
            | Leaf _ -> loc.LSB <<< (32 - int loc.Len), empty, empty

        let ll = { LSB = loc.LSB <<< 1; Len = loc.Len + 1uy }
        let rl = { LSB = 1ul + (loc.LSB <<< 1); Len = loc.Len + 1uy }

        let inline insertLt lt' = Node(path, lt', rt)
        let inline insertRt rt' = Node(path, lt, rt')

        let tree', loc', cont =
            ((lt, ll, insertLt), (rt, rl, insertRt)) |> chose32 code.Loc.LSB mask

        insertAt loc' code (mask >>> 1) tree' (cont >> wrap)

let inline private insertRoot tree code =
    let mask = 1ul <<< (int code.Loc.Len - 1) in insertAt { LSB = 0ul; Len = 0uy } code mask tree id

/// Huffman tree used for decoding a character.
let Root = (empty, Table) ||> List.fold insertRoot

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
    | Leaf code, mask -> Final(code.Char, mask)
    | Node(msb, _, _) as node, 0uy -> Inter(msb, node)
    | Node(_, left, right), mask -> chose8 path mask (left, right) |> walk path (mask >>> 1)

/// Confirm padding correctness of the last encoded octet.
let inline checkPadding pad a =
    match pad with
    | 0xfe000000ul
    | 0xfc000000ul
    | 0xf8000000ul
    | 0xf0000000ul
    | 0xe0000000ul
    | 0xc0000000ul
    | 0x80000000ul -> Ok a
    | p when p > 0xfe000000ul -> Error $"Padding length exceeds 7 bits: %B{p}"
    | p -> Error $"Invalid padding: %B{p}"

/// Decode complete octet sequence to string using static Huffman code.
let decodeStr (octets: byte array) =
    let lastOctet = octets.Length - 1

    let rec loop tree mask i acc =
        match walk octets[i] mask tree with
        | Final(char, 0uy) when i = lastOctet -> Ok(char :: acc)
        | Final(char, 0uy) -> char :: acc |> loop Root 128uy (i + 1)
        | Final(char, msk) -> char :: acc |> loop Root msk i
        | Inter(path, _) when i = lastOctet -> checkPadding path acc
        | Inter(_, tree) -> loop tree 128uy (i + 1) acc

    [] |> loop Root 128uy 0 |> Result.map(List.rev >> List.toArray >> String)

/// Encode ASCII character or EOS.
let inline encodeChar (char: char) = Table[int char]
