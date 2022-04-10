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

open System.Collections.Generic
open Semagle.Numerics.Vectors

module LRF =
    [<Struct>]
    type private Column = { index : int; features: SparseVector[] }

    /// LRU list of computed feature columns
    type LRF(capacity : int, N : int, dJF : int -> int -> SparseVector, parallelize : bool) =
        let indices = Dictionary<int, LinkedListNode<Column>>()
        let columns = LinkedList<Column>()

        /// Remove columns of dJF matrix
        member lrf.Resize M =
            lock lrf (fun () ->
                let mutable node = columns.First
                while node <> null do
                    if node.Value.index >= M-1 then
                        let removed = node
                        node <- node.Next
                        indices.Remove removed.Value.index |> ignore
                        columns.Remove removed
                    else
                        node <- node.Next)

        /// Returns k-th column of dJF matrix
        member lrf.Item i =
            lock lrf (fun () ->
                let mutable node : LinkedListNode<Column> = null
                if indices.TryGetValue (i, &node) then
                    columns.Remove node
                else
                    if columns.Count >= capacity then
                        indices.Remove columns.Last.Value.index |> ignore
                        columns.RemoveLast ()

                    let features =
                        if parallelize then
                            Array.Parallel.init N (fun j -> dJF i j)
                        else
                            Array.init N (fun j -> dJF i j)

                    node <- LinkedListNode({ index = i; features = features })
                    indices.Add (i, node) 

                columns.AddFirst node

                node.Value.features)

        /// Swap column elements
        member lrf.Swap (i : int) (j : int) =
            lock lrf (fun () ->
                let mutable node_i : LinkedListNode<Column> = null
                if indices.TryGetValue (i, &node_i) then
                    indices.Remove i |> ignore
                    node_i.Value <- { index = j; features = node_i.Value.features }

                let mutable node_j : LinkedListNode<Column> = null
                if indices.TryGetValue (j, &node_j) then
                    indices.Remove j |> ignore
                    node_j.Value <- { index = i; features = node_j.Value.features }

                if node_i <> null then
                    indices.Add (j, node_i)

                if node_j <> null then
                    indices.Add (i, node_j))
