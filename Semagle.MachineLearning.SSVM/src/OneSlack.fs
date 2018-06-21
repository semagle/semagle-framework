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

open LanguagePrimitives
open System.Threading.Tasks

open Semagle.Logging
open Semagle.MachineLearning.SVM
open Semagle.MachineLearning.SVM.LRU
open Semagle.MachineLearning.SSVM.LRF
open Semagle.Numerics.Vectors

module OneSlack =
    type OptimizationOptions = {
        /// Maximum optimization error
        epsilon : float32
        /// Rescaling type
        rescaling : Rescaling;
        /// Parellelize
        parallelize : bool;
        /// SMO optimization options
        smoOptimizationOptions : SMO.OptimizationOptions
    }

    let defaultOptimizationOptions : OptimizationOptions = {
        epsilon = 0.001f; rescaling = Slack; parallelize = true; smoOptimizationOptions = SMO.defaultOptimizationOptions
    }

    type OneSlack<'X,'Y> = {
        /// Penalty for slack variables
        C : float32;
        /// Solution dimensions
        dimensions : int;
        /// Loss function
        loss : LossFunction<'Y>;
        /// Argmax function
        argmax : ArgmaxFunction<'Y>
    }

    /// Q matrix for structured problems
    type private Q_S(capacity : int, N : int, Q : int -> int -> float32, dJF : LRF, parallelize : bool) =
        let mutable N = N
        let mutable D = Array.init N (fun i -> Q i i)
        let lru = LRU(capacity, N, Q, parallelize)

        /// Resize Q matrix
        member this.Resize (N' : int) =
            lru.Resize N'
            if N' > N then
                let D' = Array.zeroCreate N'
                Array.blit D 0 D' 0 N
                if parallelize then
                    Parallel.For(N, N', (fun i -> D'.[i] <- Q i i)) |> ignore
                else
                    for i=N to N'-1 do D'.[i] <- Q i i
                D <- D'
            else
                D <- D.[..N'-1]
            N <- N'

        interface SMO.Q with
            /// Swap column elements
            member q.Swap (i : int) (j : int) = 
                lru.Swap i j
                swap D i j
                dJF.Swap i j

            /// Returns N elements of the main diagonal of Q matrix
            member q.D = D

            /// Returns L elements of j-th column of Q matrix
            member q.C (j : int) (L : int) = lru.Get j L

    // 1-slack optimization problem solver
    let oneSlack (X : 'X[]) (Y : 'Y[]) (JF : JointFeatureFunction<'X,'Y>) (parameters : OneSlack<'X,'Y>) 
                 (options : OptimizationOptions) =
        if Array.length X <> Array.length Y then
            invalidArg "X and Y" "have different lengths"

        let logger = LoggerBuilder(Log.create "OneSlack")

        logger { info (sprintf "dimensons = %d" parameters.dimensions)}

        let N = Array.length X
        let W = Array.zeroCreate<float32> parameters.dimensions

        let mutable X' = Array.empty<(* Y *) 'Y[] * (* L *) float32[]>

        let dJF = LRF(100, N, (fun k i -> let Y' = fst X'.[k] in (JF X.[i] Y.[i]) - (JF X.[i] Y'.[i])), options.parallelize)

        let mu L = match options.rescaling with | Slack -> L | Margin -> 1.0f

        let inline add_by_N (W : float32[]) k a =
            if a <> 0.0f then
                let a = DivideByInt a N
                let L_k = snd X'.[k]
                let dJF_k = dJF.[k]
                Array.iter2 (fun L (dJF : SparseVector) -> 
                             let m = mu L
                             Array.iter2 (fun i v -> W.[i] <- W.[i] + a * m * v) dJF.Indices dJF.Values) L_k dJF_k

        let H k k' = 
            let inline updates k =
                let W_k = Array.zeroCreate parameters.dimensions
                add_by_N W_k k 1.0f
                W_k

            let W_k = updates k
            let W_k' = if k <> k' then updates k' else W_k

            Array.fold2 (fun sum w_k w_k' -> sum + w_k * w_k') 0.0f W_k W_k'

        let M = int (1.0f / options.epsilon)
        let Q = Q_S(SMO.capacity options.smoOptimizationOptions.cacheSize M, 0, H, dJF,
                    options.smoOptimizationOptions.parallelize)

        let inline xi X' =
            DivideByInt 
                (if not (Array.isEmpty X') then
                    X' 
                    |> Seq.mapi (fun k x' -> 
                        let L_k = snd x'
                        let dJF_k = dJF.[k]
                        Seq.map2 (fun L (dJF : SparseVector) -> 
                            let m = mu L
                            let WxdJF = dJF.SumBy(fun i v -> W.[i]*v)
                            L - m * WxdJF) L_k dJF_k
                        |> Seq.sum)
                    |> Seq.max
                    |> max 0.0f
                 else
                    0.0f) N

        let inline reconstructW A =
            Array.fill W 0 W.Length 0.0f
            Array.iteri (add_by_N W) A

        let inline solve X' Y' A C p = 
            if not (Array.isEmpty X') then
                SMO.C_SMO X' Y' Q { A = A; C = C; p = p } 
                          { options.smoOptimizationOptions with epsilon = options.epsilon * 2.0f } |> ignore

                reconstructW A

            xi X'

        let newConstraint () =
            let Y', L, H = 
                (if options.parallelize then Array.Parallel.init else Array.init) N (parameters.argmax W) 
                |> Array.unzip3
            (Y',L), H

        let inline isOptimal xi xi_k =
            logger { debug (sprintf "xi=%f, xi_k=%f" xi xi_k) }

            (xi_k - xi) <= options.epsilon

        let inline append (a : 'A[]) (e: 'A) =
            let M = Array.length a
            let b = Array.zeroCreate<'A> (M+1)
            Array.blit a 0 b 0 M
            b.[M] <- e
            b

        let rec optimize (k : int) (Y' : float32[]) (A : float32[]) (C : float32[]) (p : float32[]) =
            logger { debug (sprintf "iteration = %d" k) }

            let xi = solve X' Y' A C p

            logger { debug (sprintf "A=%A" A) }

            let x',h = newConstraint ()

            if not (isOptimal xi (DivideByInt (Array.sum h) N)) then
                X' <- append X' x'
                let Y' = append Y' 1.0f
                let C = append C parameters.C
                let A = append A (parameters.C - Array.sum A)
                let p = append p (DivideByInt -(Array.sum (snd x')) N)
                Q.Resize (Array.length X')

                optimize (k+1) Y' A C p
            else
                logger { info (sprintf "iteration=%d, xi=%f" k xi)}

        optimize (* k *) 0 (* Y' *) Array.empty (* A *) Array.empty (* p *) Array.empty (* C *) Array.empty

        W