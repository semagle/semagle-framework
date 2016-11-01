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

open Semagle.Numerics.Vectors

/// Simple feature function
type FeatureFunction<'X> = 'X -> SparseVector

/// Joint feature function type
type JointFeatureFunction<'X,'Y> = 'X -> 'Y -> SparseVector

/// Joint kernel function type
type JointKernel<'X, 'Y> = 'X -> 'Y -> 'X -> 'Y -> float32

/// Structured SVM rescaling
type Rescaling = Slack | Margin

/// Structured SVM model
type SSVM<'X,'Y> = OneSlack of DenseVector

/// Structured SVM loss function type
type LossFunction<'Y> = 'Y -> 'Y -> float32

/// Type alias for argmax function result
type Result<'Y> = (* y *) 'Y * (* loss *) float32 * (* cost *) float32

/// Argmax function type
type ArgmaxLossFunction<'X,'Y> = SSVM<'X,'Y> -> int -> Result<'Y>
