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

open Semagle.Logging
open Semagle.MachineLearning.SVM.LRU

/// Unit of measure for cache size
[<Measure>] type MB

/// Implementation of Sequential Minimal Optimization (SMO) algorithm
module SMO =
    [<Literal>]
    let private tau = 1e-12f

    [<Literal>]
    let private not_found = -1

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
        epsilon : float;
        /// The maximum number of SMO algorithm iterations
        maxIterations : int;
        /// The working set selection strategy
        strategy : WSSStrategy;
        /// Enable/disable working set shrinking
        shrinking : bool;
        /// Maximum number of iterations before shrinking
        shrinkingIterations : int;
        /// Kernel cache size
        cacheSize : int<MB>;
        /// Parallelize kernel evaluations
        parallelize : bool
    }

    /// Default optimization options
    let defaultOptimizationOptions : OptimizationOptions =
        { epsilon = 0.001; maxIterations = 1000000;
          strategy = SecondOrderInformation;
          shrinking = true; shrinkingIterations = 1000;
          cacheSize = 100<MB>; parallelize = true }

    /// Returns the required capacity for the specified cache size and column length
    let capacity (cacheSize : int<MB>) (length : int) =
       let columnSize = sizeof<float32>*length + sizeof<int> + sizeof<float32[]>
       max 2 ((int cacheSize)*1024*1024 / columnSize)

    /// General parameters for C_SMO problem
    type C_SMO = {
        /// Initial feasible values of optimization varibles
        A : float[];
        /// Per-sample penalties
        C : float[];
        /// The linear term of the optimized function
        p: float[];
    }

    /// Sequential Minimal Optimization (SMO) problem solver
    let C_SMO (X : 'X[]) (Y : float32[]) (Q : Q) (parameters : C_SMO) (options : OptimizationOptions) =
        if Array.length X <> Array.length Y then
            invalidArg "X and Y" "have different lengths"

        let logger = LoggerBuilder(Log.create "C_SMO")

        let epsilon = options.epsilon
        let C = parameters.C
        let p = parameters.p

        let N = Array.length X
        let A = parameters.A

        let G = Array.copy p
        let G' = Array.zeroCreate<float> N

        // working set selection helper functions
        let inline _y_gf i = -G.[i]*(float Y.[i])

        let inline isFree i = 0.0 < A.[i] && A.[i] < C.[i]

        let inline isUp i = (Y.[i] = +1.0f && A.[i] < C.[i]) || (Y.[i] = -1.0f && A.[i] > 0.0)

        let inline isLow i = (Y.[i] = +1.0f && A.[i] > 0.0) || (Y.[i] = -1.0f && A.[i] < C.[i])

        let inline maxUp n =
            let mutable max_i = not_found
            let mutable max_v = System.Double.NegativeInfinity
            for i = 0 to n-1 do
                if isUp i then
                    let v = _y_gf i
                    if v >= max_v then
                        max_i <- i
                        max_v <- v
            max_i

        let inline minLow n =
            let mutable min_j = not_found
            let mutable min_v = System.Double.PositiveInfinity
            for j = 0 to n-1 do
                if isLow j then
                    let v = _y_gf j
                    if v <= min_v then
                        min_j <- j
                        min_v <- v
            min_j

        let inline minLowTo i n =
            let Q_s = Q.C i n
            let inline objective j =
                let a = max (Q.D.[j] + Q.D.[i] - 2.0f*Q_s.[j]*Y.[j]*Y.[i]) tau
                let b = _y_gf j - _y_gf i
                -b*b / (float a)

            let mutable min_j = not_found
            let mutable min_v = System.Double.PositiveInfinity
            for j = 0 to n-1 do
                if (isLow j) && (_y_gf j < _y_gf i) then
                    let v = objective j
                    if v <= min_v then
                        min_j <- j
                        min_v <- v
            min_j

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

            let a = max (Q.D.[i] + Q.D.[j] - 2.0f*Q_i.[j]*Y.[i]*Y.[j]) tau

            if Y.[i] <> Y.[j] then
                let delta = (-G.[i]-G.[j]) / (float a)
                let diff = A.[i] - A.[j]
                match (A.[i] + delta, A.[j] + delta) with
                    | _, a_j when diff > 0.0 && a_j < 0.0 -> (diff, 0.0)
                    | a_i, _ when diff <= 0.0 && a_i < 0.0 -> (0.0, -diff)
                    | a_i, _ when diff > C.[i] - C.[j] && a_i > C.[i] -> (C.[i], C.[i] - diff)
                    | _, a_j when diff <= C.[i] - C.[j] && a_j > C.[j] -> (C.[j] + diff, C.[j])
                    | a_i, a_j -> a_i, a_j
            else
                let delta = (G.[i]-G.[j]) / (float a)
                let sum = A.[i] + A.[j]
                match (A.[i] - delta, A.[j] + delta) with
                    | a_i, _ when sum > C.[i] && a_i > C.[i] -> (C.[i], sum - C.[i])
                    | _, a_j when sum <= C.[i] && a_j < 0.0 -> (sum, 0.0)
                    | _, a_j when sum > C.[j] && a_j > C.[j] -> (sum - C.[j], C.[j])
                    | a_i, _ when sum <= C.[j] && a_i < 0.0 -> (0.0, sum)
                    | a_i, a_j -> a_i, a_j

        /// Initialize gradient
        let inline initialize_gradient () =
            for i = 0 to N-1 do
                if A.[i] > 0.0 then
                    let Q_i = Q.C i N
                    let inline updateG (a_i : float) (G : float[]) =
                        for j = 0 to N-1 do
                            G.[j] <- G.[j] + a_i*(float Q_i.[j])

                    updateG A.[i] G

                    if A.[i] >= C.[i] then
                        updateG C.[i] G'

        /// update gradient
        let inline update_gradient i a_i n =
            let isAddedBound = a_i >= C.[i] && A.[i] < C.[i]
            let isRemovedBound = a_i < C.[i] && A.[i] >= C.[i]
            let n' =
                if options.shrinking && (isAddedBound || isRemovedBound) then
                    N
                else
                    n
            let Q_i = Q.C i n'

            for t = 0 to n-1 do
                let Q_i_t = float Q_i.[t]
                G.[t] <- G.[t] + Q_i_t*a_i - Q_i_t*A.[i]

            if options.shrinking then
                let inline updateG' C =
                    for t = 0 to N-1 do
                        G'.[t] <- G'.[t] + C*(float Q_i.[t])

                if isAddedBound then
                    updateG' C.[i]
                else if isRemovedBound then
                    updateG' -C.[i]

        /// reconstruct gradient
        let inline reconstruct_gradient (G : float[]) n =
            for t = n to N-1 do
                G.[t] <- G'.[t] + p.[t]

            let mutable free = 0
            for t = 0 to n-1 do
                if isFree t then free <- free + 1

            if free*n > 2*n*(N-n) then
                // passive/active
                logger { verbose (sprintf "reconstruct gradient: passive = %d / active = %d" (N - n) n) }
                for i = n to N-1 do
                    let Q_i = Q.C i n
                    for j = 0 to n-1 do
                        if isFree j then
                            G.[i] <- G.[i] + A.[j]*(float Q_i.[j])
            else
                // active/passive
                logger { verbose (sprintf "reconstruct gradient: active = %d / passive = %d" n (N - n)) }
                for j = 0 to n-1 do
                    if isFree j then
                        let Q_j = Q.C j N
                        for i = n to N-1 do
                            G.[i] <- G.[i] + A.[j]*(float Q_j.[i])

        let inline m n =
            let mutable max_v = System.Double.NegativeInfinity
            for i = 0 to n-1 do
                if isUp i then
                    let v = _y_gf i
                    if v > max_v then
                        max_v <- v
            max_v

        let inline M n =
            let mutable min_v = System.Double.PositiveInfinity
            for i = 0 to n-1 do
                if isLow i then
                    let v = _y_gf i
                    if v < min_v then
                        min_v <- v
            min_v

        /// shrink active set
        let inline shrink m M n =
            let inline isShrinked i =
                (_y_gf i) > m && (A.[i] >= C.[i] && Y.[i] = +1.0f || A.[i] <= 0.0 && Y.[i] = -1.0f) ||
                (_y_gf i) < M && (A.[i] <= 0.0 && Y.[i] = +1.0f || A.[i] >= C.[i] && Y.[i] = -1.0f)

            let inline swapAll i j =
                swap X i j; swap Y i j
                swap C i j; swap p i j;
                swap A i j; swap G i j;
                swap G' i j; Q.Swap i j

            let mutable i = 0
            let mutable n' = n

            let mutable swaps = 0
            while i < n' do
                if isShrinked i then
                    n' <- n' - 1
                    while i < n' && isShrinked n' do
                        n' <- n' - 1
                    swaps <- swaps + 1
                    swapAll i n'
                i <- i + 1

            logger { verbose (sprintf "shrink active set: shrinked = %d, active = %d, swaps = %d" (n - n') n' swaps) }

            n'

        let inline isOptimal m M epsilon =
            let diff = abs (m - M)
            diff <= epsilon || diff <= epsilon * (min (abs m) (abs M))

        /// Sequential Minimal Optimization (SMO) Algorithm
        let inline optimize_solve n =
            // Find a pair of elements that violate the optimality condition
            match selectWorkingSet n with
                | Some (i, j) ->
                    logger { verbose(sprintf "working set = {%d, %d}" i j) }
                    // Solve the optimization sub-problem
                    let a_i, a_j = solve i j n
                    // Update the gradient
                    update_gradient i a_i n
                    update_gradient j a_j n
                    // Update the solution
                    A.[i] <- a_i; A.[j] <- a_j
                    true
                | None ->
                    logger { verbose(sprintf "working set = {}") }
                    false

        let objective n =
            let G =
                if n < N then
                    let G = Array.copy G
                    reconstruct_gradient G n
                    G
                else
                    G

            let mutable sum = 0.0
            for i = 0 to N-1 do
                sum <- sum + A.[i]*(G.[i] + p.[i])
            sum / 2.0

        /// optimize with shrinking every 1000 iterations
        let rec optimize_shrinking k s n unshrinked =
            let inline optimize_shrink m M n =
                if s = 0 then
                    // time to shrink
                    if not(unshrinked) && isOptimal m M (10.0*epsilon) then
                       // reconstruct G if (M - m) <= 10*epsilon for the first time
                       reconstruct_gradient G n
                       // shrink the full set
                       true, shrink m M N
                    else
                        // shrink a subset
                        unshrinked, shrink m M n
                else
                    // no time to shrink
                    unshrinked, n

            if k <= options.maxIterations then
                if k % 1000 = 0 then
                    logger { debug (sprintf "iteration = %d, objective = %f" k (objective n)) }

                let m_k = m n
                let M_k = M n

                // shrink active set
                let unshrinked, n = optimize_shrink m_k M_k n

                // check optimality for active set
                if isOptimal m_k M_k epsilon then
                    reconstruct_gradient G n

                    // check optimality for full set
                    if not(isOptimal (m N) (M N) epsilon) && optimize_solve N then
                        // shrink on next iteration
                        optimize_shrinking (k + 1) 1 N unshrinked
                    else
                        logger { info (sprintf "iteration = %d, objective = %f" k (objective N)) }
                else
                    if optimize_solve n then
                        let s = if s > 0 then (s - 1) else options.shrinkingIterations
                        optimize_shrinking (k + 1) s n unshrinked
                    else
                        logger { info ((sprintf "iteration = %d, objective = %f" k (objective n))) }
             else
                failwith "Exceeded iterations limit"

        /// optimize without shrinking every 1000 iterations
        let rec optimize_non_shrinking k =
            if k <= options.maxIterations then
                if k % 1000 = 0 then
                    logger { debug (sprintf "iteration = %d, objective = %f" k (objective N)) }

                let m_k = m N
                let M_k = M N

                if not (isOptimal m_k M_k epsilon) && optimize_solve N then
                    optimize_non_shrinking (k + 1)
                else
                    logger { info (sprintf "iteration = %d, objective = %f" k (objective N)) }
            else
                failwith "Exceeded iterations limit"

        initialize_gradient ()

        if options.shrinking then
            optimize_shrinking 1 options.shrinkingIterations N false
        else
            optimize_non_shrinking 1

        logger { info (let mutable support = 0 in
                       let mutable bounded = 0 in
                       for i = 0 to N-1 do
                           if A.[i] <> 0.0 then
                               support <- support + 1
                               if A.[i] >= C.[i] then
                                   bounded <- bounded + 1
                       sprintf "support vectors = %d, bounded = %d" support bounded) }

        /// Reconstruction of hyperplane bias
        let bias =
            let mutable b = 0.0
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
        C_p : float;
        /// The penalty for -1 class instances
        C_n : float;
    }

    /// Two class C Support Vector Classification (SVC) problem solver
    let C_SVC (X : 'X[]) (Y : float32[]) (K : Kernel<'X>) (parameters : C_SVC) (options : OptimizationOptions) =
        let N = Array.length X
        let X' = Array.copy X
        let Y' = Array.copy Y
        let C = Array.init N (fun i -> if Y.[i] = +1.0f then parameters.C_p else parameters.C_n )
        let p = Array.create N -1.0
        let A = Array.zeroCreate N

        let Q = new Q_C(capacity options.cacheSize N, N,
                        (fun i j -> (K X'.[i] X'.[j])*Y'.[i]*Y'.[j]), options.parallelize)
        let (X',Y',A',b) = C_SMO X' Y' Q { A = A; C = C; p = p } options

        // Remove support vectors with A.[i] = 0.0 and compute Y.[i]*A.[i]
        let N'' = Array.sumBy (fun a -> if a <> 0.0 then 1 else 0) A'
        let X'' = Array.zeroCreate N''
        let A'' = Array.zeroCreate N''

        let mutable k = 0
        for i = 0 to N-1 do
            if A'.[i] <> 0.0 then
                X''.[k] <- X'.[i]
                A''.[k] <- (float Y'.[i])*A'.[i]
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
        nu : float;
    }

    /// One-Class problem solver
    let OneClass (X : 'X[]) (K : Kernel<'X>) (parameters : OneClass) (options : OptimizationOptions) =
        let N = (Array.length X)
        let X' = Array.copy X
        let Y = Array.create N 1.0f
        let C = Array.create N 1.0
        let p = Array.create N 0.0
        let n = int (parameters.nu * (float N))
        let A = Array.init N (fun i ->
            match i with
            | _ when i < n -> 1.0
            | _ when i > n -> 0.0
            | _ -> parameters.nu * (float N) - (float n))

        let Q = new Q_C(capacity options.cacheSize N, N,
                        (fun i j -> K X'.[i] X'.[j]), options.parallelize)
        let (X',_,A',b) = C_SMO X' Y Q { A = A; C = C; p = p } options

        // Remove support vectors with A.[i] = 0.0
        let N'' = Array.sumBy (fun a -> if a <> 0.0 then 1 else 0) A'
        let X'' = Array.zeroCreate N''
        let A'' = Array.zeroCreate N''

        let mutable k = 0
        for i = 0 to N-1 do
            if A'.[i] <> 0.0 then
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
        eta : float;
        /// The penalty
        C : float;
    }

    /// C Support Vector Regression (SVR) problem solver
    let C_SVR (X : 'X[]) (Y : float32[]) (K : Kernel<'X>) (parameters : C_SVR) (options : OptimizationOptions) =
        let N = Array.length X
        let N' = 2 * N
        let Y' = Array.init N' (fun i -> if i < N then +1.0f else -1.0f)
        let X' = Array.init N' (fun i -> if i < N then X.[i] else X.[i-N])
        let C' = Array.create N' parameters.C
        let p' = Array.init N' (fun i -> parameters.eta - (if i < N then (float Y.[i]) else (float -Y.[i-N])))
        let A' = Array.zeroCreate N'

        let Q = new Q_R(capacity options.cacheSize (2*N), N,
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
        let N'' = Array.sumBy (fun a -> if a <> 0.0 then 1 else 0) A
        let A'' = Array.zeroCreate N''
        let X'' = Array.zeroCreate N''

        let mutable k = 0
        for i = 0 to N - 1 do
            if A.[i] <> 0.0 then
                A''.[k] <- A.[i]
                X''.[k] <- X.[i]
                k <- k + 1

        Regression(K,X'',A'',b)
