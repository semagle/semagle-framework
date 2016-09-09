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

namespace Semagle.MachineLearning.SSVM

open Semagle.MachineLearning.SVM
open Semagle.Numerics.Vectors

type MultiClass<'X,'Y> = MultiClass of SSVM<'X,'Y> * ('Y[])

module MultiClass =
    type Parameters<'Y> = { rescaling : Rescaling; C : float32; epsilon : float32; loss : LossFunction<'Y>; 
                            options : SMO.OptimizationOptions  }

    let defaults : Parameters<'Y> = 
        { rescaling = Slack; C = 1.0f; epsilon = 0.001f; 
          loss = (fun y y' -> if y = y' then 0.0f else 1.0f);
          options = { strategy = SMO.SecondOrderInformation; maxIterations = 1000000; 
                      shrinking = true; cacheSize = 200<MB> } } 

    /// Learn structured SVM model for multi-class classification
    let learn (X: 'X[]) (Y : 'Y[]) (F : FeatureFunction<'X>) (parameters : Parameters<'Y>) =
        let YS = Array.distinct Y

        let JF x y =  
            let f = F x 
            let M = Array.length YS
            let y = Array.findIndex ((=) y) YS
            SparseVector(Array.map (fun i -> i*M + y) f.Indices, f.Values)

        let argmaxLoss model loss i = 
            match model with
            | OneSlack(F, W) ->
                let F_i = F X.[i] Y.[i]
                YS |> Array.map (fun y ->
                    let dF = F_i - (F X.[i] y)
                    let WF = W .* dF
                    let loss = loss Y.[i] y
                    let m = match parameters.rescaling with | Slack -> loss | Margin -> 1.0f
                    let cost = loss - m * WF
                    y, loss, dF, cost)
                |> Array.maxBy (fun (_, _, _, cost) -> cost)

        let ssvm = OneSlack.optimize X Y JF 
                                     { rescaling = parameters.rescaling; 
                                       C = parameters.C; epsilon = parameters.epsilon;
                                       loss = parameters.loss; argmaxLoss = argmaxLoss;
                                       options = parameters.options }

        MultiClass(ssvm,YS)

    /// Predict class by multi-class structured SVM model
    let predict (model : MultiClass<'X,'Y>) (x : 'X) : 'Y =
        match model with
        | MultiClass(OneSlack(F, W), Y) -> Y |> Array.maxBy (fun y -> W .* (F x y))
