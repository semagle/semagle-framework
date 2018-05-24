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
        let indices = Array.zeroCreate<int> capacity
        let columns = Array.zeroCreate<'A[]> capacity
        let lengths = Array.zeroCreate<int> capacity

        let mutable N = N
        let mutable first = 0
        let mutable last = 0

        /// Resize columns of Q matrix
        member lru.Resize (n : int) =
            let mutable k = first
            while k <> last do
                if n > N then
                    let column = Array.zeroCreate n
                    Array.blit columns.[k] 0 column 0 N
                    columns.[k] <- column
                else
                    columns.[k] <- columns.[k].[..n-1]
                    lengths.[k] <- n
                k <- (k + 1) % capacity
            N <- n

        /// Returns L elements of j-th column of Q matrix
        member lru.Get (j : int) (L : int) =
            let index = lru.tryFindIndex j
            if index <> not_found then
                let column = columns.[index]
                let length = lengths.[index]
                if length < L then
                    if parallelize then
                        Parallel.For(length, L, fun i -> column.[i] <- Q i j) |> ignore
                    else
                        for i = length to L-1 do
                            column.[i] <- Q i j
                    lengths.[index] <- L
                column
            else 
                let column = Array.zeroCreate N
                if parallelize then
                    Parallel.For(0, L, fun i -> column.[i] <- Q i j) |> ignore
                else
                    for i = 0 to L-1 do
                        column.[i] <- Q i j
                lru.insert j column L
                column

        /// Swap column elements
        member lru.Swap (i : int) (j : int) = 
            let mutable index_i = not_found
            let mutable index_j = not_found

            let mutable k = first
            while k <> last do
                if indices.[k] = i then index_i <- k
                if indices.[k] = j then index_j <- k
                swap columns.[k] i j
                k <- (k + 1) % capacity 

            if index_i <> not_found && index_j <> not_found then
                swap lengths index_i index_j

            if index_i <> not_found then indices.[index_i] <- j
            if index_j <> not_found then indices.[index_j] <- i

        /// Try to find computed column values
        member private lru.tryFindIndex t =
            let mutable i = first
            while (i <> last) && (indices.[i] <> t) do
                i <- (i + 1) % capacity 

            if i <> last then i else not_found

        /// Insert new computed column values
        member private lru.insert index column length =
            indices.[last] <- index
            columns.[last] <- column
            lengths.[last] <- length
            last <- (last + 1) % capacity

            if first = last then first <- (first + 1) % capacity
