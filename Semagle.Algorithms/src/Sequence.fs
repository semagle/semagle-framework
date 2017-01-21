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

module Sequence =
    let inline viterbi (N : int) (K : int) (h : (* i *) int -> ( (* k *) int -> ((* k' *) int -> ^T))) =
        let S = Array.zeroCreate N

        // forward computation
        for i = 0 to N-1 do
            S.[i] <-
                let h_i_k = h i
                Array.init K (fun k -> 
                    let h_i_k_k' = h_i_k k
                    if i > 0 then
                        S.[i-1] |> Array.argmaximax (fun k' (_, h_1) -> h_1 + (h_i_k_k' k'))
                    else
                        (-1, h_i_k_k' -1))    
        
        // find maximum cost 
        let k_max, h_max = (Array.last S) |> Array.argmaximax (fun k (_, h) -> h)

        // backward decoding
        let s = Array.zeroCreate N
        let mutable k_max = k_max
        for i = N-1 downto 0 do
            s.[i] <- k_max
            k_max <- S.[i].[k_max] |> fst

        (s, h_max)
