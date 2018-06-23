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

/// Structured SVM model for multi-class classification
type MultiClass<'X,'Y> = MultiClass of FeatureFunction<'X> * (* W *) float32[] * 'Y[]

module MultiClass =
    /// Optimization parameters for the multi-class structured SVM
    type MultiClass<'Y> = {
         /// Penalty for slack variables
         C : float32;
         /// Loss function
         loss : LossFunction<'Y>
    }

    let defaultMultiClass : MultiClass<'Y> = {
        C = 1.0f; loss = (fun y y' -> if y = y' then 0.0f else 1.0f)
    }

    /// Returns i-th feature index for k-th class
    let inline private index D k i = D*k + i

    /// Learn structured SVM model for multi-class classification
    let learn (X : 'X[])(Y : 'Y[])(F: FeatureFunction<'X>)(parameters: MultiClass<'Y>)(options : OneSlack.OptimizationOptions) =
        let Y' = Array.distinct Y
        let K = Array.length Y'
        let D = Array.fold (fun d x -> max d (F x).Dimensions) 0 X

        let loss = parameters.loss

        let JF (x : 'X) (y : 'Y) =
            let F_x = (F x).AsSparse
            let k = Array.findIndex ((=) y) Y'
            let index_k = index D k
            SparseVector(Array.map index_k F_x.Indices, F_x.Values)

        let argmax (W : float32[]) (i : int) =
            let x = X.[i]
            let y = Y.[i]
            let F = (F x).AsSparse
            let k = Array.findIndex ((=) y) Y'
            let WxJF = F.SumBy(fun i v -> W.[index D k i]*v)
            let y_max, loss_max, cost_max = 
                Y' 
                |> Array.map (fun y' ->
                    let k' = Array.findIndex ((=) y') Y'
                    let WxdJF = WxJF - F.SumBy(fun i v -> W.[index D k' i]*v)
                    let loss = loss y y'
                    let m = match options.rescaling with | Slack -> loss | Margin -> 1.0f
                    let cost = loss - m*WxdJF
                    (y', loss, cost))
                |> Array.maxBy (fun (_, _, cost) -> cost)
            y_max, loss_max, cost_max

        let W = OneSlack.oneSlack X Y JF { C = parameters.C; dimensions = K*D; loss = loss; argmax = argmax } options

        MultiClass(F, W, Y')

    /// Predict class by multi-class structured model
    let predict (MultiClass(F, W, Y) : MultiClass<'X,'Y>) (x : 'X) : 'Y =
        let F = F x
        let D = W.Length / (Array.length Y)
        let mutable k_max = 0
        let mutable WxJF_max = System.Single.NegativeInfinity
        for k = 0 to Y.Length-1 do
            let inline index_k i = index D k i
            let WxJF = F.SumBy (fun i v -> 
                if i < D then
                    W.[index_k i]*v
                else
                    0.0f)
            if WxJF > WxJF_max then
                k_max <- k
                WxJF_max <- WxJF
        Y.[k_max]
