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

open System.Threading.Tasks

open Logary
open Hopac

open Semagle.Numerics.Vectors
open Semagle.MachineLearning.SVM
open Semagle.MachineLearning.SVM.LRU

module OneSlack = 
    type OneSlack<'X,'Y> = { 
        rescaling : Rescaling; 
        C : float32;  
        epsilon : float32;
        loss : LossFunction<'Y>;
        argmaxLoss : ArgmaxLossFunction<'X,'Y>; 
        options : SMO.OptimizationOptions
    }

    /// Q matrix for structured problems
    type private Q_S(capacity : int, N : int, Q : int -> int -> float32) =
        let mutable N = N
        let mutable inactive = Array.zeroCreate N
        let mutable diagonal = Array.init N (fun i -> Q i i)
        let lru = LRU(capacity, N, Q)

        member this.Resize (n : int) =
            lru.Resize n
            if n > N then
                let diagonal' = Array.zeroCreate n
                Array.blit diagonal 0 diagonal' 0 N
                Parallel.For(N, n, (fun i -> diagonal'.[i] <- Q i i)) |> ignore
                diagonal <- diagonal'
            else
                diagonal <- diagonal.[..n-1]
            N <- n

        interface SMO.Q with
            /// Swap column elements
            member q.Swap (i : int) (j : int) = 
                lru.Swap i j
                swap diagonal i j

            /// Returns N elements of the main diagonal of Q matrix
            member q.D = diagonal

            /// Returns L elements of j-th column of Q matrix
            member q.C (j : int) (L : int) = lru.Get j L

    let optimize (X : 'X[]) (Y : 'Y[]) (F : JointFeatureFunction<'X, 'Y>) (parameters : OneSlack<'X,'Y>) =
        if Array.length X <> Array.length Y then
            invalidArg "X and Y" "have different lengths"

        let logger = Logging.getCurrentLogger()
        let log msg = msg |> Logary.Logger.log logger |> Hopac.start
        let info fmt = Printf.kprintf (fun s -> s |> Logary.Message.eventInfo |> log) fmt

        let N = Array.length X

        let mutable X' = Array.zeroCreate<(* Y *) 'Y[] * (* L *) float32[] * (* dF *) SparseVector[]> 0
        let mutable Y' = Array.zeroCreate<float32> 0
        let mutable p = Array.zeroCreate<float32> 0
        let mutable C = Array.zeroCreate<float32> 0
        let mutable A = Array.zeroCreate<float32> 0
        let mutable W = DenseVector([||])

        let inline update (W : DenseVector) k a =
            if a <> 0.0f then
                let W = W.Values
                let a = a / (float32 N)
                let (_, L, dF) = X'.[k]
                Array.iteri2 (fun i (L : float32) (dF : SparseVector) -> 
                    let m = match parameters.rescaling with | Slack -> L | Margin -> 1.0f
                    Array.iter2 (fun j v -> W.[j] <- W.[j] + a * m * v) dF.Indices dF.Values) L dF

        let H k l = 
            let sumF k =
                let sumF = DenseVector(Array.zeroCreate W.Length)
                update sumF k 1.0f
                sumF
            (sumF k) .* (sumF l)

        let M = int (1.0f / parameters.epsilon)
        let Q = new Q_S(LRU.capacity parameters.options.cacheSize M, 0, H)

        let inline solve () = 
            SMO.C_SMO X' Y' Q { epsilon = parameters.epsilon / 2.0f; 
                                A = A; C = C; p = p;
                                options = parameters.options } |> ignore

            W <- DenseVector(Array.zeroCreate W.Length)
            Array.iteri (fun k a -> update W k a) A

            if not (Array.isEmpty X') then
                X' |> Array.map (fun (_, L, dF) ->
                    let H = Array.Parallel.init N (fun i ->
                        let m = match parameters.rescaling with | Slack -> L.[i] | Margin -> 1.0f
                        L.[i] - m * (W .* dF.[i]))
                    (Array.sum H) / (float32 N))
                |> Array.max
                |> max 0.0f
            else
                0.0f

        let inline findNewConstraint () =
            let Y = Array.zeroCreate N
            let L = Array.zeroCreate N
            let DF = Array.zeroCreate N
            let H = Array.zeroCreate N
            let argmaxLoss = parameters.argmaxLoss (OneSlack(F, W)) parameters.loss
            Parallel.For(0, N, (fun i -> 
                let y, l, dF, h = argmaxLoss i 
                Y.[i] <- y; L.[i] <- l; DF.[i] <- dF; H.[i] <- h)) |> ignore
            (Y, L, DF), H

        let inline append (a : 'A[]) (e: 'A) =
            let M = Array.length a
            let b = Array.zeroCreate<'A> (M+1)
            Array.blit a 0 b 0 M
            b.[M] <- e
            b

        let inline resizeW (DF : SparseVector[]) =
            let M = DF |> Array.fold (fun m dF -> 
                max m (if not (Array.isEmpty dF.Indices) then Array.last dF.Indices else 0)) 0
            if W.Length < (M+1) then
                let W' = Array.zeroCreate (M+1)
                Array.blit W.Values 0 W' 0 W.Length
                W <- DenseVector(W')

        let inline isOptimal xi h = 
            let h = (Array.sum h) / (float32 N)
            info "xi=%f, h=%f" xi h
            h - xi <= parameters.epsilon

        let rec optimize k =
            info "iteration=%d, size=%d" k (Array.length X')

            let xi = solve ()
            let x', h = findNewConstraint ()
            X' <- append X' x' 
            Y' <- append Y' 1.0f
            C <- append C parameters.C
            A <- append A (parameters.C - Array.sum A)
            match x' with | (_, L, DF) -> p <- append p (-(Array.sum L)/(float32 N)); resizeW DF
            Q.Resize (Array.length X')

            if (isOptimal xi h) then
                k
            else
                optimize (k+1)

        let iterations = optimize 1
        info "iterations = %d" iterations

        OneSlack(F, W)