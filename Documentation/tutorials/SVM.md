# SVM classification and regression
This tutorial shows how to train and evaluate the performance of SVM models for 
for two and one class classification and regression problems using LIBSVM datasets.

## Initialization
First, we need to load Semagle framework assemblies for manipulation of vector data
and SVM training/prediction.

    #r "Semagle.Numerics.Vectors.dll"
    #r "Semagle.Numerics.Vectors.IO.dll"
    #r "Semagle.MachineLearning.SVM.dll"
    #r "Semagle.MachineLearning.SVM.dll"

    open LanguagePrimitives
    open System

    open Semagle.Numerics.Vectors
    open Semagle.Numerics.Vectors.IO
    open Semagle.MachineLearning.SVM

## Reading LIBSVM Data
[`Semagle.Numerics.Vectors.IO`](Semagle.Numerics.Vectors.IO/index.html) provides function `LibSVM.read` 
that returns the lazy sequence of `(y, x)` pairs, but [`Semagle.MachineLearning.SVM`](Semagle.MachineLearning.SVM/index.html) 
requires separate arrays of labels and samples for training. Function `readData` converts the sequence to array and splits
the array of pairs.

    let readData file = LibSVM.read file |> Seq.toArray |> Array.unzip

    let train_y, train_x = readData fsi.CommandLineArgs.[1]

    let test_y, test_x = readData fsi.CommandLineArgs.[2]

## Training

There are three different functions [`SMO.C_SVC`, `SMO.OneClass` and `SMO.C_SVR`](reference/semagle-machinelearning-svm-smo.html)
that build two class classification, one class classification and regression SVM models. The functions take
the samples array `train_x`, the labels array `train_y` (except for `OneClass`), the kernel function `(Kernel.rbf 0.1f)` and
parameters specific to the particular optimization problem.

### Two Class
Two class classification problem requires separate penalties for positive `C_p` and negative `C_n` samples:

    let svm = SMO.C_SVC train_x train_y (Kernel.rbf 0.1f) 
                        { C_p = 1.0f; C_n = 1.0f; epsilon = 0.001f;
                          options = { strategy = SMO.SecondOrderInformation; maxIterations = 1000000; 
                                      shrinking = true; cacheSize = 200<MB> } }

### One Class
One class classification problem requires the fraction of support vectors `nu`:

    let svm = SMO.OneClass train_x (Kernel.rbf 0.1f) 
                          { nu = 0.5f; epsilon = 0.001f;
                            options = { strategy = SMO.SecondOrderInformation; maxIterations = 1000000; 
                                        shrinking = true; cacheSize = 200<MB> } }

### Regression
Regression problem requires the boundary `eta` and the penalty `C`:

    let svm = SMO.C_SVR train_x train_y (Kernel.rbf 0.1f) 
                        { eta = 0.1f; C = 1.0f; epsilon = 0.001f;
                          options = { strategy = SMO.SecondOrderInformation; maxIterations = 1000000; 
                                      shrinking = true; cacheSize = 200<MB> } }

## Predicting

There are three different prediction functions [`TwoClass.predict`](reference/semagle-machinelearning-svm-twoclass.html),
[`OneClass.predict`](reference/semagle-machinelearning-svm-oneclass.html) and 
[`Regression.predict`](reference/semagle-machinelearning-svm-regression.html). The functions take the model `model` and the sample `x` 
and return the label `y`. The prediction function can be curried

 *  Two Class

        let predict = TwoClass.predict svm

 * One Class

        let predict = OneClass.predict svm

 * Regression 

        let predict = Regression.predict svm   
   
and applied to the test samples vector

    let predict_y = test_x |> Array.map (fun x -> predict x)

## Evaluating
There are two widely used metrics for the evaluation of the performace:

 * Accuracy (two and one class classification)

        let accuracy = 
            let correct (Array.zip test_y predict_y) |> Array.sumBy (fun (t, p) -> if t = float32 p then 1 else 0) in
                100.0 * (DivideByInt (float correct) (Array.length test_y))

 * Mean Squared Error (regression)
        
        let mse = DivideByInt (Array.fold2 (fun sum t p -> sum + (pown (t - p) 2)) 0.0f test_y predict_y) (Array.length test_y)