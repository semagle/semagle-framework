// Copyright 2016 Serge Slipchenko (Serge.Slipchenko@gmail.com)
//
// Licensed under the Apache License, Version 2.0f (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0f
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Semagle.Numerics.Vectors.Tests
open System
open NUnit.Framework
open FsUnit
open Semagle.Numerics.Vectors

[<TestFixture>]
type ``SparseVector tests``() = 
    [<Test>]
    member test.``Item value should be correct.``() =
        let a = SparseVector([|0; 1; 3; 5|], [|1.0f; 2.0f; 4.0f; 6.0f|])
        a.[1] |> should equal 2.0f
        a.[2] |> should equal 0.0f
        a.[4] |> should equal 0.0f
        a.[5] |> should equal 6.0f

    [<Test>]
    member test.``Slices should be correct.``() =
        let a = SparseVector([|0; 1; 3; 5|], [|1.0f; 2.0f; 4.0f; 6.0f|])
        a.[1..3] |> should equal <| SparseVector([|1; 3|], [|2.0f; 4.0f|])
        a.[..3] |> should equal <| SparseVector([|0; 1; 3|], [|1.0f; 2.0f; 4.0f|])
        a.[2..] |> should equal <| SparseVector([|3; 5|], [|4.0f; 6.0f|])

    [<Test>]
    member test.``Element-wise addition should be correct.``() =
        let a = SparseVector([|0; 1; 3|], [|1.0f; 2.0f; 4.0f|])
        let b = SparseVector([|2; 3|], [|3.0f; 4.0f|])
        (a + b) |> should equal <| SparseVector([|0; 1; 2; 3|], [|1.0f; 2.0f; 3.0f; 8.0f|])

    [<Test>]
    member test.``Element-wise substraction should be correct.``() =
        let a = SparseVector([|0; 1; 3|], [|1.0f; 2.0f; 4.0f|])
        let b = SparseVector([|2; 3|], [|3.0f; 4.0f|])
        (a - b) |> should equal <| SparseVector([|0; 1; 2|], [|1.0f; 2.0f; -3.0f|])
        (a - a) |> should equal <| SparseVector([||], [||])

    [<Test>]
    member test.``Element-wise multiplication should be correct.``() =
        let a = SparseVector([|0; 1; 3|], [|1.0f; 2.0f; 4.0f|])
        let b = SparseVector([|0; 2; 3|], [|1.0f; 3.0f; 4.0f|])
        (a * b) |> should equal <| SparseVector([|0; 3|], [|1.0f; 16.0f|])

    [<Test>]
    member test.``Scalar product should be correct.``() =
        let a = SparseVector([|0; 1; 3|], [|1.0f; 2.0f; 4.0f|])
        let b = SparseVector([|0; 2; 3|], [|1.0f; 3.0f; 4.0f|])
        (a .* b) |> should equal 17.0f

    [<Test>]
    member test.``Negation should be correct.``() =
        let a = SparseVector([|0; 1; 3; 5|], [|1.0f; -2.0f; 4.0f; -6.0f|])
        -a |> should equal <| SparseVector([|0; 1; 3; 5|], [|-1.0f; 2.0f; -4.0f; 6.0f|])

    [<Test>]
    member test.``Multiplication by scalar should be correct.``() =
        let a = SparseVector([|0; 1; 3; 5|], [|1.0f; -2.0f; 4.0f; -6.0f|])
        (a * 3.0f) |> should equal <| SparseVector([|0; 1; 3; 5|], [|3.0f; -6.0f; 12.0f; -18.0f|])

    [<Test>]
    member test.``Division by scalar should be correct.``() =
        let a = SparseVector([|0; 1; 3; 5|], [|1.0f; -2.0f; 4.0f; -6.0f|])
        (a / 2.0f) |> should equal <| SparseVector([|0; 1; 3; 5|], [|0.5f; -1.0f; 2.0f; -3.0f|])
