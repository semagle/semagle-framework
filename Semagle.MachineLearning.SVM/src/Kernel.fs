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

/// Popular kernel functions definitions
module Kernel =
    /// Linear kernel function $(x_1 \cdot x_2)$
    let inline linear (x1 : ^X) (x2 : ^X) : float32 = x1 .* x2

    /// Polynomial kernel function $(\gamma*(x_1 \cdot x_2)+\mu)^n$
    let inline polynomial (gamma : float32) (mu : float32) (n : float32) (x1 : ^X) (x2 : ^X) : float32 = (gamma*(x1 .* x2) + mu) ** n

    /// Radial basis kernel function $exp(-\gamma||x_1 - x_2||^2)$
    let inline rbf (gamma : float32) (x1 : ^X) (x2 : ^X) : float32 = // let x' = (x1 - x2) in exp (-gamma * (x' .* x'))
        exp (-gamma * (x1 .* x1 + x2 .* x2 - 2.0f*(x1 .* x2)))

    /// Sigmoid kernel function $tanh(\gamma(x_1 \cdot x_2)+\mu)$
    let inline sigmoid (gamma : float32) (mu : float32) (x1 : ^X) (x2 : ^X) : float32 = tanh (gamma*(x1 .* x2) + mu)
