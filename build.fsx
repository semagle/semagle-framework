// Copyright 2016 Serge Slipchenko (Serge.Slipchenko@gmail.com)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

#r "packages/build/FAKE/tools/FakeLib.dll"

open Fake.FscHelper
open Fake
open Fake.Git
open System
open System.IO

let buildDir = "./build/"

MSBuildDefaults <- { MSBuildDefaults with Verbosity = Some MSBuildVerbosity.Quiet } 

Target "Clean" (fun _ -> CleanDir buildDir)

Target "TestAll" (fun _ -> 
    !!(buildDir + "*.Tests.dll") 
    |> NUnit (fun p ->
        {p with
            DisableShadowCopy = true;
            OutputFile = "TestResults.xml";
            WorkingDir = buildDir }))

Target "LoggingFile" (fun _ -> 
    ReplaceInFiles [ "namespace Logary.Facade", "namespace Semagle.Logging" ]
                   [ "paket-files/logary/logary/src/Logary.Facade/Facade.fs" ])

Target "BuildLibraries" (fun _ -> 
    !!"Semagle.*/*.fsproj"
    |> MSBuildRelease buildDir "Build" 
    |> Log "LibrariesBuild-Output: ")

Target "BuildTests" (fun _ -> 
    !!"Semagle.*/tests/*.fsproj"
    |> MSBuildRelease buildDir "Build"
    |> Log "TestsBuild-Output: ")

Target "BuildSamples" ( fun _ ->
    // fix System.Reflection.Metadata issue
    [|  @"packages/build/System.Reflection.Metadata/lib/portable-net45+win8/System.Reflection.Metadata.dll";
        @"packages/build/System.Reflection.Metadata/lib/portable-net45+win8/System.Reflection.Metadata.xml" |] 
    |> FileHelper.Copy @"packages/build/FAKE/tools"

    // copy FSharp.Core development files
    !!"packages/FSharp.Core/lib/net45/FSharp.Core.optdata" |> Copy buildDir
    !!"packages/FSharp.Core/lib/net45/FSharp.Core.sigdata" |> Copy buildDir

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
        compileFiles [s] (List.append [ "--target:exe"; "--out:" + output; "--lib:" + buildDir] 
            (List.map (fun r -> "--reference:" + r) references)) |> ignore
        output)
    |> Log "Sample: "
)

// Generate the documentation
let fakePath = "packages" </> "build" </> "FAKE" </> "tools" </> "FAKE.exe"
let fakeStartInfo script workingDirectory args fsiargs environmentVars =
    (fun (info: System.Diagnostics.ProcessStartInfo) ->
        info.FileName <- System.IO.Path.GetFullPath fakePath
        info.Arguments <- sprintf "%s --fsiargs -d:FAKE %s \"%s\"" args fsiargs script
        info.WorkingDirectory <- workingDirectory
        let setVar k v =
            info.EnvironmentVariables.[k] <- v
        for (k, v) in environmentVars do
            setVar k v
        setVar "MSBuild" msBuildExe
        setVar "GIT" Git.CommandHelper.gitPath
        setVar "FSI" fsiPath)

/// Run the given buildscript with FAKE.exe
let executeFAKEWithOutput workingDirectory script fsiargs envArgs =
    let exitCode =
        ExecProcessWithLambdas
            (fakeStartInfo script workingDirectory "" fsiargs envArgs)
            TimeSpan.MaxValue false ignore ignore
    System.Threading.Thread.Sleep 1000
    exitCode

// Documentation
let buildDocumentationTarget fsiargs target =
    trace (sprintf "Building documentation (%s), this could take some time, please wait..." target)
    let exit = executeFAKEWithOutput "Documentation" "generate.fsx" fsiargs ["target", target]
    if exit <> 0 then
        failwith "generating reference documentation failed"
    ()  

Target "GenerateReference" (fun _ ->
    buildDocumentationTarget "-d:RELEASE -d:REFERENCE" "Default"
)      

Target "GenerateHelp" (fun _ ->
    buildDocumentationTarget "-d:RELEASE -d:HELP" "Default"
)

Target "BuildDocumentation" DoNothing

Target "ReleaseDocumentation" (fun _ ->
    let url = CommandHelper.runSimpleGitCommand __SOURCE_DIRECTORY__ "remote get-url origin"
    let ghPages = buildDir + "gh-pages"
    CleanDir ghPages
    Repository.cloneSingleBranch "" url "gh-pages" ghPages

    Directory.GetDirectories(ghPages) 
    |> Seq.filter (fun dir -> Path.GetFileName(dir) <> ".git") 
    |> Seq.iter (fun dir -> Directory.Delete(dir, true))
    Directory.GetFiles(ghPages) |> Seq.iter File.Delete

    !! "Documentation/**/*.html" ++ "Documentation/**/*.css" ++ "Documentation/**/*.js" ++ "Documentation/**/*.png"
    |> Seq.iter (fun file -> CopyFileWithSubfolder "Documentation" ghPages file)

    StageAll ghPages
    Git.Commit.Commit ghPages (sprintf "Update generated documentation")
    Branches.push ghPages
)

Target "BuildAll" DoNothing

"Clean" ==> "BuildLibraries"
"LoggingFile" ==> "BuildLibraries"

"BuildLibraries" ==> "BuildTests"
"BuildLibraries" ==> "BuildSamples"

"BuildTests" ==> "TestAll"

"BuildLibraries" ==> "GenerateReference"
"GenerateHelp" ==> "BuildDocumentation"
"GenerateReference" ==> "BuildDocumentation"

"TestAll" ==> "BuildAll"
"BuildDocumentation" ==> "BuildAll"
"BuildSamples" ==> "BuildAll" 

"BuildDocumentation" ==> "ReleaseDocumentation"

RunTargetOrDefault "TestAll"
