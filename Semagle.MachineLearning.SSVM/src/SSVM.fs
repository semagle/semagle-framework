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

open Semagle.Numerics.Vectors
open System
open System.Runtime.CompilerServices

/// Simple feature function
type FeatureFunction<'X> = 'X -> Vector

/// Joint feature function type
type JointFeatureFunction<'X,'Y> = (* x *) 'X -> (* y *) 'Y -> SparseVector

/// Joint kernel function type
type JointKernel<'X, 'Y> = (* x *) 'X -> (* y *) 'Y -> (* x' *) 'X -> (* y' *) 'Y -> float

/// Structured SVM rescaling
type Rescaling = Slack | Margin

/// Structured SVM loss function type
type LossFunction<'Y> = (* y *) 'Y -> (* y' *)'Y -> float

/// Delta value
[<Struct;IsReadOnly;CustomEquality;CustomComparison>]
type Delta(rescaling: Rescaling, loss: float, dJFxW: float) =
    member _.Rescaling = rescaling
    member _.Loss = loss
    member _.WxdJF = dJFxW
    member _.Value =
        match rescaling with
        | Slack -> loss*(1.0 - dJFxW)
        | Margin -> loss - dJFxW

    override a.Equals b =
        match b with
        | :? Delta as b -> a.Value = b.Value
        | _ -> failwith "Invalid equality for Delta"

    override a.GetHashCode () =
        a.Value.GetHashCode ()

    interface IComparable with
        member a.CompareTo b =
            match b with
            | :? Delta as b -> sign (a.Value - b.Value)
            | _ -> failwith "Invalid comparison for Delta"

    static member (+)(a: Delta, b: Delta) =
        Delta(a.Rescaling, a.Loss + b.Loss, a.WxdJF + b.WxdJF)

/// Argmax function type
type ArgmaxFunction<'Y> = (* W *) float[] -> (* i *) int -> 'Y * Delta
