// Copyright 2017 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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

namespace Semagle.Algorithms.Tests
open System
open NUnit.Framework
open FsUnit
open Semagle.Algorithms

[<TestFixture>]
type ``Viterbi algorithm tests``() = 

    // Data from https://en.wikipedia.org/wiki/Viterbi_algorithm

    let p_start = [| (* healthy *) 0.6f; (* fever *) 0.4f |]

    let p_transition = 
        [| 
            (* healthy *) [| (* healthy *) 0.7f; (* fever *) 0.3f |]; 
            (* fever *)   [| (* healthy *) 0.4f; (* fever *) 0.6f |] 
        |]

    let p_emission = 
        [|
            (* healthy *) [| (* normal *) 0.5f; (* cold *) 0.4f; (* dizzy *) 0.1f |];
            (* fever *)   [| (* normal *) 0.1f; (* cold *) 0.3f; (* dizzy *) 0.6f |]
        |]

    let observations = [| (* normal *) 0; (* cold *) 1; (* dizzy *) 2 |]

    [<Test>]
    member x.``Decoding should be correct``() =
        let s, p = Sequence.viterbi (Array.length observations) (Array.length p_start) 
                                    (fun i ->
                                        (fun k -> 
                                            (fun k' -> 
                                                let log_p_emission = log p_emission.[k].[observations.[i]]
                                                if k' = -1 then
                                                    log_p_emission + (log p_start.[k])
                                                else 
                                                    log_p_emission + (log p_transition.[k'].[k]))))
        
        [| (* healthy *) 0; (* healthy *) 0; (* fever *) 1 |] |> should equal <| s
        0.01512 |> should (equalWithin 0.00001f) <| (exp p)
