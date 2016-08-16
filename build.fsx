#r "packages/build/FAKE/tools/FakeLib.dll"

open Fake.FscHelper
open Fake
open System.IO

let buildDir = "./build/"

MSBuildDefaults <- { MSBuildDefaults with Verbosity = Some MSBuildVerbosity.Quiet } 

Target "Clean" (fun _ -> CleanDir buildDir)

Target "TestAll" (fun _ -> 
    !!(buildDir + "*.Tests.dll") 
    |> NUnit (fun p ->
        {p with
            DisableShadowCopy = true;
            OutputFile = buildDir + "TestResults.xml" }))

Target "BuildLibraries" (fun _ -> 
    !!"Semagle.*/*.fsproj"
    |> MSBuildRelease buildDir "Build"
    |> Log "LibrariesBuild-Output: ")

Target "BuildTests" (fun _ -> 
    !!"Semagle.*/tests/*.fsproj"
    |> MSBuildRelease buildDir "Build"
    |> Log "TestsBuild-Output: ")

Target "BuildSamples" ( fun _ ->
    !!"Semagle.*/samples/*.fsx"
    |> Seq.map (fun (s : string) ->
        let output = buildDir + (s.[s.LastIndexOfAny([| '/'; '\\' |])+1..s.Length - 4] + "exe")
        let references = 
            seq {
                use r = new StreamReader(s)
                while not r.EndOfStream do
                    let line = r.ReadLine()
                    if line.StartsWith ("#r") then
                        yield (Path.GetFullPath buildDir) + line.[line.IndexOf("\"") + 1..line.LastIndexOf("\"")-1]} 
            |> Seq.toList
        compileFiles [s] (List.append [ "--standalone"; "--target:exe"; "--out:" + output; "--lib:" + buildDir] 
            (List.map (fun r -> "--reference:" + r) references)) |> ignore
        output)
    |> Log "Sample: "
)

Target "BuildAll" (fun _ -> ())

"Clean" ==> "BuildLibraries"

"BuildLibraries" ==> "BuildTests"
"BuildLibraries" ==> "BuildSamples"

"BuildTests" ==> "TestAll"

"TestAll" ==> "BuildAll"
"BuildSamples" ==> "BuildAll" 

RunTargetOrDefault "TestAll"
