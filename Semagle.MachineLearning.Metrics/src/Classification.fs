// Copyright 2017 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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

/// Classification metrics
module Classification =
    /// Returns classification accuracy score
    let accuracy (expected : 'Y[]) (predicted : 'Y[]) = 
        if (Array.length expected) <> (Array.length predicted) then
            invalidArg "expected and predicted" "have different lengths"
        let correct = Array.fold2 (fun count e p -> if e = p then count + 1 else count) 0 expected predicted
        (float correct) / (float (Array.length expected))
