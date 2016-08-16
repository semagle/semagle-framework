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

/// Kernel function
type Kernel<'X> = 'X -> 'X -> float32

/// SVM model definition includes kernel function, array of support vectors with respective weights and bias value.
type SVM<'X> = 
    | TwoClass of Kernel<'X> * ('X[]) *  (float32[]) * float32
    | OneClass of Kernel<'X> * ('X[]) *  (float32[]) * float32
    | Regression of Kernel<'X> * ('X[]) *  (float32[]) * float32

/// Two class classification
module TwoClass =
    /// Predict {+1,0,-1} class of the sample x using the specified SVM model.
    let inline predict (model : SVM<'X>) (x : 'X) =
        match model with 
        | TwoClass (K,X,A,b) -> sign (b + Array.fold2 (fun sum x_i a_i -> sum + a_i * (K x_i x)) 0.0f X A)
        | _ -> invalidArg "svm" "type is invalid"

/// One class classification (distribution estimation)
module OneClass =
    /// Predict {+1, 0, -1} class of the sample usung the specified SVM model.
    let inline predict (model : SVM<'X>) (x : 'X) =
        match model with
        | OneClass (K,X,A,b) -> sign (b + Array.fold2 (fun sum x_i a_i -> sum + a_i * (K x_i x)) 0.0f X A)
        | _ -> invalidArg "svm" "type is invalid"

/// Regression
module Regression =
    /// Predict $y \in R$ target output.
    let inline predict (model : SVM<'X>) (x : 'X) =
        match model with
        | Regression (K,X,A,b) -> b + Array.fold2 (fun sum x_i a_i -> sum + a_i * (K x_i x)) 0.0f X A
        | _ -> invalidArg "svm" "type is invalid"

