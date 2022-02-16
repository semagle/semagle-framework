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

namespace Semagle.MachineLearning.SSVM

open Semagle.Algorithms
open Semagle.Logging
open Semagle.Numerics.Vectors
open System

/// Structured SVM model for sequence labeling
type Sequence<'X,'Y> = Sequence of FeatureFunction<'X> * (* W *) float[] * 'Y[]

module Sequence =
    /// Optimization parameters for the sequence structured SVM
    type Sequence<'Y> = {
        /// Penalty for slack variables
        C : float;
        /// Loss function
        loss : LossFunction<'Y>
    }

    let defaultSequence : Sequence<'Y> = {
        C = 1.0; loss = (fun y y' -> if y  = y' then 0.0 else 1.0)
    }

    /// Returns (i, k) feature index
    let inline index_k_i D K k i = K*K + D*k + i

    /// Returns (k, k') feature index
    let inline index_k_k_1 K k k_1 = k_1*K + k

    let learn (X : 'X[][]) (Y : 'Y[][]) (F : FeatureFunction<'X>)
              (parameters : Sequence<'Y>)
              (options : OneSlack.OptimizationOptions) =
        let Y' = Y |> Array.toSeq |> Seq.concat |> Seq.distinct |> Seq.toArray
        let K = Array.length Y'
        let D = Array.fold(fun D xs -> Array.fold (fun D x -> max D (F x).Dimensions) D xs) 0 X

        let logger = LoggerBuilder(Log.create "Sequence")

        logger { debug(sprintf "classes = %d, features = %d" K D) }

        let dimensions = K*K + K*D
        let inline index y = Array.findIndex ((=) y) Y'
        let index_k_i = index_k_i D K
        let index_k_k_1 = index_k_k_1 K

        let JF (X : 'X[]) (Y: 'Y[]) =
            if X.Length <> Y.Length then
                invalidArg "X and Y" "have different lengths"

            let inline prepend (e : 'A) (a : 'A[]) =
                let M = Array.length a
                let b = Array.zeroCreate<'A> (M+1)
                Array.blit a 0 b 1 M
                b.[0] <- e
                b

            Array.fold2 (fun (k_1, JF) x y ->
                let k = index y
                let F_x = (F x).AsSparse
                let F_x_y = SparseVector(Array.map (index_k_i k) F_x.Indices, F_x.Values)
                let JF =
                    if k_1 <> -1 then
                        JF + SparseVector(prepend (index_k_k_1 k k_1) F_x_y.Indices,
                                          prepend 1.0f F_x_y.Values)
                    else
                        JF + F_x_y
                (k, JF))
                (-1, SparseVector.Zero) X Y
            |> snd

        let loss_1 = parameters.loss
        let loss_n = Array.fold2 (fun sum y y' -> sum + (loss_1 y y')) 0.0

        let argmax (W : float[]) (i : int) =
            let X = X.[i]
            let Y = Y.[i]

            if X.Length <> Y.Length then
                invalidArg "X and Y" "have different lengths"

            let S_max, Delta_max =
                Sequence.viterbi X.Length K
                    (fun i ->
                        let F_x = (F X.[i]).AsSparse
                        let k = index Y.[i]
                        let k_1 = if i > 0 then index Y.[i-1] else -1
                        let W_k = Span<float>(W, K*K + D*k, D)
                        // TODO: Check W_k .* F in future F# compilers
                        let WxJF = SparseVector.(.*)(W_k, F_x)
                        (fun k' ->
                            let loss = loss_1 Y.[i] Y'.[k']
                            let W_k' = Span<float>(W, K*K + D*k', D)
                            // TODO: Check W_k' .* F in future F# compilers
                            let WxdJF = WxJF - SparseVector.(.*)(W_k', F_x)
                            (fun k'_1 ->
                                if k'_1 <> -1 then
                                    Delta(options.rescaling, loss,
                                          WxdJF + (W.[index_k_k_1 k k_1] -
                                            W.[index_k_k_1 k' k'_1]))
                                else
                                    Delta(options.rescaling, loss, WxdJF))))
                
            let Y_max = Array.map (Array.get Y') S_max

            Y_max, Delta_max

        let W =
            OneSlack.oneSlack X Y JF
                { C = parameters.C; dimensions = dimensions; loss = loss_n; argmax = argmax }
                options

        Sequence(F, W, Y')

    /// Predict labals sequence by sequence structured SVM
    let predict (Sequence(F,W,Y) : Sequence<'X, 'Y>) (X : 'X[]) : 'Y[] =
        if X.Length <> 0 then
            let K = Y.Length
            let D = (W.Length - K*K) / K

            let index_k_k_1 = index_k_k_1 K

            let S, _ =
                Sequence.viterbi X.Length Y.Length
                    (fun i ->
                        let F_x = (F X.[i]).AsSparse
                        if F_x.Dimensions > D then
                            invalidArg "x" "F(x).Dimensions > D"

                        (fun k ->
                            let W_k = Span<float>(W, K*K + D*k, D)
                            // TODO: Check W_k .* F in future F# compilers
                            let WxJF = SparseVector.(.*)(W_k, F_x)
                            (fun k_1 ->
                                if k_1 <> -1 then
                                    WxJF + W.[index_k_k_1 k k_1]
                                else
                                    WxJF)))
            Array.map (Array.get Y) S
        else
            Array.empty
