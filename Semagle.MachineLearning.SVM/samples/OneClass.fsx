﻿// Copyright 2016-2022 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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
#r "Semagle.Logging.dll"
#r "Semagle.MachineLearning.Metrics.dll"
#r "Semagle.Numerics.Vectors.dll"
#r "Semagle.Data.Formats.dll"
#r "Semagle.MachineLearning.SVM.dll"
#endif // INTERACTIVE

open Hopac
open Semagle.Logging
open Semagle.Data.Formats
open Semagle.MachineLearning.Metrics;
open Semagle.MachineLearning.SVM

let logger = LoggerBuilder(Log.create "OneClass")

#if INTERACTIVE
let main (args : string[]) =
#else
[<EntryPoint>]
let main (args) =
#endif
    if args.Length < 2 then
        printfn "Usage: [fsi OneClass.fsx | OneClass.exe] <train.data> <test.data>"
        exit 1

    Scheduler.Global.setCreate { Scheduler.Create.Def with NumWorkers = Some(1) }
    Global.initialise { Global.defaultConfig with getLogger = (fun name -> Targets.create Info name) }

    // load train and test data
    let readData file = LibSVM.read file |> Seq.toArray |> Array.unzip

    logger { info("Loading train data...") }
    let _, train_x = logger { time(readData args.[0]) }

    logger { info ("Loading test data...") }
    let test_y, test_x = logger { time(readData args.[1]) }

    // create SVM model
    logger { info("Training SVM model...") }
    let svm = logger { time(SMO.OneClass train_x (Kernel.rbf 0.1) { nu = 0.5 } SMO.defaultOptimizationOptions) }

    // predict and compute correct count
    logger { info ("Predicting SVM model...") }
    let predict = OneClass.predict svm
    let predict_y = logger { time(test_x |> Array.map (fun x -> float32 (predict x))) }

    // compute statistics
    let total = Array.length test_y
    let accuracy = Classification.accuracy test_y predict_y
    let correct = int (accuracy * (float total))

    printfn "Accuracy = %f%%(%d/%d)" (accuracy * 100.0) correct total
    0

#if INTERACTIVE
main fsi.CommandLineArgs.[1..]
#endif
