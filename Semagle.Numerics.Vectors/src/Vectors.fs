// Copyright 2016-2022 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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

namespace Semagle.Numerics.Vectors

open System

/// Vector abstract class
[<AbstractClass>]
type Vector() =
    /// Returns the number of the vector dimensions
    abstract member Dimensions: int with get

    /// Returns dense vector representation
    abstract member AsDense: DenseVector

    /// Returns sparse vector representation
    abstract member AsSparse: SparseVector

/// Dense vector stores both zero and non-zero values
and DenseVector(values : float32[]) =
    inherit Vector()

    override vector.Dimensions = values.Length

    override vector.AsDense = vector

    override vector.AsSparse =
        let M = Array.sumBy(fun v -> if v <> 0.0f then 1 else 0) values
        let indices = Array.zeroCreate<int> M
        let values' = Array.zeroCreate<float32> M
        let mutable k = 0
        for i=0 to values.Length-1 do
            if values.[i] <> 0.0f then
                indices.[k] <- i
                values'.[k] <- values.[i]
                k <- k + 1
        SparseVector(indices, values')

    /// Returns underlying values array
    member vector.Values = values

    /// Returns size of the vector
    member vector.Length = Array.length values

    /// Gets vector element value
    member vector.Item(i : int) = values.[i]

    /// Returns vector slice
    member vector.GetSlice(a : int option, b : int option) =
        match a, b with
        | Some(i), Some(j) -> DenseVector(values.[i..j])
        | Some(i), None -> DenseVector(values.[i..])
        | None, Some(j) -> DenseVector(values.[..j])
        | None, None -> DenseVector(values)

    /// Returns string representation
    override vector.ToString() =
        sprintf "(%s)" (values |> Seq.map (sprintf "%A") |> String.concat ", ")

    /// Compares two dense vectors
    override vector.Equals(other) =
        match other with
        | :? DenseVector as other -> vector.Values = other.Values
        | _ -> false

    /// Returns hash code
    override vector.GetHashCode() = vector.Values.GetHashCode()

    /// Zero dense vector
    static member inline Zero = DenseVector([||])

    /// Element-wise addition
    static member inline (+) (a : DenseVector, b : DenseVector) =
        DenseVector(Array.map2 (+) a.Values b.Values)

    /// Element-wise substraction
    static member inline (-) (a : DenseVector, b : DenseVector) =
        DenseVector(Array.map2 (-) a.Values b.Values)

    /// Element-wise multiplication
    static member inline (*) (a : DenseVector, b : DenseVector) =
        DenseVector(Array.map2 (*) a.Values b.Values)

    /// Negation of vector
    static member inline (~-)(a : DenseVector) =
        DenseVector(Array.map (~-) a.Values)

    /// Scalar product
    static member inline (.*) (a : DenseVector, b : DenseVector) =
        if LanguagePrimitives.PhysicalEquality a b then
            // optimization for x .* x cases
            Array.sumBy (fun v -> v*v) a.Values
        else
            // general case
            Array.fold2 (fun sum va vb -> sum + va*vb) 0.0f a.Values b.Values

    /// Squared Euclidean distance $||a-b||^2$
    static member inline (||-||) (a : DenseVector, b : DenseVector) =
        if LanguagePrimitives.PhysicalEquality a b then
            // optimization for x .* x cases
            0.0f
        else
            // general case
            Array.fold2 (fun sum va vb -> let v = va - vb in sum + v*v) 0.0f a.Values b.Values

    /// Scalar product
    static member inline (.*) (a : DenseVector, b : SparseVector) =
        Array.fold2 (fun sum i v -> if i < a.Length then sum + a.[i]*v else sum) 0.0f b.Indices b.Values

    /// Mutiply each element of vector by scalar
    static member inline (*)(a : DenseVector, c : float32) =
        DenseVector (Array.map (fun va -> va * c) a.Values)

    /// Divide each element of vector by scalar
    static member inline (/)(a : DenseVector, c : float32) =
        DenseVector (Array.map (fun va -> va / c) a.Values)

/// Sparse vector stores non-zero values and non-zero values indices
and SparseVector(indices : int[], values : float32[]) =
    inherit Vector()

    do
        // check that indices and values arrays have the same size
        assert ((Array.length indices) = (Array.length values))
        // check indices are strictly increasing
        assert (indices |> Array.pairwise |> Array.forall (fun (i, j) -> i < j))

    override vector.Dimensions =
        if indices.Length > 0 then
            indices.[indices.Length-1]+1
        else
            0

    override vector.AsDense =
        let values' = Array.zeroCreate<float32> vector.Dimensions
        for i=0 to indices.Length-1 do
            values'.[indices.[i]] <-  values.[i]
        DenseVector(values')

    override vector.AsSparse = vector

    /// Returns underlying indices array
    member vector.Indices = indices

    /// Returns underlying values array
    member vector.Values = values

    /// Gets vector element value
    member vector.Item(i : int) =
        match (Array.tryFindIndex (fun index -> index = i) indices) with
        | Some index -> values.[index]
        | None -> 0.0f

    /// Returns vector slice
    member vector.GetSlice(a : int option, b : int option) =
        let first = match a with
                    | Some(i) ->
                        let f = System.Array.BinarySearch(indices, i)
                        if f > 0 then f else ~~~f
                    | None -> 0
        let last = match b with
                   | Some(j) ->
                        let l = System.Array.BinarySearch(indices, j)
                        if l > 0 then l else (~~~l)+1
                   | None -> indices.Length-1
        SparseVector(indices.[first..last], values.[first..last])

    /// Returns string representation
    override vector.ToString() =
         sprintf "(%s)" (Seq.map2 (sprintf "%A:%A") indices values |> String.concat ", ")

    /// Compares two sparse vectors
    override vector.Equals(other) =
        match other with
        | :? SparseVector as other -> vector.Indices = other.Indices &&
                                      vector.Values = other.Values
        | _ -> false

    /// Returns hash code
    override vector.GetHashCode() = 32*vector.Indices.GetHashCode() + 16*vector.Values.GetHashCode()

    /// Zero sparse vector
    static member inline Zero = SparseVector([||],[||])

    /// Element-wise addition
    static member inline (+) (a : SparseVector, b : SparseVector) =
        let a_indices = a.Indices
        let a_values = a.Values

        let b_indices = b.Indices
        let b_values = b.Values

        let indices = Array.zeroCreate (a_indices.Length + b_indices.Length)
        let values = Array.zeroCreate (a_values.Length + b_values.Length)

        let mutable i = 0
        let mutable j = 0
        let mutable k = 0

        while i < a_indices.Length && j < b_indices.Length do
            let a_index = a_indices.[i]
            let b_index = b_indices.[j]
            if a_index < b_index then
                indices.[k] <- a_index
                values.[k] <- a_values.[i]
                i <- i + 1
                k <- k + 1
            else if a_index > b_index then
                indices.[k] <- b_index
                values.[k] <- b_values.[j]
                j <- j + 1
                k <- k + 1
            else
                let v = a_values.[i] + b_values.[j]
                if v <> 0.0f then
                    indices.[k] <- a_index
                    values.[k] <- v
                    k <- k + 1
                i <- i + 1
                j <- j + 1

        while i < a_indices.Length do
            indices.[k] <- a_indices.[i]
            values.[k] <- a_values.[i]
            i <- i + 1
            k <- k + 1

        while j < b_indices.Length do
            indices.[k] <- b_indices.[j]
            values.[k] <- b_values.[j]
            j <- j + 1
            k <- k + 1

        if k = 0 then
            SparseVector([||], [||])
        else
            SparseVector(indices.[..k-1], values.[..k-1])

    /// Element-wise substraction
    static member inline (-) (a : SparseVector, b : SparseVector) =
        let a_indices = a.Indices
        let a_values = a.Values

        let b_indices = b.Indices
        let b_values = b.Values

        let indices = Array.zeroCreate (a_indices.Length + b_indices.Length)
        let values = Array.zeroCreate (a_values.Length + b_values.Length)

        let mutable i = 0
        let mutable j = 0
        let mutable k = 0

        while i < a_indices.Length && j < b_indices.Length do
            let a_index = a_indices.[i]
            let b_index = b_indices.[j]
            if a_index < b_index then
                indices.[k] <- a_index
                values.[k] <- a_values.[i]
                i <- i + 1
                k <- k + 1
            else if a_index > b_index then
                indices.[k] <- b_index
                values.[k] <- -b_values.[j]
                j <- j + 1
                k <- k + 1
            else
                let v = a_values.[i] - b_values.[j]
                if v <> 0.0f then
                    indices.[k] <- a_index
                    values.[k] <- v
                    k <- k + 1
                i <- i + 1
                j <- j + 1

        while i < a_indices.Length do
            indices.[k] <- a_indices.[i]
            values.[k] <- a_values.[i]
            i <- i + 1
            k <- k + 1

        while j < b_indices.Length do
            indices.[k] <- b_indices.[j]
            values.[k] <- -b_values.[j]
            j <- j + 1
            k <- k + 1

        if k = 0 then
            SparseVector([||], [||])
        else
            SparseVector(indices.[..k-1], values.[..k-1])

    /// Element-wise multiplication
    static member inline (*) (a : SparseVector, b : SparseVector) =
        let a_indices = a.Indices
        let a_values = a.Values

        let b_indices = b.Indices
        let b_values = b.Values

        let indices = Array.zeroCreate (a_indices.Length + b_indices.Length)
        let values = Array.zeroCreate (a_values.Length + b_values.Length)

        let mutable i = 0
        let mutable j = 0
        let mutable k = 0

        while i < a_indices.Length && j < b_indices.Length do
            let a_index = a_indices.[i]
            let b_index = b_indices.[j]
            if a_index < b_index then
                i <- i + 1
            else if a_index > b_index then
                j <- j + 1
            else
                indices.[k] <- a_index
                values.[k] <- a_values.[i] * b_values.[j]
                k <- k + 1
                i <- i + 1
                j <- j + 1

        if k = 0 then
            SparseVector([||], [||])
        else
            SparseVector(indices.[..k-1], values.[..k-1])

    /// Scalar product
    static member inline (.*) (a : SparseVector, b : SparseVector) =
        let mutable sum = 0.0f
        if LanguagePrimitives.PhysicalEquality a b then
            // optimization for x .* x cases
            let values = a.Values
            for i = 0 to values.Length-1 do
                let v = values.[i]
                sum <- sum + v*v
        else
            // general case
            let mutable i = 0
            let mutable j = 0

            let a_indices = a.Indices
            let a_values = a.Values

            let b_indices = b.Indices
            let b_values = b.Values

            while i < a_indices.Length && j < b_indices.Length do
                if a_indices.[i] < b_indices.[j] then i <- i + 1
                else if a_indices.[i] > b_indices.[j] then j <- j + 1
                else sum <- sum + a_values.[i]*b_values.[j]; i <- i + 1; j <- j + 1

        sum

    /// Scalar product with weight array
    static member inline (.*) (w : float[], v : SparseVector) =
        let indices = v.Indices
        let values = v.Values
        let mutable sum = 0.0
        for i = 0 to indices.Length-1 do
            sum <- sum + w.[indices.[i]]*(float values.[i])
        sum

    /// Scalar product with weight subarray
    static member inline (.*) (w : Span<float>, v : SparseVector) =
        let indices = v.Indices
        let values = v.Values
        let mutable sum = 0.0
        for i = 0 to indices.Length-1 do
            sum <- sum + w.[indices.[i]]*(float values.[i])
        sum

    /// Squared Euclidean distance $||a-b||^2$
    static member inline (||-||) (a : SparseVector, b : SparseVector) =
        if LanguagePrimitives.PhysicalEquality a b then
            0.0f
        else
            let mutable sum = 0.0f
            let mutable i = 0
            let mutable j = 0

            let a_indices = a.Indices
            let a_values = a.Values

            let b_indices = b.Indices
            let b_values = b.Values

            while i < a_indices.Length && j < b_indices.Length do
                if a_indices.[i] < b_indices.[j] then
                    let v = a_values.[i]
                    i <- i + 1
                    sum <- sum + v*v
                else if a_indices.[i] > b_indices.[j] then
                    let v = -b_values.[j]
                    j <- j + 1
                    sum <- sum + v*v
                else
                    let v = a_values.[i] - b_values.[j]
                    i <- i + 1; j <- j + 1
                    sum <- sum + v*v

            while i < a_indices.Length do
                let v = a_values.[i]
                i <- i + 1
                sum <- sum + v*v

            while j < b_indices.Length do
                let v = -b_values.[j]
                j <- j + 1
                sum <- sum + v*v

            sum

    /// Negation of vector
    static member inline (~-)(a : SparseVector) =
        SparseVector(Array.copy a.Indices, Array.map (~-) a.Values)

    /// Mutiply each element of vector by scalar
    static member inline (*)(a : SparseVector, c : float32) =
        SparseVector(Array.copy a.Indices, Array.map (fun va -> va * c) a.Values)

    /// Divide each element of vector by scalar
    static member inline (/)(a : SparseVector, c : float32) =
        SparseVector(Array.copy a.Indices, Array.map (fun va -> va / c) a.Values)
