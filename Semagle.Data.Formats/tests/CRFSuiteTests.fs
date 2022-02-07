// Copyright 2022 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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

namespace Semagle.Data.Formats.Tests

open FsUnit
open NUnit.Framework
open Semagle.Data.Formats
open Semagle.Numerics.Vectors
open System.IO

[<TestFixture>]
type ``CRFSuite tests``() =
    let chunking = "B-NP	w[0]=Confidence	w[1]=in\n\
                    B-PP	w[-1]=Confidence	w[0]=in	w[1]=the\n\
                    B-NP	w[-1]=in	w[0]=the	w[1]=pound\n\
                    I-NP	w[-1]=the	w[0]=pound	w[1]=is\n\
                    B-VP	w[-1]=pound	w[0]=is	w[1]=widely\n\
                    I-VP	w[-1]=is	w[0]=widely	w[1]=expected\n\
                    I-VP	w[-1]=widely	w[0]=expected	w[1]=to\n\
                    I-VP	w[-1]=expected	w[0]=to	w[1]=take\n\
                    I-VP	w[-1]=to	w[0]=take	w[1]=another\n\
                    B-NP	w[-1]=take	w[0]=another	w[1]=sharp\n\
                    I-NP	w[-1]=another	w[0]=sharp	w[1]=dive\n\
                    I-NP	w[-1]=sharp	w[0]=dive	w[1]=if\n\
                    \n\
                    B-NP	w[0]=Rockwell	w[1]=said\n\
                    B-VP	w[-1]=Rockwell	w[0]=said	w[1]=the\n\
                    B-NP	w[-1]=said	w[0]=the	w[1]=agreement\n\
                    I-NP	w[-1]=the	w[0]=agreement	w[1]=calls\n\
                    B-VP	w[-1]=agreement	w[0]=calls	w[1]=for\n\
                    B-SBAR	w[-1]=calls	w[0]=for	w[1]=it\n\
                    B-NP	w[-1]=for	w[0]=it	w[1]=to\n\
                    \n\
                    B-NP	w[0]=Rockwell	w[1]=said\n\
                    B-VP	w[-1]=Rockwell	w[0]=said	w[1]=the\n\
                    B-NP	w[-1]=said	w[0]=the	w[1]=agreement\n\
                    I-NP	w[-1]=the	w[0]=agreement	w[1]=calls\n\
                    B-VP	w[-1]=agreement	w[0]=calls	w[1]=for\n\
                    B-SBAR	w[-1]=calls	w[0]=for	w[1]=it\n\
                    B-NP	w[-1]=for	w[0]=it	w[1]=to\n"


    let makeTmpData () =
        let tmp = Path.GetTempFileName() + ".data"

        use w = new StreamWriter(tmp)
        w.Write(chunking)
        tmp

    [<Test>]
    member x.``Test CRFSuite file read.``() =
        let tmp = makeTmpData ()
        try
            let Y, X =
                CRFSuite.read tmp
                |> Seq.toArray
                |> Array.unzip

            Y |> should equal [|
                [| "B-NP"; "B-PP"; "B-NP"; "I-NP"; "B-VP";
                   "I-VP"; "I-VP"; "I-VP"; "I-VP"; "B-NP";
                   "I-NP"; "I-NP" |]
                [| "B-NP"; "B-VP"; "B-NP"; "I-NP"; "B-VP";
                   "B-SBAR"; "B-NP" |]
                [| "B-NP"; "B-VP"; "B-NP"; "I-NP"; "B-VP";
                   "B-SBAR"; "B-NP" |]
            |]

            X |> should equal [|
                [|
                    [| "w[0]=Confidence"; "w[1]=in" |]
                    [| "w[-1]=Confidence"; "w[0]=in"; "w[1]=the" |]
                    [| "w[-1]=in"; "w[0]=the"; "w[1]=pound" |]
                    [| "w[-1]=the"; "w[0]=pound"; "w[1]=is" |]
                    [| "w[-1]=pound"; "w[0]=is"; "w[1]=widely" |]
                    [| "w[-1]=is"; "w[0]=widely"; "w[1]=expected" |]
                    [| "w[-1]=widely"; "w[0]=expected"; "w[1]=to" |]
                    [| "w[-1]=expected"; "w[0]=to"; "w[1]=take" |]
                    [| "w[-1]=to"; "w[0]=take"; "w[1]=another" |]
                    [| "w[-1]=take"; "w[0]=another"; "w[1]=sharp" |]
                    [| "w[-1]=another"; "w[0]=sharp"; "w[1]=dive" |]
                    [| "w[-1]=sharp"; "w[0]=dive"; "w[1]=if" |]
                |]
                [|
                    [| "w[0]=Rockwell"; "w[1]=said" |]
                    [| "w[-1]=Rockwell"; "w[0]=said"; "w[1]=the" |]
                    [| "w[-1]=said"; "w[0]=the"; "w[1]=agreement" |]
                    [| "w[-1]=the"; "w[0]=agreement"; "w[1]=calls" |]
                    [| "w[-1]=agreement"; "w[0]=calls"; "w[1]=for" |]
                    [| "w[-1]=calls"; "w[0]=for"; "w[1]=it" |]
                    [| "w[-1]=for"; "w[0]=it"; "w[1]=to" |]
                |]
                [|
                    [| "w[0]=Rockwell"; "w[1]=said" |]
                    [| "w[-1]=Rockwell"; "w[0]=said"; "w[1]=the" |]
                    [| "w[-1]=said"; "w[0]=the"; "w[1]=agreement" |]
                    [| "w[-1]=the"; "w[0]=agreement"; "w[1]=calls" |]
                    [| "w[-1]=agreement"; "w[0]=calls"; "w[1]=for" |]
                    [| "w[-1]=calls"; "w[0]=for"; "w[1]=it" |]
                    [| "w[-1]=for"; "w[0]=it"; "w[1]=to" |]
                |]
            |]
        finally
            if File.Exists tmp then File.Delete tmp

    [<Test>]
     member x.``Test CRFSuite dictionary.``() =
        let tmp = makeTmpData ()
        try
            let X = CRFSuite.read tmp |> Seq.map snd
            let dictionary = CRFSuite.buildFeatureDictionary X
            dictionary.Count |> should equal 51
        finally
            if File.Exists tmp then File.Delete tmp

    [<Test>]
    member x.``Test CRFSuite vectorize.``() =
        let tmp = makeTmpData ()
        try
            let X = CRFSuite.read tmp |> Seq.map snd |> Seq.toArray
            let dictionary = CRFSuite.buildFeatureDictionary X

            CRFSuite.vectorizeFeatures dictionary X.[0].[0]
            |> should equal <| SparseVector([| 0; 1 |], [| 1.0f; 1.0f |])

            CRFSuite.vectorizeFeatures dictionary X.[0].[1]
            |> should equal <| SparseVector([| 2; 3; 4 |], [| 1.0f; 1.0f; 1.0f |])

            CRFSuite.vectorizeSequence dictionary X.[1]
            |> should equal <| CRFSuite.vectorizeSequence dictionary X.[2]
        finally
            if File.Exists tmp then File.Delete tmp


