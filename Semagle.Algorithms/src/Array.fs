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

namespace Semagle.Algorithms

module Array =
    let inline argmaximax (f : int -> ^A -> ^B) (a : ^A[]) =
        Array.fold (fun (i, (i_max, f_max)) a ->
            let f = f i a
            (i+1, if f > f_max then (i, f) else (i_max, f_max)))
            (0, (0, (f 0 a.[0]))) a
        |> snd
