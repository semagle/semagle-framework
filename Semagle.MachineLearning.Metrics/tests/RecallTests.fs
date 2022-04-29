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

namespace Semagle.MachineLearning.Metrics.Tests

open NUnit.Framework
open FsUnit
open Semagle.MachineLearning.Metrics

[<TestFixture>]
type ``Recall tests``() =
    [<Test>]
    member _.``Recall should be zero.``() =
        let expected = [| |]
        let predicted = [| |]

        Classification.recall expected predicted
        |> should equal Map.empty

        let expected = [| "A"; "B"; "C" |]
        let predicted = [| "D"; "D"; "D" |]

        Classification.recall expected predicted
        |> should equal (Map.ofArray [| ("A", 0.0); ("B", 0.0); ("C", 0.0) |])

    [<Test>]
    member _.``Recall should be one.``() =
        let expected = [| "A"; "B"; "C"; "A"; "B"; "C" |]
        let predicted = [| "A"; "B"; "C"; "A"; "B"; "C" |]

        Classification.recall expected predicted
        |> should equal (Map.ofArray [| ("A", 1.0); ("B", 1.0); ("C", 1.0) |])

    [<Test>]
    member _.``Recall should be correct.``() =
        let expected = [| "A"; "B"; "C"; "A"; "B"; "C" |]
        let predicted = [| "C"; "B"; "C"; "C"; "A"; "C" |]

        Classification.recall expected predicted
        |> should equal (Map.ofArray [| ("A", 0.0); ("B", 0.5); ("C", 1.0) |])
