// Copyright 2016-2022 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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

namespace Semagle.Data.Formats

open System.IO
open Semagle.Numerics.Vectors

/// Reading and writing LIBSVM files
module LibSVM =
    /// Read sequence of (y, x) pairs from LIBSVM file
    let read (file : string) = seq {
        use r = new StreamReader(file)
        while not r.EndOfStream do
            let line = r.ReadLine().Trim()
            let fields = line.Split [|' '|]
            let y = float32 fields.[0]
            let indices, values = fields.[1..]
                                  |> Array.map (fun field ->
                                    let x_i = field.Split [|':'|] in (int x_i.[0])-1, float32 x_i.[1])
                                  |> Array.unzip
            yield y, SparseVector(indices, values)
    }

    /// Write sequence of (y, x) pairs to LIBSVM file
    let write (file : string) (samples : seq<float32*SparseVector>) =
        use w = new StreamWriter(file)
        let str (x : SparseVector) =
            Seq.zip (Array.toSeq x.Indices) (Array.toSeq x.Values)
            |> Seq.map (fun (index, value) -> sprintf "%d:%f" (index + 1) value)
            |> String.concat " "
        samples |> Seq.iter (fun (y, x) -> fprintfn w "%f %s" y (str x))
