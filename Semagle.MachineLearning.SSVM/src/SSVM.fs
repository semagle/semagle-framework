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

/// Simple feature function
type FeatureFunction<'X> = 'X -> Vector

/// Joint feature function type
type JointFeatureFunction<'X,'Y> = (* x *) 'X -> (* y *) 'Y -> SparseVector

/// Joint kernel function type
type JointKernel<'X, 'Y> = (* x *) 'X -> (* y *) 'Y -> (* x' *) 'X -> (* y' *) 'Y -> float32

/// Structured SVM rescaling
type Rescaling = Slack | Margin

/// Structured SVM loss function type
type LossFunction<'Y> = (* y *) 'Y -> (* y' *)'Y -> float32

/// Argmax function result
type Argmax<'Y> = (* y *) 'Y * (* loss *) float32 * (* cost *) float32

/// Argmax function type
type ArgmaxFunction<'Y> = (* W *) float32[] -> (* i *) int -> Argmax<'Y>