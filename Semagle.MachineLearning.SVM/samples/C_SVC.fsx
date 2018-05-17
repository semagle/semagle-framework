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

#r "Hopac.dll"
#r "Hopac.Core.dll"
#r "Logary.dll"
#r "NodaTime.dll"

#r "Semagle.Numerics.Vectors.dll"
#r "Semagle.Numerics.Vectors.IO.dll"
#r "Semagle.MachineLearning.Metrics.dll"
#r "Semagle.MachineLearning.SVM.dll"
#r "Semagle.MachineLearning.SVM.dll"
#endif // INTERACTIVE

open LanguagePrimitives
open System

open Hopac
open Logary
open Logary.Targets
open Logary.Configuration

open Semagle.MachineLearning.Metrics
open Semagle.Numerics.Vectors
open Semagle.Numerics.Vectors.IO
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
        printfn "Usage: [fsi C_SVC.fsx | C_SVC.exe] <train.data> <test.data>"
        exit 1

    withLogaryManager "C_SVC" (
        withTargets [Console.create (Console.ConsoleConf.create Formatting.StringFormatter.verbatim) "console"] >> 
        withRules [Rule.createForTarget "console"]) |> Job.Ignore |> Hopac.run

    // load train and test data
    let readData file = LibSVM.read file |> Seq.toArray |> Array.unzip

    printfn "Loading train data..." 
    let train_y, train_x = duration { return readData args.[0] }
    let multiclass = (train_y |> Array.distinct |> Array.length) <> 2

    printfn "Loading test data..."
    let test_y, test_x = duration { return readData args.[1] }

    // create SVM model
    printfn "Training SVM model..."
    let svm = duration { 
        let C_SVC = if multiclass then SMO.C_SVC_M else SMO.C_SVC
        return C_SVC train_x train_y (Kernel.rbf 0.1f) { C_p = 1.0f; C_n = 1.0f } SMO.defaultOptimizationOptions
        }

    // predict and compute correct count
    printfn "Predicting SVM model..."
    let predict x = 
        if multiclass then 
            MultiClass.predict svm x
        else
            float32 (TwoClass.predict svm x)
    let predict_y = duration { return test_x |> Array.map (fun x -> predict x) }

    // compute statistics
    let total = Array.length test_y
    let accuracy = Classification.accuracy test_y predict_y
    let correct = int (accuracy * (float total))

    printfn "Accuracy = %f%%(%d/%d)" (accuracy * 100.0) correct total
    0

#if INTERACTIVE
main fsi.CommandLineArgs.[1..]
#endif
