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
let root = "/Semagle.Framework"
#else
let root = "file://" + __SOURCE_DIRECTORY__
#endif

let formatting  = __SOURCE_DIRECTORY__ @@ "../packages/build/FSharp.Formatting/"
let templates = __SOURCE_DIRECTORY__ @@ "templates"
let template = formatting @@ "templates/docpage.cshtml"

let layoutRoots = [ templates; formatting @@ "templates"; formatting @@ "templates/reference"]

let buildDocumentation() =
    !! (__SOURCE_DIRECTORY__ @@ "../Semagle.*/doc")
    |> Seq.iter (fun inputDir ->
        let libraryName = Path.GetFileName(Path.GetDirectoryName(inputDir))
        let outputDir = __SOURCE_DIRECTORY__ @@ libraryName
        Directory.Delete(outputDir, true)
        Directory.CreateDirectory(outputDir) |> ignore
        Literate.ProcessDirectory(inputDir, template, outputDir, 
            replacements = ("root", root + "/" + libraryName)::("library-name", libraryName)::projectInfo, 
            layoutRoots = layoutRoots)
    )

    for file in Directory.EnumerateFiles(__SOURCE_DIRECTORY__, "*.md", SearchOption.AllDirectories) do
        Literate.ProcessMarkdown(file, template, replacements = ("root", root)::projectInfo, layoutRoots = layoutRoots)

buildDocumentation ()
