// Copyright 2018-2022 Serge Slipchenko (Serge.Slipchenko@gmail.com)
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
#r "Semagle.Data.Formats.dll"
#r "Semagle.MachineLearning.Metrics.dll"
#r "Semagle.MachineLearning.SVM.dll"
#r "Semagle.MachineLearning.SSVM.dll"
#endif // INTERACTIVE

open LanguagePrimitives
open System

open Semagle.Logging
open Semagle.Numerics.Vectors
open Semagle.Data.Formats
open Semagle.MachineLearning.Metrics
open Semagle.MachineLearning.SSVM

let logger = LoggerBuilder(Log.create "Sequence")

#if INTERACTIVE
let main (args : string[]) =
#else
[<EntryPoint>]
let main(args) =
#endif
    if args.Length < 2 then
        printfn "Usage: [fsi Sequence.fsx | Sequence.exe] <train.data> <test.data>"
        exit 1

    Global.initialise { Global.defaultConfig with getLogger = (fun name -> Targets.create Info name) }

    // load train and test data
    let readData file =
        CRFSuite.read file |> Seq.toArray |> Array.unzip

    logger { info ("Loading train data...") }
    let train_y, train_x = logger { time(readData args.[0]) }

    logger { info ("Loading test data...") }
    let test_y, test_x = logger { time(readData args.[1]) }

    let dictionary = CRFSuite.buildFeatureDictionary train_x

    let train_x = CRFSuite.vectorizeSequences dictionary train_x |> Seq.toArray
    let test_x = CRFSuite.vectorizeSequences dictionary test_x |> Seq.toArray

    logger { info ("Training...") }
    let sequence = logger { time(Sequence.learn train_x train_y (fun v -> v :> Vector)
                                                { Sequence.defaultSequence with C = 1.0 }
                                                OneSlack.defaultOptimizationOptions) }

    logger { info ("Predicting...") }
    let predict = Sequence.predict sequence

    let predict_y = logger { time(test_x |> Array.map(predict)) }

    let total = test_y |> Array.sumBy Array.length

    let correct =
        (Array.zip test_y predict_y)
        |> Array.sumBy (fun (ts, ps) ->
            (Array.zip ts ps) |> Array.sumBy (fun (t, p) -> if t = p then 1 else 0))

    let test_y = Array.collect id test_y
    let predict_y = Array.collect id predict_y

    let recall = Classification.recall test_y predict_y
    let precision = Classification.precision test_y predict_y
    let f1 = Classification.f1 test_y predict_y

    printfn "Recall, precision and F1 per class"
    Seq.ofArray test_y
    |> Seq.iter (fun y ->
        printfn "%A = %g, %g, %g" y (Map.find y recall) (Map.find y precision) (Map.find y f1))

    printfn "Accuracy = %f(%d/%d)"
            ((DivideByInt (float correct) total) * 100.0)
            correct total
    printfn "Recall = %f" (recall |> Map.toSeq |> Seq.averageBy snd)
    printfn "Precision = %f" (precision |> Map.toSeq |> Seq.averageBy snd)
    printfn "F1 = %f" (f1 |> Map.toSeq |> Seq.averageBy snd)

    0
