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

open LanguagePrimitives
open System
open System.Threading.Tasks

open Semagle.Logging
open Semagle.MachineLearning.SVM
open Semagle.MachineLearning.SVM.LRU
open Semagle.MachineLearning.SSVM.LRF
open Semagle.Numerics.Vectors

module OneSlack =
    type OptimizationOptions = {
        /// Maximum optimization error
        epsilon : float
        /// Rescaling type
        rescaling : Rescaling;
        /// Parellelize
        parallelize : bool;
        /// SMO optimization options
        SMO : SMO.OptimizationOptions
    }

    let defaultOptimizationOptions : OptimizationOptions = {
        epsilon = 0.001; rescaling = Slack; parallelize = true; SMO = SMO.defaultOptimizationOptions
    }

    type OneSlack<'X,'Y> = {
        /// Penalty for slack variables
        C : float;
        /// Solution dimensions
        dimensions : int;
        /// Loss function
        loss : LossFunction<'Y>;
        /// Argmax function
        argmax : ArgmaxFunction<'Y>
    }

    /// Q matrix for structured problems
    type private Q_S(size: int<MB>, N : int, Q : int -> int -> float32, dJF : LRF, parallelize : bool) =
        let mutable N = N
        let mutable D = Array.init N (fun i -> Q i i)
        let lru = LRU(size, N, Q, parallelize)

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
        let W = Array.zeroCreate<float> parameters.dimensions

        let mutable X' = Array.empty<(* Y *) 'Y[] * (* Loss *) float[]>

        let dJF = LRF(100, N, (fun k i -> let Y' = fst X'.[k] in (JF X.[i] Y.[i]) - (JF X.[i] Y'.[i])), options.parallelize)

        let mu L = match options.rescaling with | Slack -> L | Margin -> 1.0
        
        let inline addToW (W : float[]) k a =
            if a <> 0.0 then
                let L_k = snd X'.[k]
                let dJF_k = dJF.[k]
                for i = 0 to L_k.Length - 1 do
                    let mu_i = mu L_k.[i]
                    let indices = dJF_k.[i].Indices
                    let values = dJF_k.[i].Values
                    for n = 0 to indices.Length-1 do
                        let j = indices.[n]
                        W.[j] <- W.[j] + a * mu_i * (float values.[n])
        
        let H k k' =
            // W_k = \sum\limits_{i=1}^n \mu_i \delta \Psi_i(y_i^k)[j]
            let W_k = Array.zeroCreate<float> parameters.dimensions
            addToW W_k k 1.0

            // H = \sum\limits_{i=1}^n \mu_i \sum\limits_{j=1}^N W_k[j] \delta\Psi_i(y_i^{k'})[j] / n^2
            let mutable sum = 0.0
            let L_k' = snd X'.[k']
            let dJF_k' = dJF.[k']
            for i = 0 to L_k'.Length-1 do
                let mu_i = mu L_k'.[i]
                let mutable sum_i = 0.0
                let indices = dJF_k'.[i].Indices
                let values = dJF_k'.[i].Values
                for n = 0 to indices.Length-1 do
                    sum_i <- sum_i + W_k.[indices.[n]] * (float values.[n])
                sum <- sum + mu_i * sum_i

            DivideByInt (float32 sum) (N*N)

        let M = int (1.0 / options.epsilon)
        let Q = Q_S(options.SMO.cacheSize, 0, H, dJF, options.SMO.parallelize)

        let inline reconstructW (A : float[]) =
            Array.fill W 0 W.Length 0.0
            for k = 0 to A.Length-1 do
                addToW W k (DivideByInt A.[k] N)

        let inline xi () =
            let xi_k k =
                let L_k = snd X'.[k]
                let dJF_k = dJF.[k]
                let mutable sum = 0.0
                for i = 0 to L_k.Length-1 do
                    let mu_i = mu L_k.[i]
                    let indices = dJF_k.[i].Indices
                    let values = dJF_k.[i].Values
                    let mutable sum_i = 0.0
                    for n = 0 to indices.Length-1 do
                        sum_i <- sum_i + W.[indices.[n]] * (float values.[n])
                    sum <- sum + (L_k.[i] - mu_i * sum_i)
                DivideByInt sum N

            if (Array.isEmpty X') then
                System.Double.NegativeInfinity
            else
                (if options.parallelize then Array.Parallel.init else Array.init) X'.Length xi_k
                |> Array.max

        let inline solve X' Y' A C p = 
            logger { verbose(sprintf "A=%A" A) }
            logger { verbose(sprintf "C=%A" C) }
            logger { verbose(sprintf "p=%A" p) }
            logger { verbose(sprintf "H=%A" (Array2D.init (Array.length A) (Array.length A) H)) }

            if not (Array.isEmpty X') then
                SMO.C_SMO X' Y' Q { A = A; C = C; p = p } 
                          { options.SMO with epsilon = options.epsilon * 2.0 } |> ignore

                reconstructW A

            xi ()

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

        let rec optimize (k : int) (Y' : float32[]) (A : float[]) (C : float[]) (p : float[]) =
            logger { debug (sprintf "iteration = %d" k) }

            let xi = solve X' Y' A C p

            logger { debug (sprintf "A=%A" A) }

            let x_k,h = newConstraint ()

            let xi_k = DivideByInt (float (Array.sum h)) N

            if not (isOptimal xi xi_k) then
                X' <- append X' x_k
                let Y' = append Y' 1.0f
                let C = append C parameters.C
                let A = append A (parameters.C - Array.sum A)
                let p = append p (DivideByInt (float -(Array.sum (snd x_k))) N)
                Q.Resize (Array.length X')

                optimize (k+1) Y' A C p
            else
                logger { info (sprintf "iteration=%d, xi=%f" k xi)}

        optimize (* k *) 0 (* Y' *) Array.empty (* A *) Array.empty (* p *) Array.empty (* C *) Array.empty

        W
