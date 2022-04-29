// Copyright 2017-2022 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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

namespace Semagle.MachineLearning.Metrics

/// Classification metrics
module Classification =
    /// Returns classification accuracy score
    let accuracy (expected : 'Y[]) (predicted : 'Y[]) = 
        if (Array.length expected) <> (Array.length predicted) then
            invalidArg "expected and predicted" "have different lengths"
        let correct = Array.fold2 (fun count e p -> if e = p then count + 1 else count) 0 expected predicted
        (float correct) / (float (Array.length expected))

    let private increment key table =
        match Map.tryFind key table with
        | Some(count) -> Map.remove key table |> Map.add key (count + 1)
        | None -> Map.add key 1 table

    let private keysOf table =
        table |> Map.toSeq |> Seq.map fst |> Set.ofSeq

    /// Returns recall score
    let recall (expected : 'Y[]) (predicted : 'Y[]) =
        if (Array.length expected) <> (Array.length predicted) then
            invalidArg "expected and predicted" "have different lengths"
        let tp, fn =
            Array.fold2 (fun (tp, fn) e p ->
                            if e = p then
                                (increment e tp, fn)
                            else
                                (tp, increment e fn))
                        (Map.empty, Map.empty)
                        expected predicted

        Set.ofArray expected
        |> Seq.map (fun y ->
            let tp = Map.tryFind y tp |> Option.defaultValue 0
            let fn = Map.tryFind y fn |> Option.defaultValue 0
            let recall =
                if tp > 0 then
                    (float tp) / (float (tp + fn))
                else
                    0.0
            (y, recall))
        |> Map.ofSeq

    /// Returns precision score
    let precision (expected : 'Y[]) (predicted : 'Y[]) =
        if (Array.length expected) <> (Array.length predicted) then
            invalidArg "expected and predicted" "have different lengths"
        let tp, fp =
            Array.fold2 (fun (tp, fp) e p ->
                            if e = p then
                                (increment e tp, fp)
                            else
                                (tp, increment p fp))
                        (Map.empty, Map.empty)
                        expected predicted

        Set.ofArray expected
        |> Seq.map (fun y ->
            let tp = Map.tryFind y tp |> Option.defaultValue 0
            let fp = Map.tryFind y fp |> Option.defaultValue 0
            let precision =
                if tp > 0 then
                    (float tp) / (float (tp + fp))
                else
                    0.0
            (y, precision))
        |> Map.ofSeq

    /// Returns F1 score
    let f1 (expected : 'Y[]) (predicted : 'Y[]) =
        if (Array.length expected) <> (Array.length predicted) then
            invalidArg "expected and predicted" "have different lengths"
        let tp, fp, fn =
            Array.fold2 (fun (tp, fp, fn) e p ->
                            if e = p then
                                (increment e tp, fp, fn)
                            else
                                (tp, increment p fp, increment e fn))
                        (Map.empty, Map.empty, Map.empty)
                        expected predicted

        Set.ofArray expected
        |> Seq.map (fun y ->
            let tp = Map.tryFind y tp |> Option.defaultValue 0
            let fp = Map.tryFind y fp |> Option.defaultValue 0
            let fn = Map.tryFind y fn |> Option.defaultValue 0
            let precision =
                if tp > 0 then
                    2.0*(float tp) / (float (2*tp + fp + fn))
                else
                    0.0
            (y, precision))
        |> Map.ofSeq
