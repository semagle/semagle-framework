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

#r "../packages/build/FAKE/tools/FakeLib.dll"
#load "../packages/build/FSharp.Formatting/FSharp.Formatting.fsx"

open System
open System.IO
open Fake
open Fake.FileHelper
open FSharp.Literate
open FSharp.Markdown
open FSharp.MetadataFormat

let projectInfo =
  [ "project-name", "Semagle.Framework"
    "project-author", "Serge Slipchenko"
    "project-summary", "Semagle: F# Framework for Machine Learning and Natural Language Processing"
    "project-github", "https://github.com/sslipchenko/semagle-framework"
    "project-nuget", "" ]

#if RELEASE
let root = "/semagle-framework"
#else
let root = "file://" + __SOURCE_DIRECTORY__
#endif

let formatting  = __SOURCE_DIRECTORY__ @@ "../packages/build/FSharp.Formatting/"
let templates = __SOURCE_DIRECTORY__ @@ "templates"
let template = formatting @@ "templates/docpage.cshtml"

let layoutRoots = [ templates; formatting @@ "templates"; formatting @@ "templates/reference"]

let buildDocumentation() =
    // generate project documentaion
    !! (__SOURCE_DIRECTORY__ @@ "**/*.md")
    |> Seq.iter (fun file ->
        Literate.ProcessMarkdown(file, template, replacements = ("root", root)::projectInfo, layoutRoots = layoutRoots))

    // generate library documentaion
    !! (__SOURCE_DIRECTORY__ @@ "../Semagle.*/doc")
    |> Seq.iter (fun inputDir ->
        let libraryName = Path.GetFileName(Path.GetDirectoryName(inputDir))
        let outputDir = __SOURCE_DIRECTORY__ @@ libraryName
        CleanDir outputDir
        Literate.ProcessDirectory(inputDir, template, outputDir, 
            replacements = ("root", root)::("library-name", libraryName)::projectInfo, 
            layoutRoots = layoutRoots)
    )

let buildReference() =
    let outputDir = __SOURCE_DIRECTORY__ @@ "reference"
    CleanDir outputDir
    let binaries = 
        !! (__SOURCE_DIRECTORY__ @@ "../Semagle.*/doc")
        |> Seq.map (fun docDir -> 
            let libraryName = Path.GetFileName(Path.GetDirectoryName(docDir))
            __SOURCE_DIRECTORY__ @@ "../build" @@ (libraryName + ".dll"))
        |> Seq.toList

    MetadataFormat.Generate (binaries, outputDir, layoutRoots, parameters = ("root", root)::projectInfo,
        sourceRepo = (List.find (fst >> ((=) "project-github")) projectInfo |> snd) @@ "tree/master",
        sourceFolder = (__SOURCE_DIRECTORY__ @@ ".."), publicOnly = true)

#if HELP
buildDocumentation ()
#endif

#if REFERENCE
buildReference()
#endif
