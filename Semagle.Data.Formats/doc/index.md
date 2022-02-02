# Semagle.Data.Formats Library

Semagle.Data.Formats provides reading and writing functions for popular
machine learning dataset formats.

## LIBSVM

LIBSVM uses "sparse" vector format where zero values do not need to be stored.
Each vector is repsented by a separate text line. A first number of the line is
the label `y` (class number or value of the approximated function), which is
followed by index/value pairs of non-zero elements of the feature vector `x`.

    [lang=txt]
    1 1:5.1 3:2 4:-15
    2 2:13 8:33

### Read Data
Read the sequence of `(y, x)` pairs from LIBSVM file:

    open Semagle.Numerics.Vectors
    open Semagle.Data.Formats

    LibSVM.read "train file"

### Write Data
Write the sequence of `(y, x)` pairs to LIBSVM file:

    open Semagle.Numerics.Vectors
    open Semagle.Data.Formats

    let data = List.toSeq [
        (+1.0f, SparseVector([|0; 3; 7|], [|1.0f; 2.0f; 5.0f|]));
        (+1.0f, SparseVector([|0; 2; 9|], [|3.0f; 2.0f; 5.0f|]));
        (-1.0f, SparseVector([|4; 8|], [|0.1f; 0.5f|]))]

    LibSVM.write "train file" data
