// Copyright 2016 Serge Slipchenko (Serge.Slipchenko@gmail.com)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Semagle.Numerics.Vectors.IO.Tests
open System
open System.IO
open NUnit.Framework
open FsUnit
open Semagle.Numerics.Vectors
open Semagle.Numerics.Vectors.IO

[<TestFixture>]
type ``LibSVM tests``() = 

    let makeTmpData =
        let tmp = Path.GetTempFileName() + ".data"

        use w = new StreamWriter(tmp)
        fprintfn w "1 1:1.0 3:2.0 7:5.0"
        fprintfn w "1 1:3.0 2:2.0 9:5.0"
        fprintfn w "-1 4:0.1 8:0.5"

        tmp

    let expectedData = 
        [(+1.0f, SparseVector([|0; 2; 6|], [|1.0f; 2.0f; 5.0f|])); 
         (+1.0f, SparseVector([|0; 1; 8|], [|3.0f; 2.0f; 5.0f|]));
         (-1.0f, SparseVector([|3; 7|], [|0.1f; 0.5f|]))] |> List.toSeq

    [<Test>]
    member x.``Test LibSVM file read.``() =
        let tmp = makeTmpData
        try 
            LibSVM.read tmp |> should equal expectedData
        finally
            if File.Exists tmp then File.Delete tmp

    [<Test>]
    member x.``Test LibSVM file write.``() =
        let tmp = Path.GetTempFileName() + ".data"
        try
            LibSVM.write tmp expectedData
            LibSVM.read tmp |> should equal expectedData
        finally
            if File.Exists tmp then File.Delete tmp
