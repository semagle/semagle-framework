// Copyright 2018-2022 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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

namespace Semagle.MachineLearning.SSVM

open Semagle.Numerics.Vectors

module LRF =
    [<Literal>]
    let private not_found = -1

    let inline swap (a : 'A[]) i j =
        let tmp = a.[i]
        a.[i] <- a.[j]
        a.[j] <- tmp

    /// LRU list of computed feature columns
    type LRF(capacity : int, N : int, dJF : int -> int -> SparseVector, parallelize : bool) =
        let indices = Array.create capacity not_found
        let columns = Array.zeroCreate<SparseVector[]> capacity

        /// Remove columns of dJF matrix
        member lrf.Resize M =
            lock lrf (fun () ->
                for n = 0 to indices.Length-1 do
                    if indices.[n] >= M-1 then
                        indices.[n] <- not_found
                        columns.[n] <- null)

        /// Returns k-th column of dJF matrix
        member lrf.Item i =
            lock lrf (fun () ->
                let k = lrf.tryFindIndex i
                if k <> not_found then
                    let column = columns.[k]

                    if k <> 0 then
                        lrf.shiftAndReplace k i column

                    column
                else
                    let column =
                        if parallelize then
                            Array.Parallel.init N (fun j -> dJF i j)
                        else
                            Array.init N (fun j -> dJF i j)

                    lrf.shiftAndReplace (indices.Length - 1) i column

                    column)

        /// Swap column elements
        member lrf.Swap (i : int) (j : int) =
            lock lrf (fun () ->
                 for k = 0 to indices.Length - 1 do
                    let index_k = indices.[k]
                    if index_k = i then indices.[k] <- j
                    else if index_k = j then indices.[k] <- i)

        /// Try to find computed column values
        member private lrf.tryFindIndex i =
            let mutable k = 0
            while k < indices.Length && indices.[k] <> i do
                k <- k + 1

            if k < indices.Length then k else not_found

        /// Shift [0,k-1]-th elements and replace 0-th element
        member private lrf.shiftAndReplace k i column =
            // shift [0,k-1]-th elements
            for k' = k downto 1 do
                indices.[k'] <- indices.[k'-1]
                columns.[k'] <- columns.[k'-1]
            // replace 0-th element
            indices.[0] <- i
            columns.[0] <- column
