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
open Fake.Tools.Git
open System.IO

let configuration = Environment.environVarOrDefault "configuration" "Release"

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
        Configuration = DotNet.Custom configuration
        MSBuildParams = {
            defaults.MSBuildParams with
                Verbosity = Some(Quiet)
                Properties = [
                    "Optimize", "True"
                    "DebugSymbols", "True"
                    "Configuration", configuration
                ]
        }
}

Target.create "BuildLibraries" (fun _ ->
    !! "**/*.*proj"
    -- "**/samples/*.*proj"
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
            Configuration = DotNet.Custom configuration
            OutputPath = Some(buildDir)
    }))
)

Target.create "BuildSamples" (fun _ ->
    !! "**/samples/*.*proj"
    |> Seq.iter (DotNet.build setBuildOptions)
)

Target.create "Test" (fun _ ->
  !! "**/*.Tests.*proj"
  |> Seq.iter (DotNet.test (fun defaults -> {
      defaults with
        Configuration = DotNet.Custom configuration
  }))
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

    WebsiteRoot =
        if configuration = "Release" then
            "/semagle-framework"
        else
            "file://" + __SOURCE_DIRECTORY__ @@ "Documentation"
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

        !! (docDir @@ "*.png")
        |> Seq.iter (fun file -> Shell.copyFile (documentation.OutputDirectory @@ projectName) file)
    )
)

Target.create "BuildDocumentation" ignore

Target.create "ReleaseDocumentation" (fun _ ->
    let url = CommandHelper.runSimpleGitCommand __SOURCE_DIRECTORY__ "remote get-url origin"
    let ghPages = __SOURCE_DIRECTORY__ @@ "build" @@ "gh-pages"
    Shell.cleanDir ghPages

    Repository.cloneSingleBranch "" url "gh-pages" ghPages

    Directory.GetDirectories(ghPages)
    |> Seq.filter (fun dir -> Path.GetFileName(dir) <> ".git")
    |> Seq.iter (fun dir -> Directory.Delete(dir, true))
    Directory.GetFiles(ghPages) |> Seq.iter File.Delete

    !! "Documentation/**/*.html" ++ "Documentation/**/*.css" ++ "Documentation/**/*.js" ++ "Documentation/**/*.png"
    |> Seq.iter (fun file -> Shell.copyFileWithSubfolder "Documentation" ghPages file)

    Staging.stageAll ghPages
    Commit.exec ghPages (sprintf "Update generated documentation")
    Branches.push ghPages
)

Target.create "All" ignore

"Patch" ==> "BuildLibraries"

"BuildLibraries"
    ==> "GenerateReference"
    ==> "BuildDocumentation"
"GenerateHelp" ==> "BuildDocumentation"
"BuildDocumentation" ==> "ReleaseDocumentation"

"BuildLibraries" ==> "Build"
"BuildTests" ==> "Build"
"BuildSamples" ==> "Build"
"BuildDocumentation" ==> "Build"

"BuildLibraries"
    ==> "BuildSamples"
    ==> "Publish"

"Clean"
  ==> "Build"
  ==> "Test"
  ==> "Publish"
  ==> "All"

Target.runOrDefault "All"
