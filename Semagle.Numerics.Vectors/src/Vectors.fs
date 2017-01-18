﻿// Copyright 2016 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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

/// Dense vector stores both zero and non-zero values
type DenseVector(values : float32[]) =
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

    /// Apply function `f` to vector `a` elements and return the resulting `DenseVector` 
    static member inline map (f : float32 -> float32) (a : DenseVector) =
        DenseVector(Array.map f a.Values)

    /// Apply function `f` to vector `a` and `b` elements and return the resulting `DenseVector`
    static member inline map2 (f : float32 -> float32 -> float32) (a : DenseVector) (b : DenseVector) =
        DenseVector(Array.map2 f a.Values b.Values)

    /// Element-wise addition
    static member inline (+) (a : DenseVector, b : DenseVector) = DenseVector.map2 (+) a b

    /// Element-wise substraction
    static member inline (-) (a : DenseVector, b : DenseVector) = DenseVector.map2 (-) a b

    /// Element-wise multiplication
    static member inline (*) (a : DenseVector, b : DenseVector) = DenseVector.map2 (*) a b

    /// Negation of vector
    static member inline (~-)(a : DenseVector) = DenseVector.map (~-) a

    /// Scalar product
    static member inline (.*) (a : DenseVector, b : DenseVector) =
        if System.Object.ReferenceEquals(a, b) then
            // optimization for x .* x cases
            Array.sumBy (fun v -> v*v) a.Values
        else
            // general case
            Array.fold2 (fun sum va vb -> sum + va*vb) 0.0f a.Values b.Values

    /// Scalar product
    static member inline (.*) (a : DenseVector, b : SparseVector) =
        Array.fold2 (fun sum i v -> if i < a.Length then sum + a.[i]*v else sum) 0.0f b.Indices b.Values

    /// Mutiply each element of vector by scalar
    static member inline (*)(a : DenseVector, c : float32) = DenseVector.map (fun va -> va * c) a

    /// Divide each element of vector by scalar
    static member inline (/)(a : DenseVector, c : float32) = DenseVector.map (fun va -> va / c) a

/// Sparse vector stores non-zero values and non-zero values indeces
and SparseVector(indices : int[], values : float32[]) =
    do
        // check that indices and values arrays have the same size
        assert ((Array.length indices) = (Array.length values))
        // check indices are strictly increasing
        assert (indices |> Array.pairwise |> Array.forall (fun (i, j) -> i < j))

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

    /// Apply function `f` to vector `a` elements and return the resulting `SparseVector`
    static member inline map (f : float32 -> float32) (a : SparseVector) =
        let mutable k = 0

        let newIndices = Array.zeroCreate a.Indices.Length
        let newValues = Array.zeroCreate a.Values.Length

        for i in 0 .. a.Indices.Length-1 do
            let value = f a.Values.[i]
            if value <> 0.0f then
                newIndices.[k] <- a.Indices.[i]; newValues.[k] <- value; k <- k + 1

        SparseVector(newIndices, newValues)

    /// Apply function `f` to vector `a` and `b` elements and return the resulting `SparseVector`
    static member inline map2 (f : float32 -> float32 -> float32) (a : SparseVector) (b : SparseVector) =
        let mutable i = 0
        let mutable j = 0
        let mutable k = 0

        let newIndices = Array.zeroCreate (a.Indices.Length + b.Indices.Length)
        let newValues = Array.zeroCreate (a.Values.Length + b.Values.Length)

        let addValue index value =
            if value <> 0.0f then 
                newIndices.[k] <- index; newValues.[k] <- value; k <- k + 1

        while i < a.Indices.Length && j < b.Indices.Length do
            match (i, j) with
            | _ when a.Indices.[i] < b.Indices.[j] -> addValue a.Indices.[i] (f a.Values.[i] 0.0f); i <- i + 1
            | _ when a.Indices.[i] > b.Indices.[j] -> addValue b.Indices.[j] (f 0.0f b.Values.[j]); j <- j + 1
            | _ -> addValue a.Indices.[i] (f a.Values.[i] b.Values.[j]); i <- i + 1; j <- j + 1

        while i < a.Indices.Length do
            addValue a.Indices.[i] (f a.Values.[i] 0.0f); i <- i + 1

        while j < b.Indices.Length do
            addValue b.Indices.[j] (f 0.0f b.Values.[j]); j <- j + 1

        if k = 0 then
            SparseVector([||], [||])
        else
            SparseVector(newIndices.[..k-1], newValues.[..k-1])

    /// Element-wise addition
    static member inline (+) (a : SparseVector, b : SparseVector) = SparseVector.map2 (+) a b

    /// Element-wise substraction
    static member inline (-) (a : SparseVector, b : SparseVector) = SparseVector.map2 (-) a b

    /// Element-wise multiplication
    static member inline (*) (a : SparseVector, b : SparseVector) = SparseVector.map2 (*) a b

    /// Fold vectors `a` and `b` by function `f`
    static member inline fold2 (f : float32 -> float32 -> float32 -> float32) (state: float32) (a : SparseVector) (b : SparseVector) = 
        let mutable i = 0
        let mutable j = 0

        let mutable s = state

        let inline updateState a b = 
            s <- (f s a b)

        while i < a.Indices.Length && j < b.Indices.Length do
            match (i, j) with
            | _ when a.Indices.[i] < b.Indices.[j] -> updateState a.Values.[i] 0.0f; i <- i + 1
            | _ when a.Indices.[i] > b.Indices.[j] -> updateState 0.0f b.Values.[j]; j <- j + 1
            | _ -> updateState a.Values.[i] b.Values.[j]; i <- i + 1; j <- j + 1

        while i < a.Indices.Length do
            updateState a.Values.[i] 0.0f; i <- i + 1

        while j < b.Indices.Length do
            updateState 0.0f b.Values.[j]; j <- j + 1

        s 

    /// Scalar product
    static member inline (.*) (a : SparseVector, b : SparseVector) =
        if System.Object.ReferenceEquals(a, b) then
            // optimization for x .* x cases
            Array.sumBy (fun v -> v*v) a.Values 
        else
            // general case
            let mutable sum = 0.0f

            let mutable i = 0
            let mutable j = 0

            while i < a.Indices.Length && j < b.Indices.Length do
                match (i, j) with
                | _ when a.Indices.[i] < b.Indices.[j] -> i <- i + 1
                | _ when a.Indices.[i] > b.Indices.[j] -> j <- j + 1
                | _ -> sum <- sum + a.Values.[i]*b.Values.[j]; i <- i + 1; j <- j + 1

            sum

    /// Negation of vector
    static member inline (~-)(a : SparseVector) = SparseVector.map (~-) a

    /// Mutiply each element of vector by scalar
    static member inline (*)(a : SparseVector, c : float32) = SparseVector.map (fun va -> va * c) a

    /// Divide each element of vector by scalar
    static member inline (/)(a : SparseVector, c : float32) = SparseVector.map (fun va -> va / c) a
