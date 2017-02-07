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

namespace Semagle.MachineLearning.Metrics.Tests
open System
open NUnit.Framework

open System
open NUnit.Framework
open FsUnit
open Semagle.MachineLearning.Metrics

[<TestFixture>]
type ``Accuracy tests``() = 
    [<Test>]
    member test.``Accuracy should be zero.``() =
        let expected = [| true; false; true; false; |]
        let predicted = [| false; true; false; true; |]

        Classification.accuracy expected predicted |> should equal 0.0f

    [<Test>]
    member test.``Accuracy should be one.``() =
        let expected = [| true; false; true; false; |]
        let predicted = [| true; false; true; false; |]

        Classification.accuracy expected predicted |> should equal 1.0f

    [<Test>]
    member test.``Accuracy should be 1/4.``() =
        let expected = [| true; false; true; false; |]
        let predicted = [| true; true; false; true; |]

        Classification.accuracy expected predicted |> should equal 0.25f
