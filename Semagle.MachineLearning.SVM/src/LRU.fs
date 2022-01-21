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

namespace Semagle.MachineLearning.SVM

open System.Threading.Tasks

module LRU = 
    [<Literal>]
    let private not_found = -1

    let inline swap (a : 'A[]) i j =
        let tmp = a.[i]
        a.[i] <- a.[j]
        a.[j] <- tmp

    /// LRU list of computed columns
    type LRU<'A>(capacity : int, N : int, Q : int -> int -> 'A, parallelize : bool) =
        let indices = Array.create capacity not_found
        let columns = Array.init capacity (fun _ -> Array.zeroCreate<'A> N)
        let lengths = Array.zeroCreate<int> capacity

        let mutable N = N

        /// Resize columns of Q matrix
        member lru.Resize (n : int) =
            for k = 0 to indices.Length - 1 do
                if n > N then
                    let column = Array.zeroCreate n
                    Array.blit columns.[k] 0 column 0 N
                    columns.[k] <- column
                else
                    columns.[k] <- columns.[k].[..n-1]
                    lengths.[k] <- n
            N <- n

        /// Returns L elements of j-th column of Q matrix
        member lru.Get (j : int) (L : int) =
            let k = lru.tryFindIndex j
            if k <> not_found then
                let column = columns.[k]
                let length = lengths.[k]
                if length < L then
                    if parallelize then
                        Parallel.For(length, L, fun i -> column.[i] <- Q i j) |> ignore
                    else
                        for i = length to L-1 do
                            column.[i] <- Q i j
                    lengths.[k] <- L

                if k <> 0 then
                    // shift [0,k-1]-th elements
                    for k' = k downto 1 do
                        indices.[k'] <- indices.[k'-1]
                        lengths.[k'] <- lengths.[k'-1]
                        columns.[k'] <- columns.[k'-1]
                    // replace 0-th element
                    indices.[0] <- j
                    lengths.[0] <- L
                    columns.[0] <- column

                column
            else
                let column = columns.[columns.Length-1]
                // shift [0,...]-th element
                for k' = indices.Length-1 downto 1 do
                    indices.[k'] <- indices.[k'-1]
                    lengths.[k'] <- lengths.[k'-1]
                    columns.[k'] <- columns.[k'-1]

                if parallelize then
                    Parallel.For(0, L, fun i -> column.[i] <- Q i j) |> ignore
                else
                    for i = 0 to L-1 do
                        column.[i] <- Q i j

                // replace 0-th element
                indices.[0] <- j
                lengths.[0] <- L
                columns.[0] <- column

                column

        /// Swap column elements
        member lru.Swap (i : int) (j : int) =
            for k = 0 to indices.Length - 1 do
                let index_k = indices.[k]
                if index_k = i then indices.[k] <- j
                else if index_k = j then indices.[k] <- i

                let length = lengths.[k]
                if i < length && j < length then
                    swap columns.[k] i j
                else if i >= length && j < length then
                    columns.[k].[j] <- Q index_k i
                else if i < length && j >= length then
                    columns.[k].[i] <- Q index_k j

        /// Try to find computed column values
        member private lru.tryFindIndex i =
            let mutable k = 0
            while k < indices.Length && indices.[k] <> i do
                k <- k + 1

            if k < indices.Length then k else not_found
