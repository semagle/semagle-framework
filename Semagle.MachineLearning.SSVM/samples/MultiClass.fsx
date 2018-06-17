// Copyright 2018 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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

#r "Semagle.Logging.dll"
#r "Semagle.Numerics.Vectors.dll"
#r "Semagle.Numerics.Vectors.IO.dll"
#r "Semagle.MachineLearning.Metrics.dll"
#r "Semagle.MachineLearning.SVM.dll"
#r "Semagle.MachineLearning.SSVM.dll"
#endif // INTERACTIVE

open System

open Semagle.Logging
open Semagle.Numerics.Vectors
open Semagle.Numerics.Vectors.IO
open Semagle.MachineLearning.Metrics
open Semagle.MachineLearning.SSVM

let logger = LoggerBuilder(Log.create "MultiClass")

#if INTERACTIVE
let main (args : string[]) =
#else
[<EntryPoint>]
let main(args) =
#endif
    if args.Length < 2 then
        printfn "Usage: [fsi MultiClass.fsx | MultiClass.exe] <train.data> <test.data>"
        exit 1

    Global.initialise { Global.defaultConfig with getLogger = (fun name -> Targets.create Info name) }

    // load train and test data
    let readData file = 
        let y, x = LibSVM.read file |> Seq.toArray |> Array.unzip
        let y = Array.map int y
        let N = Array.length y
        let percents = 
            y |> Array.countBy id |> Array.sortBy snd 
              |> Array.map (fun (_, c) -> sprintf "%0.2f%%" (100.0 * (float c) / (float N)))
        logger { info(sprintf "[%s]" (String.Join("; ", percents))) }
        y, x

    logger { info ("Loading train data...") }
    let train_y, train_x = logger { time(readData args.[0]) }

    logger { info ("Loading test data...") }
    let test_y, test_x = logger { time(readData args.[1]) }

    logger { info ("Training...") }
    let multiclass = logger { time(MultiClass.learn train_x train_y (fun v -> v :> Vector) 
                                                    { MultiClass.defaultMultiClass with C = 100.0f }
                                                    OneSlack.defaultOptimizationOptions) }

    logger { info ("Predicting...") }
    let predict = MultiClass.predict multiclass

    let predict_y = logger { time(test_x |> Array.map(predict)) }

    let total = Array.length test_y
    let accuracy = Classification.accuracy test_y predict_y
    let correct = int (accuracy * (float total))

    printfn "Accuracy = %f%%(%d/%d)" (accuracy * 100.0) correct total
    0
      