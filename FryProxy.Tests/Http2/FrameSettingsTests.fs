module FryProxy.Tests.Http2.FrameSettingsTests

open FryProxy.Http2
open FryProxy.Http2.Frames
open FsUnit
open NUnit.Framework

let settingsCases =
    let tc name (settings: Setting list) (expectedFrame: Frame) =
        TestCaseData(settings, expectedFrame).SetName(name)

    let setting2 =
        [ { Type = SettingType.INITIAL_WINDOW_SIZE; Value = 65535u }
          { Type = SettingType.MAX_FRAME_SIZE; Value = 16384u } ]

    [ tc "empty settings list" ([]: Setting list)
      <| { Header = { Length = 0u; Flags = 0uy; Type = FrameType.SETTINGS; StreamId = 0u }
           Body = Settings { Settings = [] } }

      tc "two settings" setting2
      <| { Header = { Length = 12u; Flags = 0uy; Type = FrameType.SETTINGS; StreamId = 0u }
           Body = Settings { Settings = setting2 } } ]

[<TestCaseSource(nameof settingsCases)>]
let testSettings (settings: Setting list) (expectedFrame: Frame) =
    let frame = Frame.settings settings
    frame |> should equal expectedFrame
