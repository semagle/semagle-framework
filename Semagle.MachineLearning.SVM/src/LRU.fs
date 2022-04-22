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

namespace Semagle.MachineLearning.SVM

open System.Threading.Tasks

module LRU = 
    [<Literal>]
    let private not_found = -1

    /// Unit of measure for cache size
    [<Measure>] type MB

    let inline swap (a : 'A[]) i j =
        let tmp = a.[i]
        a.[i] <- a.[j]
        a.[j] <- tmp

    /// LRU list of computed columns
    type LRU<'A>(size : int<MB>, N : int, Q : int -> int -> 'A, parallelize : bool) =
        let capacity =
            let columnSize = sizeof<'A>*N + sizeof<int>*2 + sizeof<'A[]> in
            max 2 (((int size)*1024*1024) / columnSize)

        let indices = Array.create capacity not_found
        let columns = Array.init capacity (fun _ -> Array.zeroCreate<'A> N)
        let lengths = Array.zeroCreate<int> capacity

        let mutable N = N

        /// Resize columns of Q matrix
        member lru.Resize (N' : int) =
            for k = 0 to indices.Length - 1 do
                if N' > N then
                    let column = Array.zeroCreate N'
                    Array.blit columns.[k] 0 column 0 N
                    columns.[k] <- column
                else
                    columns.[k] <- columns.[k].[..N'-1]

                    if indices.[k] >= N'-1 then
                        indices.[k] <- not_found
                        lengths.[k] <- 0
                    else
                        lengths.[k] <- min N' lengths.[k]

            N <- N'

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
                    lengths.[0] <- max length L
                    columns.[0] <- column

                column
            else
                let mutable k = 0
                while k < indices.Length - 1 && indices.[k] <> not_found do
                    k <- k + 1

                let column = columns.[k]
                // shift [0,...]-th element
                for k' = k downto 1 do
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
                if index_k <> not_found then
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
