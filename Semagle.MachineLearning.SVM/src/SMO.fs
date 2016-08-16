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

[<Measure>] type MB

/// Implementation of Sequential Minimal Optimization (SMO) algorithm
module SMO =
    [<Literal>]
    let private tau = 1e-12f

    [<Literal>]
    let private not_found = -1

    [<Literal>]
    let private shrinking_iterations = 1000

    [<Literal>]
    let private LowerBound = -1y

    [<Literal>]
    let private Unbound = 0y

    [<Literal>]
    let private UpperBound = +1y

    let inline swap (a : 'A[]) i j =
        let tmp = a.[i]
        a.[i] <- a.[j]
        a.[j] <- tmp

    /// LRU list of computed columns
    type private LRU(capacity : int, N : int, Q : int -> int -> float32) =
        let indices = Array.zeroCreate<int> capacity
        let columns = Array.zeroCreate<float32[]> capacity
        let lengths = Array.zeroCreate<int> capacity

        let mutable first = 0
        let mutable last = 0

        /// Returns L elements of j-th column of Q matrix
        member lru.Get (j : int) (L : int) =
            let index = lru.tryFindIndex j
            if index <> not_found then
                let column = columns.[index]
                let length = lengths.[index]
                if length < L then
                    for i = length to L-1 do
                        column.[i] <- Q i j
                    lengths.[index] <- L
                column
            else 
                let column = Array.init N (fun i -> if i < L then Q i j else 0.0f)
                lru.insert j column L
                column

        /// Swap column elements
        member lru.Swap (i : int) (j : int) = 
            let mutable index_i = not_found
            let mutable index_j = not_found

            let mutable k = first
            while k <> last do
                if indices.[k] = i then index_i <- k
                if indices.[k] = j then index_j <- k
                swap columns.[k] i j
                k <- (k + 1) % capacity 

            if index_i <> not_found && index_j <> not_found then
                swap lengths index_i index_j

            if index_i <> not_found then indices.[index_i] <- j
            if index_j <> not_found then indices.[index_j] <- i

        /// Try to find computed column values
        member private lru.tryFindIndex t =
            let mutable i = first
            while (i <> last) && (indices.[i] <> t) do
                i <- (i + 1) % capacity 

            if i <> last then i else not_found

        /// Insert new computed column values
        member private lru.insert index column length =
            indices.[last] <- index
            columns.[last] <- column
            lengths.[last] <- length
            last <- (last + 1) % capacity

            if first = last then first <- (first + 1) % capacity

        /// Returns required capacity for the specified cache size and column length
        static member capacity (cacheSize : int<MB>) (length : int) =
           let columnSize = sizeof<float32>*length + sizeof<int> + sizeof<float32[]>
           max 2 ((int cacheSize)*1024*1024 / columnSize)

    /// Interface of Q matrix
    type Q =
        /// Swap column elements
        abstract member Swap : int -> int -> unit

        /// Returns N elements of the main diagonal of Q matrix
        abstract member D : float32[]

        /// Returns L elements of j-th column of Q matrix
        abstract member C : int -> int -> float32[]

    type WSSStrategy = MaximalViolatingPair | SecondOrderInformation

    /// Optimization options of SMO algorithm
    type OptimizationOptions = { maxIterations : int; strategy : WSSStrategy; shrinking : bool; cacheSize : int<MB> }

    /// General parameters for C_SVM problem
    type C_SVM = { A : float32[]; C : float32[]; p: float32[]; epsilon : float32; Q : Q; options : OptimizationOptions }

    /// Sequential Minimal Optimization (SMO) problem solver
    let C_SMO (X : 'X[]) (Y : float32[]) (K : Kernel<'X>) (parameters : C_SVM) = 
        if Array.length X <> Array.length Y then
            invalidArg "X and Y" "have different lengths"

        let info = printfn

        let epsilon = parameters.epsilon
        let C = parameters.C
        let p = parameters.p

        let N = Array.length X
        let Q = parameters.Q
        let A = parameters.A

        let inline status i = 
            if 0.0f < A.[i] && A.[i] < C.[i] then
                Unbound
            else if Y.[i] = +1.0f && A.[i] = C.[i] || Y.[i] = -1.0f && A.[i] = 0.0f then
                UpperBound
            else
                LowerBound

        let S = Array.init N status

        let inline isUpperBound i = S.[i] = UpperBound
        let inline isUnbound i = S.[i] = Unbound
        let inline isLowerBound i = S.[i] = LowerBound

        let G = Array.copy p
        let G' = Array.zeroCreate<float32> N 

        for i = 0 to N-1 do
            if not (isLowerBound i) then
                let Q_i = Q.C i N
                let inline updateG (a_i : float32) (G : float32[]) = 
                    for j = 0 to N-1 do
                        G.[j] <- G.[j] + a_i*Q_i.[j]
                
                updateG A.[i] G

                if isUpperBound i then updateG C.[i] G'

        // working set selection helper functions
        let inline _y_gf i = -G.[i]*Y.[i]

        let inline maxUp L =
            let mutable max_i = not_found
            let mutable max_v = System.Single.NegativeInfinity
            for i = 0 to L-1 do
                if not (isUpperBound i) then
                    let v = _y_gf i
                    if v > max_v then
                        max_i <- i
                        max_v <- v
            max_i

        let inline minLow L =
            let mutable min_i = not_found
            let mutable min_v = System.Single.PositiveInfinity
            for i = 0 to L-1 do
                if not (isLowerBound i) then
                    let v = _y_gf i
                    if v < min_v then
                        min_i <- i
                        min_v <- v
            min_i

        let inline minLowTo s L =
            let Q_s = Q.C s L
            let inline objective t = 
                let a_ts = Q.D.[t] + (Q_s.[s]) - 2.0f*(Q_s.[t])*Y.[t]*Y.[s]
                let b_ts = _y_gf t - _y_gf s
                -b_ts*b_ts/(if a_ts > 0.0f then a_ts else tau)

            let mutable min_i = not_found
            let mutable min_v = System.Single.PositiveInfinity
            for i = 0 to L-1 do
                if not (isLowerBound i) && (_y_gf i < _y_gf s) then
                    let v =objective i
                    if v < min_v then
                        min_i <- i
                        min_v <- v
            min_i
        
        /// Maximal violating pair working set selection strategy
        let maximalViolatingPair L =
            let i = maxUp L
            if i = not_found then None else Some (i, minLow L)

        /// Second order information working set selection strategy 
        let secondOrderInformation L = 
            let i = maxUp L
            if i = not_found then None else Some (i, minLowTo i L)

        let selectWorkingSet =
            match parameters.options.strategy with
                | MaximalViolatingPair -> maximalViolatingPair
                | SecondOrderInformation -> secondOrderInformation       

        /// Solve an optimization sub-problem
        let inline solve i j L = 
            let Q_i = Q.C i L
            let Q_j = Q.C j L
            let a_ij = (Q_i.[i]) + (Q_j.[j]) - 2.0f*(Q_i.[j])*Y.[i]*Y.[j]
            if Y.[i] <> Y.[j] then
                let delta = (-G.[i]-G.[j])/(if a_ij < 0.0f then tau else a_ij)
                let diff = A.[i] - A.[j]
                match (A.[i] + delta, A.[j] + delta) with
                    | _, a_j when diff > 0.0f && a_j < 0.0f -> (diff, 0.0f)
                    | a_i, _ when (* diff <= 0.0f && *) a_i < 0.0f -> (0.0f, diff)
                    | _, a_j when diff <= C.[i] - C.[j] && a_j > C.[j] -> (C.[j]+diff, C.[j])
                    | a_i, _ when (* diff > C.[i] - C.[j] && *) a_i > C.[i] -> (C.[i], C.[i] - diff)
                    | a_i, a_j -> a_i, a_j
            else
                let delta = (G.[i]-G.[j])/(if a_ij < 0.0f then tau else a_ij)
                let sum = A.[i] + A.[j]
                match (A.[i] - delta, A.[j] + delta) with
                    | a_i, _ when sum > C.[i] && a_i > C.[i] -> (C.[i], sum - C.[i])
                    | _, a_j when (* sum <= C.[i] && *) a_j < 0.0f -> (sum, 0.0f)
                    | _, a_j when sum > C.[j] && a_j > C.[j] -> (sum - C.[j], C.[j])
                    | a_i, _ when (* sum <= C.[j] && *) a_i < 0.0f -> (0.0f, sum)
                    | a_i, a_j -> a_i, a_j

        /// Update gradient
        let inline updateG i j a_i a_j L =
            let Q_i = Q.C i L
            let Q_j = Q.C j L

            for t = 0 to L-1 do
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

        /// Reconstruct gradient
        let inline reconstructG L = 
            info "reconstruct gradient"
            for t = L to N-1 do
                G.[t] <- G'.[t] + p.[t]

            let mutable unbound = 0
            for i = 0 to L-1 do
                if isUnbound i then unbound <- unbound + 1

            let inline passive2active () =
                for i = L to N-1 do
                    let Q_i = Q.C i L
                    for j = 0 to L-1 do
                        if isUnbound j then 
                            G.[i] <- G.[i] + A.[j]*Q_i.[j]

            let inline active2passive () =
                for i = 0 to L-1 do
                    if isUnbound i then
                        let Q_i = Q.C i N
                        for j = L to N-1 do
                            G.[j] <- G.[j] + A.[i]*Q_i.[j]

            if unbound*L > 2*L*(N-L) then
                passive2active ()
            else
                active2passive ()

        let inline m L = 
            let mutable max_v = System.Single.NegativeInfinity
            for i = 0 to L-1 do
                if not (isUpperBound i) then
                    let v = _y_gf i
                    if v > max_v then
                        max_v <- v
            max_v

        let inline M L =
            let mutable min_v = System.Single.PositiveInfinity
            for i = 0 to L-1 do
                if not (isLowerBound i) then
                    let v = _y_gf i
                    if v < min_v then
                        min_v <- v
            min_v

        /// shrink active set
        let inline shrink m M L = 
            let inline isShrinked i = (isUpperBound i) && (_y_gf i) > m || (isLowerBound i) && (_y_gf i) < M

            let inline swapAll i j =
                swap X i j; swap Y i j
                swap C i j; swap A i j
                swap G i j; swap G' i j
                swap S i j; Q.Swap i j

            let mutable swaps = 0
            let mutable i = 0
            let mutable j = L - 1
            let mutable k = L - 1

            let mutable shrinked = 0

            while i <= j do
                match (isShrinked i), (isShrinked j) with
                | false, false -> i <- i + 1; j <- j - 1
                | true, false -> j <- j - 1
                | false, true -> i <- i + 1
                | _ ->
                    if j = k then
                        j <- j - 1
                    else
                        swaps <- swaps + 1
                        swapAll i k
                        i <- i + 1     
                    shrinked <- shrinked + 1
                    k <- k - 1

            if shrinked > 0 then                
                info "shrinked = %d, active = %d, swaps = %d" shrinked (L - shrinked) swaps

            L - shrinked

        let inline isOptimal m M epsilon = (m - M) <= epsilon

        /// Sequential Minimal Optimization (SMO) Algorithm
        let inline optimize_solve L =
            // Find a pair of elements that violate the optimality condition
            match selectWorkingSet L with
                | Some (i, j) -> 
                    // Solve the optimization sub-problem
                    let a_i, a_j = solve i j L
                    // Update the gradient
                    updateG i j a_i a_j L
                    if parameters.options.shrinking then
                        updateG' i a_i
                        updateG' j a_j
                    // Update the solution
                    A.[i] <- a_i; A.[j] <- a_j
                    S.[i] <- status i; S.[j] <- status j
                    true
                | None -> false

        /// Optimize with shrinking every 1000 iterations
        let rec optimize_shrinking k s L reconstructed =
            let inline optimize_shrink m M L =
                let mutable reconstructed = reconstructed
                if not reconstructed && isOptimal m M (10.0f*epsilon) then
                    reconstructG L
                    reconstructed <- true
                reconstructed, shrink m M L

            if k < parameters.options.maxIterations then
                let m_k = m L
                let M_k = M L

                // shrink active set
                let reconstructed, L = 
                    if s = 0 then 
                        optimize_shrink m_k M_k L 
                    else 
                        reconstructed, L

                // check optimality for active set
                if isOptimal m_k M_k epsilon then
                    reconstructG L

                    // check optimality for full set
                    if not(isOptimal (m L) (M L) epsilon) && optimize_solve N then
                        // shrink on next iteration
                        optimize_shrinking k 1 N reconstructed
                    else
                        k 
                else
                    if optimize_solve L then 
                        optimize_shrinking (k + 1) (if s > 0 then (s - 1) else shrinking_iterations) L reconstructed
                    else 
                        k
             else
                failwith "Exceeded iterations limit"

        /// Optimize without shrinking every 1000 iterations
        let rec optimize_non_shrinking k =
            if k < parameters.options.maxIterations then
                let m_k = m N
                let M_k = M N

                if not (isOptimal m_k M_k epsilon) && optimize_solve N then
                    optimize_non_shrinking (k + 1)
                else
                    k
            else
                failwith "Exceeded iterations limit"

        let iterations = 
            if parameters.options.shrinking then 
                optimize_shrinking 0 shrinking_iterations N false
            else
                optimize_non_shrinking 0
        info "#iterations = %d" iterations

        /// Reconstruction of hyperplane bias
        let bias =
            let mutable b = 0.0f
            let mutable M = 0
            for i = 0 to N-1 do
                if isUnbound i then
                    b <- b + _y_gf i
                    M <- M + 1
            DivideByInt b M

        (K,X,Y,A,bias)

    /// Q matrix for classification problems
    type private Q_C(capacity : int, N : int, Q : int -> int -> float32) =
        let diagonal = Array.init N (fun i -> Q i i)
        let lru = LRU(capacity, N, Q)

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
    type C_SVC = { C_p : float32; C_n : float32; epsilon : float32; options : OptimizationOptions }

    /// C Support Vector Classification (SVC) problem solver
    let C_SVC (X : 'X[]) (Y : float32[]) (K : Kernel<'X>) (parameters : C_SVC) =
        let N = Array.length X
        let X' = Array.copy X
        let Y' = Array.copy Y
        let C = Array.init N (fun i -> if Y.[i] = +1.0f then parameters.C_p else parameters.C_n )
        let p = Array.create N -1.0f
        let A = Array.zeroCreate N

        let (K,X',Y',A',b) = C_SMO X' Y' K { epsilon = parameters.epsilon; A = A; C = C; p = p;
                                             Q = new Q_C(LRU.capacity parameters.options.cacheSize N, N, 
                                                         (fun i j -> (K X'.[i] X'.[j])*Y'.[i]*Y'.[j])); 
                                             options = parameters.options }

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

    /// Optimization parameters for One-Class problem
    type OneClass = { nu : float32; epsilon : float32; options : OptimizationOptions }

    /// One-Class problem solver
    let OneClass (X : 'X[]) (K : Kernel<'X>) (parameters : OneClass) = 
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

        let (K,X',_,A',b) = C_SMO X' Y K { epsilon = parameters.epsilon; A = A; C = C; p = p; 
                                           Q = new Q_C(LRU.capacity parameters.options.cacheSize N, N, 
                                                       (fun i j -> K X'.[i] X'.[j]));
                                           options = parameters.options }

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
    type private Q_R(capacity : int, N : int, Q : int -> int -> float32) =
        let diagonal = Q_R.initDiagonal N Q
        let indices = Array.init (2*N) id
        let lru = LRU(capacity, 2*N, Q)

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
    type C_SVR = { eta : float32; C : float32; epsilon : float32; options : OptimizationOptions }

    /// C Support Vector Regression (SVR) problem solver
    let C_SVR (X : 'X[]) (Y : float32[]) (K : Kernel<'X>) (parameters : C_SVR) =
        let N = Array.length X
        let N' = 2 * N
        let Y' = Array.init N' (fun i -> if i < N then +1.0f else -1.0f)
        let X' = Array.init N' (fun i -> if i < N then X.[i] else X.[i-N])
        let C' = Array.create N' parameters.C
        let p' = Array.init N' (fun i -> parameters.eta - (if i < N then Y.[i] else -Y.[i-N]))
        let A' = Array.zeroCreate N'

        let Q = new Q_R(LRU.capacity parameters.options.cacheSize (2*N), N, (fun i j -> (K X'.[i] X'.[j])*Y'.[i]*Y'.[j]))
        let (K,X',_,A',b) = C_SMO X' Y' K { epsilon = parameters.epsilon; A = A'; C = C'; p = p'; Q = Q;
                                            options = parameters.options }

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
