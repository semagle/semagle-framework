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

/// <summary>Kernel function</summary>
/// <typeparam name="'X">Type of support vector</typeparam>
type Kernel<'X> = 'X -> 'X -> float32

/// <summary>SVM model definition includes kernel function, array of support vectors 
/// with respective weights and bias value.</summary>
/// <typeparam name="'X">The type of support vector.</typeparam>
type SVM<'X,'Y> = 
    /// SVM model for two class classification
    | TwoClass of Kernel<'X> * ('X[]) *  (float32[]) * float32
    /// SVM model for one class classification
    | OneClass of Kernel<'X> * ('X[]) *  (float32[]) * float32
    /// SVM model for regression
    | Regression of Kernel<'X> * ('X[]) *  (float32[]) * float32
    /// SVM model for multi-class classification
    | MultiClass of Kernel<'X> * (('Y * 'Y * ('X[]) * (float32[]) * float32)[])

module SVM =
    let inline predict (x : 'X) (K : Kernel<'X>) (X : 'X[]) (A : float32[]) (b : float32) =
        Array.fold2 (fun sum x_i a_i -> sum + a_i * (K x_i x)) b X A

/// Two class classification
module TwoClass =
    /// <summary>Predict {+1,0,-1} class of the sample x using the specified SVM model.</summary>
    /// <param name="model">The two class classification model.</param>
    /// <param name="x">The input sample.</param>
    /// <returns>The class of the sample.</returns>
    let inline predict (model : SVM<'X,'Y>) (x : 'X) =
        match model with 
        | TwoClass (K,X,A,b) -> sign (SVM.predict x K X A b)
        | _ -> invalidArg "svm" "type is invalid"

/// One class classification (distribution estimation)
module OneClass =
    /// <summary>Predict {+1, 0, -1} class of the sample usung the specified SVM model.</summary>
    /// <param name="model">The one class classification model.</param>
    /// <param name="x">The input sample.</param>
    /// <returns>The class of the sample.</returns>
    let inline predict (model : SVM<'X,'Y>) (x : 'X) =
        match model with
        | OneClass (K,X,A,b) -> sign (SVM.predict x K X A b)
        | _ -> invalidArg "svm" "type is invalid"

/// Regression
module Regression =
    /// <summary>Predict $y \in R$ target output.</summary>
    /// <param name="model">The regression model.</param>
    /// <param name="x">The input sample.</param>
    /// <returns>The predicted value.</returns>
    let inline predict (model : SVM<'X,'Y>) (x : 'X) =
        match model with
        | Regression (K,X,A,b) -> SVM.predict x K X A b
        | _ -> invalidArg "svm" "type is invalid"

/// Multi-class classification
module MultiClass =
    /// <summary>Predict $y \in Y$ class of the sample x using the specified SVM model.</summary>
    /// <param name="model">The multi-class classification model.</param>
    /// <param name="x">The input sample.</param>
    /// <returns>The class of the sample.</returns>
    let inline predict(model : SVM<'X,'Y>) (x : 'X) =
        match model with
        | MultiClass (K, models) -> 
            models
            |> Array.Parallel.map (fun (y', y'', X, A, b) ->
                 if sign (SVM.predict x K X A b) > 0 then y' else y'')
            |> Array.countBy id
            |> Array.maxBy snd
            |> fst
        | _ -> invalidArg "svm" "type is invalid"
