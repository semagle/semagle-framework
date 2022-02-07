// Copyright 2022 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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

open Semagle.Numerics.Vectors
open System
open System.Collections.Generic
open System.IO

/// Reading CRFSuite files
module CRFSuite =
    type LabelSequence = string[]
    type Features = string[]
    type FeatureSequence = Features[]
    type FeatureDictionary = IDictionary<string, int>

    /// Read an array of label and feature sequences
    let read (file : string) : seq<LabelSequence * FeatureSequence> = 
        seq {
            use r = new StreamReader(file)
            let rec readSequence Y X =
                let line = r.ReadLine()
                if not (String.IsNullOrEmpty line) then
                    let fields = line.Split [|'\t'|]
                    let y = fields.[0]
                    let x = fields.[1..]
                    readSequence (y :: Y) (x :: X)
                else
                    List.rev Y |> List.toArray, List.rev X |> List.toArray
            while not r.EndOfStream do
                yield readSequence List.empty List.empty
        }

    /// Build a feature dictionary for the sequences
    let buildFeatureDictionary (sequences : seq<FeatureSequence>) : FeatureDictionary =
        let dictionary = Dictionary<string, int>() :> FeatureDictionary
        for sequence in sequences do
            for element in sequence do
                for feature in element do
                    if not (dictionary.ContainsKey feature) then
                        dictionary.Add(feature, dictionary.Count)
        dictionary

    /// Vectorize the element features
    let vectorizeFeatures (dictionary: FeatureDictionary) (features: Features) =
        let indices = Array.zeroCreate<int> features.Length
        let mutable k = 0
        for i = 0 to features.Length-1 do
            let mutable index = 0
            if dictionary.TryGetValue(features.[i], &index) then
                indices.[k] <- index
                k <- k + 1
        let indices = indices.[0..k]
        Array.sortInPlace indices
        SparseVector(indices, Array.create indices.Length 1.0f)

    /// Vectorize the element sequence
    let vectorizeSequence (dictionary: FeatureDictionary) (sequence : FeatureSequence) =
        Array.map (vectorizeFeatures dictionary) sequence

    /// Vectorize the sequences of elements
    let vectorizeSequences (dictionary: FeatureDictionary) (sequences : seq<FeatureSequence>) =
        Seq.map (vectorizeSequence dictionary) sequences
