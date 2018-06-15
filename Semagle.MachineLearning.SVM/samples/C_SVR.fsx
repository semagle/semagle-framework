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

#if INTERACTIVE
#I @"../../build"

#r "Semagle.Numerics.Vectors.dll"
#r "Semagle.Numerics.Vectors.IO.dll"
#r "Semagle.MachineLearning.Metrics.dll"
#r "Semagle.MachineLearning.SVM.dll"
#endif // INTERACTIVE

open LanguagePrimitives

open Semagle.Numerics.Vectors.IO
open Semagle.MachineLearning.Metrics
open Semagle.MachineLearning.SVM

type DurationBuilder() =
    member duration.Delay(f) =
        let timer = new System.Diagnostics.Stopwatch()
        timer.Start()
        let returnValue = f()
        printfn "Elapsed Time: %f" ((float timer.ElapsedMilliseconds) / 1000.0)
        returnValue

    member duration.Return(x) =
        x

let duration = DurationBuilder()

#if INTERACTIVE
let main (args : string[]) =
#else
[<EntryPoint>]
let main(args) =
#endif
    if args.Length < 2 then
        printfn "Usage: [fsi C_SVR.fsx | C_SVR.exe] <train.data> <test.data>"
        exit 1

    // load train and test data
    let readData file = LibSVM.read file |> Seq.toArray |> Array.unzip

    printfn "Loading train data..." 
    let train_y, train_x = duration { return readData args.[0] }

    printfn "Loading test data..."
    let test_y, test_x = duration { return readData args.[1] }

    // create SVM model
    printfn "Training SVM model..."
    let svm = duration { 
        return SMO.C_SVR train_x train_y (Kernel.rbf 0.1f) { eta = 0.1f; C = 1.0f }
            SMO.defaultOptimizationOptions }

    // predict and compute correct count
    printfn "Predicting SVM model..."
    let predict = Regression.predict svm
    let predict_y = duration { return test_x |> Array.map (fun x -> predict x) }

    // compute statistics
    printfn "MSE = %f" (Regression.mse test_y predict_y)
    0

#if INTERACTIVE
main fsi.CommandLineArgs.[1..]
#endif

