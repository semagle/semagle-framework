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

open LanguagePrimitives

open Logary

open Semagle.MachineLearning.SVM.LRU

/// Implementation of Sequential Minimal Optimization (SMO) algorithm
module SMO =
    [<Literal>]
    let private tau = 1e-12f

    [<Literal>]
    let private not_found = -1

    [<Literal>]
    let private shrinking_iterations = 1000

    /// Interface of Q matrix
    type Q =
        /// Swap column elements
        abstract member Swap : int -> int -> unit

        /// Returns N elements of the main diagonal of Q matrix
        abstract member D : float32[]

        /// Returns L elements of j-th column of Q matrix
        abstract member C : int -> int -> float32[]

    /// Working set selection strategy
    type WSSStrategy = MaximalViolatingPair | SecondOrderInformation

    /// Optimization options of SMO algorithm
    type OptimizationOptions = {
        /// The maximum optimization error
        epsilon : float32;
        /// The maximum number of SMO algorithm iterations
        maxIterations : int; 
        /// The working set selection strategy
        strategy : WSSStrategy; 
        /// Enable/disable working set shrinking
        shrinking : bool; 
        /// Kernel cache size
        cacheSize : int<MB>;
        /// Parallelize kernel evaluations
        parallelize : bool;
        /// Logger
        logger : Logger;
    }

    // default optimization options
    let defaultOptimizationOptions : OptimizationOptions =
        { epsilon = 0.001f; maxIterations = 1000000;
          strategy = SecondOrderInformation;  shrinking = true; 
          cacheSize = 200<MB>; parallelize = true; 
          logger = Logging.getCurrentLogger() }

    /// General parameters for C_SMO problem
    type C_SMO = { 
        /// Initial feasible values of optimization varibles
        A : float32[];
        /// Per-sample penalties 
        C : float32[];
        /// The linear term of the optimized function 
        p: float32[]; 
    }

    /// Sequential Minimal Optimization (SMO) problem solver
    let C_SMO (X : 'X[]) (Y : float32[]) (Q : Q) (parameters : C_SMO) (options : OptimizationOptions) = 
        if Array.length X <> Array.length Y then
            invalidArg "X and Y" "have different lengths"

        let log msg = msg |> Logger.logSimple options.logger
        let info fmt = Printf.kprintf (fun s -> s |> Logary.Message.eventInfo |> log) fmt

        let epsilon = options.epsilon
        let C = parameters.C
        let p = parameters.p

        let N = Array.length X
        let A = parameters.A

        let G = Array.copy p
        let G' = Array.zeroCreate<float32> N 

        let initialize_gradient =
            for i = 0 to N-1 do
                if A.[i] > 0.0f then
                    let Q_i = Q.C i N
                    let inline updateG (a_i : float32) (G : float32[]) = 
                        for j = 0 to N-1 do
                            G.[j] <- G.[j] + a_i*Q_i.[j]

                    updateG A.[i] G

                    if A.[i] >= C.[i] then updateG C.[i] G'

        // working set selection helper functions
        let inline _y_gf i = -G.[i]*Y.[i]

        let inline isFree i = 0.0f < A.[i] && A.[i] < C.[i]

        let inline isUp i = (Y.[i] = +1.0f && A.[i] < C.[i]) || (Y.[i] = -1.0f && A.[i] > 0.0f)

        let inline isLow i = (Y.[i] = +1.0f && A.[i] > 0.0f) || (Y.[i] = -1.0f && A.[i] < C.[i])

        let inline maxUp n =
            let mutable max_i = not_found
            let mutable max_v = System.Single.NegativeInfinity
            for i = 0 to n-1 do
                if isUp i then
                    let v = _y_gf i
                    if v > max_v then
                        max_i <- i
                        max_v <- v
            max_i

        let inline minLow n =
            let mutable min_i = not_found
            let mutable min_v = System.Single.PositiveInfinity
            for i = 0 to n-1 do
                if isLow i then
                    let v = _y_gf i
                    if v < min_v then
                        min_i <- i
                        min_v <- v
            min_i

        let inline minLowTo s n =
            let Q_s = Q.C s n
            let inline objective t = 
                let a_ts = Q.D.[t] + (Q_s.[s]) - 2.0f*(Q_s.[t])*Y.[t]*Y.[s]
                let b_ts = _y_gf t - _y_gf s
                -b_ts*b_ts/(if a_ts > 0.0f then a_ts else tau)

            let mutable min_i = not_found
            let mutable min_v = System.Single.PositiveInfinity
            for i = 0 to n-1 do
                if (isLow i) && (_y_gf i < _y_gf s) then
                    let v = objective i
                    if v < min_v then
                        min_i <- i
                        min_v <- v
            min_i
        
        /// Maximal violating pair working set selection strategy
        let maximalViolatingPair n =
            let i = maxUp n
            if i <> not_found then
                let j = minLow n
                if j <> not_found && _y_gf(i) > _y_gf(j) then
                    Some(i, j)
                else
                    None
            else
                None

        /// Second order information working set selection strategy 
        let secondOrderInformation n = 
            let i = maxUp n
            if i <> not_found then
                let j = minLowTo i n
                if j <> not_found && _y_gf(i) > _y_gf(j) then
                    Some(i, j)
                else
                    None
            else
                None

        let selectWorkingSet =
            match options.strategy with
                | MaximalViolatingPair -> maximalViolatingPair
                | SecondOrderInformation -> secondOrderInformation       

        /// Solve an optimization sub-problem
        let inline solve i j n = 
            let Q_i = Q.C i n
            let Q_j = Q.C j n
            let a = (Q_i.[i]) + (Q_j.[j]) - 2.0f*(Q_i.[j])*Y.[i]*Y.[j]
            let a' = if a > 0.0f then a else tau
            if Y.[i] <> Y.[j] then
                let delta = (-G.[i]-G.[j]) / a'
                let diff = A.[i] - A.[j]
                match (A.[i] + delta, A.[j] + delta) with
                    | _, a_j when diff > 0.0f && a_j < 0.0f -> (diff, 0.0f)
                    | a_i, _ when diff <= 0.0f && a_i < 0.0f -> (0.0f, diff)
                    | _, a_j when diff <= C.[i] - C.[j] && a_j > C.[j] -> (C.[j] + diff, C.[j])
                    | a_i, _ when diff > C.[i] - C.[j] && a_i > C.[i] -> (C.[i], C.[i] - diff)
                    | a_i, a_j -> a_i, a_j
            else
                let delta = (G.[i]-G.[j]) / a'
                let sum = A.[i] + A.[j]
                match (A.[i] - delta, A.[j] + delta) with
                    | a_i, _ when sum > C.[i] && a_i > C.[i] -> (C.[i], sum - C.[i])
                    | _, a_j when sum <= C.[i] && a_j < 0.0f -> (sum, 0.0f)
                    | _, a_j when sum > C.[j] && a_j > C.[j] -> (sum - C.[j], C.[j])
                    | a_i, _ when sum <= C.[j] && a_i < 0.0f -> (0.0f, sum)
                    | a_i, a_j -> a_i, a_j

        /// update gradient
        let inline updateG i j a_i a_j n =
            let Q_i = Q.C i n
            let Q_j = Q.C j n

            for t = 0 to n-1 do
                G.[t] <- G.[t] + (Q_i.[t])*(a_i - A.[i]) + (Q_j.[t])*(a_j - A.[j])

        let inline updateG' i a =
            let sign = match a, A.[i] with
                       | _ when a = C.[i] && A.[i] <> C.[i] -> +1.0f
                       | _ when a <> C.[i] && A.[i] = C.[i] -> -1.0f
                       | _ -> 0.0f

            if sign <> 0.0f then
                let Q_i = Q.C i N
                let C_i = C.[i]
                for t = 0 to N-1 do
                    G'.[t] <- G'.[t] + sign * C_i*Q_i.[t]

        /// reconstruct gradient
        let inline reconstructG n = 
            for t = n to N-1 do
                G.[t] <- G'.[t] + p.[t]

            let mutable free = 0
            for i = 0 to n-1 do
                if isFree i then free <- free + 1

            let inline passive_active () =
                for i = n to N-1 do
                    let Q_i = Q.C i n
                    for j = 0 to n-1 do
                        if isFree j then 
                            G.[i] <- G.[i] + A.[j]*Q_i.[j]

            let inline active_passive () =
                for i = 0 to n-1 do
                    if isFree i then
                        let Q_i = Q.C i N
                        for j = n to N-1 do
                            G.[j] <- G.[j] + A.[i]*Q_i.[j]

            if free*n > 2*n*(N-n) then
                passive_active ()
            else
                active_passive ()

        let inline m n = 
            let mutable max_v = System.Single.NegativeInfinity
            for i = 0 to n-1 do
                if isUp i then
                    let v = _y_gf i
                    if v > max_v then
                        max_v <- v
            max_v

        let inline M n =
            let mutable min_v = System.Single.PositiveInfinity
            for i = 0 to n-1 do
                if isLow i then
                    let v = _y_gf i
                    if v < min_v then
                        min_v <- v
            min_v

        /// shrink active set
        let inline shrink m M n = 
            let inline isShrinked i = 
                (_y_gf i) > m && A.[i] = C.[i] && Y.[i] = +1.0f || A.[i] = 0.0f && Y.[i] = -1.0f ||
                (_y_gf i) < M && A.[i] = 0.0f && Y.[i] = +1.0f || A.[i] = C.[i] && Y.[i] = -1.0f

            let inline swapAll i j =
                swap X i j; swap Y i j
                swap C i j; swap p i j;
                swap A i j; swap G i j; 
                swap G' i j; Q.Swap i j

            let mutable i = 0
            let mutable n' = n

            while i < n' do
                if isShrinked i then
                    n' <- n' - 1 
                    while i < n' && isShrinked n' do
                        n' <- n' - 1
                    swapAll i n'
                i <- i + 1    
            n'

        let inline isOptimal m M epsilon = 
            let diff = abs (m - M)
            diff <= epsilon || diff <= epsilon * (min (abs m) (abs M))

        /// Sequential Minimal Optimization (SMO) Algorithm
        let inline optimize_solve n =
            // Find a pair of elements that violate the optimality condition
            match selectWorkingSet n with
                | Some (i, j) -> 
                    // Solve the optimization sub-problem
                    let a_i, a_j = solve i j n
                    // Update the gradient
                    updateG i j a_i a_j n
                    if options.shrinking then
                        updateG' i a_i
                        updateG' j a_j
                    // Update the solution
                    A.[i] <- a_i; A.[j] <- a_j
                    true
                | None -> false

        /// optimize with shrinking every 1000 iterations
        let rec optimize_shrinking k s n unshrinked =
            let inline optimize_shrink m M n =
                if s = 0 then
                    // time to shrink
                    if not(unshrinked) && isOptimal m M (10.0f*epsilon) then
                       // reconstruct G if (M - m) <= 10*epsilon for the first time
                       reconstructG n
                       // shrink the full set
                       true, shrink m M N
                    else
                        // shrink a subset
                        unshrinked, shrink m M n
                else
                    // no time to shrink
                    unshrinked, n

            if k < options.maxIterations then
                let m_k = m n
                let M_k = M n

                // shrink active set
                let unshrinked, n = optimize_shrink m_k M_k n

                // check optimality for active set
                if isOptimal m_k M_k epsilon then
                    reconstructG n

                    // check optimality for full set
                    if not(isOptimal (m N) (M N) epsilon) && optimize_solve N then
                        // shrink on next iteration
                        optimize_shrinking (k + 1) 1 N unshrinked
                    else
                        k
                else
                    if optimize_solve n then 
                        optimize_shrinking (k + 1) (if s > 0 then (s - 1) else shrinking_iterations) n unshrinked
                    else 
                        k
             else
                failwith "Exceeded iterations limit"

        /// optimize without shrinking every 1000 iterations
        let rec optimize_non_shrinking k =
            if k < options.maxIterations then
                let m_k = m N
                let M_k = M N

                if not (isOptimal m_k M_k epsilon) && optimize_solve N then
                    optimize_non_shrinking (k + 1)
                else
                    k
            else
                failwith "Exceeded iterations limit"

        initialize_gradient

        let iterations = 
            if options.shrinking then 
                optimize_shrinking 0 shrinking_iterations N false
            else
                optimize_non_shrinking 0
        info "#iterations = %d" iterations

        /// Reconstruction of hyperplane bias
        let bias =
            let mutable b = 0.0f
            let mutable M = 0
            for i = 0 to N-1 do
                if isFree i then
                    b <- b + _y_gf i
                    M <- M + 1
            DivideByInt b M

        (X,Y,A,bias)

    /// Q matrix for classification problems
    type private Q_C(capacity : int, N : int, Q : int -> int -> float32, parallelize : bool) =
        let diagonal = Array.init N (fun i -> Q i i)
        let lru = LRU(capacity, N, Q, parallelize)

        interface Q with
            /// Swap column elements
            member q.Swap (i : int) (j : int) = 
                lru.Swap i j
                swap diagonal i j

            /// Returns N elements of the main diagonal of Q matrix
            member q.D = diagonal

            /// Returns L elements of j-th column of Q matrix
            member q.C (j : int) (L : int) = lru.Get j L

    /// Optimization parameters for C_SVC problem 
    type C_SVC = {
        /// The penalty for +1 class instances 
        C_p : float32;
        /// The penalty for -1 class instances 
        C_n : float32; 
    }

    /// Two class C Support Vector Classification (SVC) problem solver
    let C_SVC (X : 'X[]) (Y : float32[]) (K : Kernel<'X>) (parameters : C_SVC) (options : OptimizationOptions) =
        let N = Array.length X
        let X' = Array.copy X
        let Y' = Array.copy Y
        let C = Array.init N (fun i -> if Y.[i] = +1.0f then parameters.C_p else parameters.C_n )
        let p = Array.create N -1.0f
        let A = Array.zeroCreate N

        let Q = new Q_C(LRU.capacity options.cacheSize N, N, 
                        (fun i j -> (K X'.[i] X'.[j])*Y'.[i]*Y'.[j]), options.parallelize)
        let (X',Y',A',b) = C_SMO X' Y' Q { A = A; C = C; p = p } options

        // Remove support vectors with A.[i] = 0.0 and compute Y.[i]*A.[i]
        let N'' = Array.sumBy (fun a -> if a <> 0.0f then 1 else 0) A'
        let X'' = Array.zeroCreate N''
        let A'' = Array.zeroCreate N''

        let mutable k = 0
        for i = 0 to N-1 do
            if A'.[i] <> 0.0f then
                X''.[k] <- X'.[i]
                A''.[k] <- Y'.[i]*A'.[i]
                k <- k + 1

        TwoClass(K,X'',A'',b)

    /// Multi-class C Support Vector Classification (SVC) problem solver
    let C_SVC_M (X : 'X[]) (Y : 'Y[]) (K : Kernel<'X>) (parameters : C_SVC) (options : OptimizationOptions) =
        let models = 
            seq { 
                let S = Array.distinct Y
                for i = 0 to (Array.length S)-2 do
                    for j = i+1 to (Array.length S)-1 do
                        yield (S.[i], S.[j]) }
            |> Seq.toArray
            |> (if options.parallelize then Array.Parallel.map else Array.map) (fun (y', y'') ->
                let X',Y' = 
                    Array.zip X Y 
                    |> Array.filter (fun (_, y) -> y = y' || y = y'')
                    |> Array.map (fun (x, y) -> (x, if y = y' then +1.0f else -1.0f))
                    |> Array.unzip
                match C_SVC X' Y' K parameters options with
                | TwoClass(_, X, A,b) -> (y', y'', X, A, b)
                | _ -> invalidArg "svm" "type is invalid")
        MultiClass(K, models)

    /// Optimization parameters for One-Class problem
    type OneClass = {
        /// The fraction of support vectors 
        nu : float32;
    }

    /// One-Class problem solver
    let OneClass (X : 'X[]) (K : Kernel<'X>) (parameters : OneClass) (options : OptimizationOptions) = 
        let N = (Array.length X)
        let X' = Array.copy X
        let Y = Array.create N 1.0f
        let C = Array.create N 1.0f
        let p = Array.create N 0.0f
        let n = int (parameters.nu * (float32 N))
        let A = Array.init N (fun i -> 
            match i with 
            | _ when i < n -> 1.0f
            | _ when i > n -> 0.0f
            | _ -> parameters.nu * (float32 N) - (float32 n))

        let Q = new Q_C(LRU.capacity options.cacheSize N, N, 
                        (fun i j -> K X'.[i] X'.[j]), options.parallelize)
        let (X',_,A',b) = C_SMO X' Y Q { A = A; C = C; p = p } options

        // Remove support vectors with A.[i] = 0.0
        let N'' = Array.sumBy (fun a -> if a <> 0.0f then 1 else 0) A'
        let X'' = Array.zeroCreate N''
        let A'' = Array.zeroCreate N''

        let mutable k = 0
        for i = 0 to N-1 do
            if A'.[i] <> 0.0f then
                X''.[k] <- X'.[i]
                A''.[k] <- A'.[i]
                k <- k + 1

        OneClass(K,X'',A'',b)

    /// Q matrix for regression problems
    type private Q_R(capacity : int, N : int, Q : int -> int -> float32, parallelize : bool) =
        let diagonal = Q_R.initDiagonal N Q
        let indices = Array.init (2*N) id
        let lru = LRU(capacity, 2*N, Q, parallelize)

        /// Initialize elements of the main diagonal of Q
        static member initDiagonal N Q =
            let D = Array.zeroCreate (2*N)
            for i = 0 to N-1 do
                let q = Q i i
                D.[i] <- q
                D.[i+N] <- q
            D

        /// Returns permutated indices
        member q.I = indices

        interface Q with
            /// Swap column elements
            member q.Swap (i : int) (j : int) = 
                lru.Swap i j
                swap diagonal i j
                swap indices i j

            /// Returns N elements of the main diagonal of Q matrix
            member q.D = diagonal

            /// Returns L elements of j-th column of Q matrix
            member q.C (j : int) (L : int) = lru.Get j L 


    /// Optimization parameters for C_SVR problem
    type C_SVR = {
        /// The boundary of the approximated function
        eta : float32;
        /// The penalty 
        C : float32;
    }

    /// C Support Vector Regression (SVR) problem solver
    let C_SVR (X : 'X[]) (Y : float32[]) (K : Kernel<'X>) (parameters : C_SVR) (options : OptimizationOptions) =
        let N = Array.length X
        let N' = 2 * N
        let Y' = Array.init N' (fun i -> if i < N then +1.0f else -1.0f)
        let X' = Array.init N' (fun i -> if i < N then X.[i] else X.[i-N])
        let C' = Array.create N' parameters.C
        let p' = Array.init N' (fun i -> parameters.eta - (if i < N then Y.[i] else -Y.[i-N]))
        let A' = Array.zeroCreate N'

        let Q = new Q_R(LRU.capacity options.cacheSize (2*N), N, 
                        (fun i j -> (K X'.[i] X'.[j])*Y'.[i]*Y'.[j]), options.parallelize)
        let (X',_,A',b) = C_SMO X' Y' Q { A = A'; C = C'; p = p' } options

        // Compute -A.[i] + A.[i+N]
        let A = Array.zeroCreate N
        for k = 0 to (Array.length Q.I)-1 do
            let i = Q.I.[k]
            if i < N then
                A.[i] <- A.[i] + A'.[k]
            else
                A.[i-N] <- A.[i-N] - A'.[k]

        // Remove support vectors with A.[i] = 0.0
        let N'' = Array.sumBy (fun a -> if a <> 0.0f then 1 else 0) A
        let A'' = Array.zeroCreate N''
        let X'' = Array.zeroCreate N''

        let mutable k = 0
        for i = 0 to N - 1 do
            if A.[i] <> 0.0f then
                A''.[k] <- A.[i]
                X''.[k] <- X.[i]
                k <- k + 1

        Regression(K,X'',A'',b)
