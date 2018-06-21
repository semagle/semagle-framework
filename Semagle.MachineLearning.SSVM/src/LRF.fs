// Copyright 2018 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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
        let indices = Array.zeroCreate<int> capacity
        let columns = Array.zeroCreate<SparseVector[]> capacity

        let mutable first = 0
        let mutable last = 0

        /// Returns k-th column of dJF matrix
        member lrf.Item k =
            let index = lrf.tryFindIndex k
            if index <> not_found then
                columns.[index]
            else 
                let column = (if parallelize then Array.Parallel.init else Array.init) N (fun i -> dJF k i)
                lrf.insert k column
                column

        /// Swap column elements
        member lru.Swap (i : int) (j : int) = 
            let mutable index_i = not_found
            let mutable index_j = not_found

            let mutable k = first
            while k <> last do
                if indices.[k] = i then index_i <- k
                if indices.[k] = j then index_j <- k
                k <- (k + 1) % capacity 

            if index_i <> not_found then indices.[index_i] <- j
            if index_j <> not_found then indices.[index_j] <- i

        /// Try to find computed column values
        member private lru.tryFindIndex t =
            let mutable i = first
            while (i <> last) && (indices.[i] <> t) do
                i <- (i + 1) % capacity 

            if i <> last then i else not_found

        /// Insert new computed column values
        member private lru.insert index column =
            indices.[last] <- index
            columns.[last] <- column
            last <- (last + 1) % capacity

            if first = last then first <- (first + 1) % capacity


