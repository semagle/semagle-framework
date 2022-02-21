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

open Semagle.Logging
open Semagle.Numerics.Vectors
open System

/// Structured SVM model for multi-class classification
type MultiClass<'X,'Y> = MultiClass of FeatureFunction<'X> * (* W *) float[] * 'Y[]

module MultiClass =
    /// Optimization parameters for the multi-class structured SVM
    type MultiClass<'Y> = {
         /// Penalty for slack variables
         C : float;
         /// Loss function
         loss : LossFunction<'Y>
    }

    let defaultMultiClass : MultiClass<'Y> = {
        C = 1.0; loss = (fun y y' -> if y = y' then 0.0 else 100.0)
    }

    /// Learn structured SVM model for multi-class classification
    let learn (X : 'X[])(Y : 'Y[])(F: FeatureFunction<'X>)(parameters: MultiClass<'Y>)(options : OneSlack.OptimizationOptions) =
        let Y' = Array.distinct Y
        let K = Array.length Y'
        let D = Array.fold (fun d x -> max d (F x).Dimensions) 0 X

        let logger = LoggerBuilder(Log.create "MultiClass")

        logger { info (sprintf "classes = %d, dimensons = %d" K D) }

        let loss = parameters.loss

        let JF (x : 'X) (y : 'Y) =
            let F_x = (F x).AsSparse
            let k = Array.findIndex ((=) y) Y'
            SparseVector(Array.map ((+) (D*k)) F_x.Indices, F_x.Values)

        let argmax (W : float[]) (i : int) =
            let x = X.[i]
            let y = Y.[i]
            let F_x = (F x).AsSparse
            let k = Array.findIndex ((=) y) Y'
            let W_k = Span<float>(W, D*k, D)
            // TODO: Check W_k .* F in future F# compilers
            let WxJF = SparseVector.(.*)(W_k, F_x)
            let y_max, delta_max =
                Y'
                |> Array.map (fun y' ->
                    let k' = Array.findIndex ((=) y') Y'
                    let W_k' = Span<float>(W, D*k', D)
                    // TODO: Check W_k' .* F in future F# compilers
                    let WxdJF = WxJF - SparseVector.(.*)(W_k', F_x)
                    (y', Delta(options.rescaling, loss y y', WxdJF)))
                |> Array.maxBy (fun (_, delta) -> delta)
            y_max, delta_max

        let W = OneSlack.oneSlack X Y JF { C = parameters.C; dimensions = K*D; loss = loss; argmax = argmax } options

        MultiClass(F, W, Y')

    /// Predict class by multi-class structured model
    let predict (MultiClass(F, W, Y) : MultiClass<'X,'Y>) (x : 'X) : 'Y =
        let D = W.Length / (Array.length Y)
        let F_x = (F x).AsSparse
        if F_x.Dimensions > D then
            invalidArg "x" "F(x).Dimensions > D"

        let mutable k_max = 0
        let mutable WxJF_max = System.Double.NegativeInfinity
        for k = 0 to Y.Length-1 do
            let W_k = Span<float>(W, D*k, D)
            // TODO: Check W_k .* F in future F# compilers
            let WxJF = SparseVector.(.*)(W_k, F_x)
            if WxJF > WxJF_max then
                k_max <- k
                WxJF_max <- WxJF
        Y.[k_max]
