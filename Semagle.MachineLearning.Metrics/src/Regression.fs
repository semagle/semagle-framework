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

namespace Semagle.MachineLearning.Metrics

open LanguagePrimitives

module Regression =
    /// Returns Mean Square Error
    let mse (expected : float32[]) (predicted : float32[]) =
        if (Array.length expected) <> (Array.length predicted) then
            invalidArg "expected and predicted" "have different lengths"
        DivideByInt (Array.fold2 (fun sum e p -> sum + pown (e - p) 2) 0.0f expected predicted) 
            (Array.length expected)