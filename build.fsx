// Copyright 2020 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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

#r "paket: groupref build //"
#load "./.fake/build.fsx/intellisense.fsx"
open Fake.Core
open Fake.DotNet
open Fake.IO
open Fake.IO.FileSystemOperators
open Fake.IO.Globbing.Operators
open Fake.Core.TargetOperators
open System.IO

Target.initEnvironment ()

Target.create "Clean" (fun _ ->
    !! "**/bin"
    ++ "**/obj"
    |> Shell.cleanDirs 
)

Target.create "Patch" (fun _ ->
    Shell.replaceInFiles
        [ "namespace Logary.Facade", "namespace Semagle.Logging" ]
        [ "paket-files/logary/logary/src/Logary.Facade/Facade.fs" ]
)

let setBuildOptions (defaults : DotNet.BuildOptions) = {
    defaults with 
        MSBuildParams = { 
            defaults.MSBuildParams with
                Verbosity = Some(Quiet)
        }
}

Target.create "BuildLibraries" (fun _ ->
    !! "**/*.*proj"
    -- "**/*.Tests.*proj"
    |> Seq.iter (DotNet.build setBuildOptions)
)

Target.create "BuildTests" (fun _ ->
    !! "**/*.Tests.*proj"
    |> Seq.iter (DotNet.build setBuildOptions)
)

Target.create "Build" ignore

Target.create "Publish" (fun _ -> 
    let buildDir = __SOURCE_DIRECTORY__ @@ "build"
    Shell.cleanDir buildDir

    !! "**/*.*proj"
    -- "**/*.Tests.*proj"
    |> Seq.iter (DotNet.publish (fun defaults -> {
        defaults with 
            OutputPath = Some(buildDir)
    }))
)

Target.create "BuildSamples" (fun _ -> 
    let buildDir = __SOURCE_DIRECTORY__ @@ "build"

    !!"**/samples/*.fsx"
    |> Seq.iter (fun s -> 
        let output = buildDir @@ (s.[s.LastIndexOfAny([| '/'; '\\' |])+1..s.Length - 4] + "exe")
        let references = 
            seq {
                use r = new StreamReader(s)
                while not r.EndOfStream do
                    let line = r.ReadLine()
                    if line.StartsWith ("#r") then
                        yield buildDir @@ (line.[line.IndexOf("\"") + 1..line.LastIndexOf("\"")-1])} 
            |> Seq.toList

        [s]
        |> Fsc.compileExternal "fsharpc"
            ([Fsc.Target Fsc.Exe; Fsc.TargetProfile Fsc.Netcore; Fsc.Out output; Fsc.Lib [buildDir]] 
                @ (List.map Fsc.Reference references))
    )
)

Target.create "Test" (fun _ -> 
  !! "**/*.Tests.*proj"
  |> Seq.iter (DotNet.test id)
)

let documentation = {|
    ProjectInfo =
      [ "project-name", "Semagle.Framework"
        "project-author", "Serge Slipchenko"
        "project-summary", "Semagle: F# Framework for Machine Learning and Natural Language Processing"
        "project-github", "https://github.com/semagle/semagle-framework"
        "project-nuget", "" ]

    SourceDirectory = __SOURCE_DIRECTORY__ @@ "Documentation"
    OutputDirectory = __SOURCE_DIRECTORY__ @@ "Documentation"
    LayoutRoots = [
        __SOURCE_DIRECTORY__ @@ "Documentation/templates"
        __SOURCE_DIRECTORY__ @@ "packages/build/FSharp.Formatting/templates"
        __SOURCE_DIRECTORY__ @@ "packages/build/FSharp.Formatting/templates/reference";
    ]
    DocumentTemplate = "docpage.cshtml"

#if RELEASE
    WebsiteRoot = "/semagle-framework"
#else
    WebsiteRoot = "file://" + __SOURCE_DIRECTORY__ @@ "Documentation"
#endif
|}

let cleanHelp =
    DirectoryInfo.getSubDirectories (DirectoryInfo.ofPath documentation.SourceDirectory)
    |> Array.filter (fun d -> d.Name.Contains("Semagle."))
    |> Seq.iter (fun d -> Shell.rm_rf d.FullName)

    !! (documentation.SourceDirectory @@ "**/*.html")
    -- (documentation.SourceDirectory @@ "**/reference/*.html")
    |> (Seq.iter Shell.rm)

Target.create "CleanDocumentation" (fun _ ->
    Shell.rm_rf (documentation.OutputDirectory @@ "reference")
    cleanHelp
)

Target.create "GenerateReference" (fun _ ->
    let referenceDir = documentation.OutputDirectory @@ "reference" 
    Directory.ensure referenceDir
    Shell.cleanDir referenceDir
    
    !! "**/*.*proj"
    -- "**/*.Tests.*proj"
    |> Seq.map (fun projectFile -> 
        let projectDir = Path.getDirectory(projectFile)
        let projectName = DirectoryInfo.ofPath(projectDir).Name
        projectDir @@ "bin" @@ "Release" @@ "netstandard2.0" @@ (projectName + ".dll"))
    |> FSFormatting.createDocsForDlls (fun args -> {
        args with 
            OutputDirectory = referenceDir
            LayoutRoots = documentation.LayoutRoots 
            ProjectParameters = ("root", documentation.WebsiteRoot)::documentation.ProjectInfo
            SourceRepository = 
                (snd (List.find (fun (p, _) -> p = "project-github") documentation.ProjectInfo))
                @@ "tree/master"
    })
)

Target.create "GenerateHelp" (fun _ ->
    cleanHelp

    // generate project documentaion
    FSFormatting.createDocs (fun args -> {
        args with
            Source = documentation.SourceDirectory
            OutputDirectory = documentation.OutputDirectory
            LayoutRoots = documentation.LayoutRoots
            ProjectParameters = ("root", documentation.WebsiteRoot)::documentation.ProjectInfo
            Template = documentation.DocumentTemplate
    })

    // generate library documentaion
    !! "**/doc"
    |> Seq.iter (fun docDir -> 
        let projectDir = Path.getDirectory(docDir)
        let projectName = DirectoryInfo.ofPath(projectDir).Name

        FSFormatting.createDocs (fun args -> {
            args with
                Source = docDir
                OutputDirectory = documentation.OutputDirectory @@ projectName
                LayoutRoots = documentation.LayoutRoots
                ProjectParameters = ("root", documentation.WebsiteRoot)::documentation.ProjectInfo
                Template = documentation.DocumentTemplate
        })  
    )
)

Target.create "BuildDocumentation" ignore

Target.create "All" ignore

"BuildLibraries" 
    ==> "GenerateReference" 
    ==> "BuildDocumentation"
"GenerateHelp" ==> "BuildDocumentation"

"BuildLibraries" ==> "Build"
"BuildTests" ==> "Build"
"BuildSamples" ==> "Build"
"BuildDocumentation" ==> "Build"

"BuildLibraries" 
    ==> "Publish" 
    ==> "BuildSamples"

"Clean"
  ==> "Patch"
  ==> "BuildLibraries"
  ==> "BuildTests"
  ==> "Test"
  ==> "BuildSamples"
  ==> "BuildDocumentation"
  ==> "All"

Target.runOrDefault "All"
