# Semagle.Numerics.Vectors Library

The library provides two `DenseVector` and `SparseVector` with corresponding operations, 
which are commonly used in machine learning applications.
 
## Creating Vectors

`DenseVector` is constructed from the array of `float32` values. 
This type of vector stores zero and non-zero values and suits well for feature
vectors with many non-zero elements.

    open Semagle.Numerics.Vector
    let a = DenseVector([| 1.0f; 3.0f; -3.0f; 4.0f; 8.0f |])

`SparseVector` is constructed from the array of `int` indices of non-zero elements
and the array of `float32` values of non-zero elements. This type of vector suits well
for feature vectors with a few non-zero elements.    

    open Semagle.Numerics.Vector
    let a = SparseVector([|0; 1; 3; 5|], [|1.0f; -2.0f; 4.0f; -6.0f|])

## Operations

### Indexing and Slicing

`DenseVector` and `SparseVector` support indexing and slicing operations, but
implementations of `SparseVector` operations are more expensive because they require
binary search for finding item indeces.

    a.[1]
    a.[1..3]
    a.[..3]
    a.[2..]

### Element-wise Operations    

`DenseVector` and `SparseVector` support element wise addition, subtraction, multiplication and division.

    a + b
    a - b
    a * b
    a / b 

### Negation and Multiplication/Division by Scalar

`DenseVector` and `SparseVector` support unary negation and multiplication/division by scalar.

    -a
    a * 1.5f
    a / 2.0f

### Dot/Inner Product

Dot product is a key operation of many machine learning algoritms and `DenseVector` and `SparseVector`
provide the effective implementations of this operation.

    a .* b
