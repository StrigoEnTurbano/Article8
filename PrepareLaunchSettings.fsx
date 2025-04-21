#r "nuget: Thoth.Json.Net"
#r "nuget: Hopac, 0.5.0"

let inline (^) f x = f x

let godotVersion = "4.4.1"

// Godot:
// - Engines: // Папка с различными версиями godot.
//   - Godot_v{godotVersion}-stable_mono_win64
//     - Godot_v{godotVersion}-stable_mono_win64.exe
// - Projects:
//  - ProjectName
//    - ProjectName.sln
//    - PrepareLaunchSettings.fsx
//    - src
//      - ProjectName : // = godotProjectDirectory
//        - ProjectName.csproj
//        - Properties: // Изначально может отсутствовать.
//          - launchSettings.json
//      - ProjectName.Core: 
//        - ProjectName.Core.fsproj

let godotProjectDirectory =
    System.IO.Directory.EnumerateFiles(
        __SOURCE_DIRECTORY__
        , "project.godot"
        , System.IO.SearchOption.AllDirectories
    )
    |> Seq.exactlyOne
    |> System.IO.Path.GetDirectoryName

[<RequireQualifiedAccess>]
type Run =
    | Editor
    | Game
    | Scene of Path : string
    | CustomScene of PublicName : string * Path : string * Args : string list

    with
    member this.CommandLineArgs = [
        "--path"
        "."

        let runOptions = [
            //"--debug-collisions"
            //"--debug-paths"
            //"--debug-navigation"
            //"--debug-avoidance"
            //"--debug-canvas-item-redraw"
            ]

        "--verbose"

        match this with
        | Run.Editor ->
            "--editor"
            //"--rendering-engine"
            //"opengl3"
        | Run.Game ->
            yield! runOptions
        | Run.Scene path ->
            path
            yield! runOptions
        | Run.CustomScene (_, path, args) ->
            path
            yield! runOptions
            // Важно, что после кастомные аргументы идут последними.
            "--"
            yield! args
    ]
    member this.Name =
        match this with
        | Run.Editor -> "Godot Editor"
        | Run.Game -> "Godot Game"
        | Run.Scene path -> $"Godot %s{path}"
        | Run.CustomScene (name, _, _) -> $"%s{name}"


let pathToGodotExe =
    System.IO.Path.Combine(
        godotProjectDirectory // Godot/Projects/ProjectName/src/ProjectName.App
        , "../../../.." // Godot
        , $"Engines/Godot_v{godotVersion}-stable_mono_win64/Godot_v{godotVersion}-stable_mono_win64.exe"
    )
    |> System.IO.Path.GetFullPath

if not ^ System.IO.File.Exists pathToGodotExe then
    failwith $"{pathToGodotExe} not found."

open Thoth.Json.Net

[
    Run.Editor
    Run.Game

    let scenes =
        System.IO.Directory.EnumerateFiles(
            godotProjectDirectory
            , "*.tscn"
            , System.IO.SearchOption.AllDirectories
        )
    for fullPath in scenes do
        System.IO.Path.GetRelativePath(godotProjectDirectory, fullPath)
        |> Run.Scene
    // CustomScene добавляйте сюда.
]
|> Seq.map ^ fun p ->
    p.Name
    , Encode.object [
        "commandName", Encode.string "Executable"
        "executablePath", Encode.string pathToGodotExe
        "commandLineArgs", Encode.string ^ String.concat " " p.CommandLineArgs
        "workingDirectory", Encode.string godotProjectDirectory
    ]
|> Seq.append [
    System.IO.Directory.EnumerateFiles(godotProjectDirectory, "*.csproj")
    |> Seq.exactlyOne
    |> System.IO.Path.GetFileNameWithoutExtension
    , Encode.object [
        "commandName", Encode.string "Project"
    ]
]
|> fun profiles ->
    Encode.object [
        "profiles"
        , Encode.object ^ List.ofSeq profiles
    ]
|> Encode.toString 2
|> fun content ->
    let propertiesDir = 
        System.IO.Path.Combine(
            godotProjectDirectory
            , "Properties"
        )
    if not ^ System.IO.Directory.Exists propertiesDir then
        System.IO.Directory.CreateDirectory propertiesDir
        |> ignore
    System.IO.File.WriteAllText(
        System.IO.Path.Combine(
            propertiesDir
            , "launchSettings.json"
        )
        , content
    )