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
#r "Semagle.MachineLearning.SVM.dll"
#r "Semagle.MachineLearning.SSVM.dll"
#endif // INTERACTIVE

open LanguagePrimitives
open System

open Hopac
open Logary
open Logary.Targets
open Logary.Configuration

open Semagle.Numerics.Vectors
open Semagle.Numerics.Vectors.IO
open Semagle.MachineLearning.SVM
open Semagle.MachineLearning.SSVM

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

    withLogaryManager "SSVM MultiClass" (
        withTargets [Console.create (Console.ConsoleConf.create Formatting.StringFormatter.verbatim) "console"] >> 
        withRules [
            Rule.createForTarget "console" |> Rule.setHieraString ".*\\.SVM\\..*" |> Rule.setLevel Warn
            Rule.createForTarget "console" |> Rule.setHieraString ".*\\.SSVM\\..*" |> Rule.setLevel Info
            ]
        ) |> Job.Ignore |> Hopac.run

    // load train and test data
    let readData file = 
        let y, x = LibSVM.read file |> Seq.toArray |> Array.unzip
        let y = Array.map int y
        let N = Array.length y
        let percents = 
            y |> Array.countBy id |> Array.sortBy snd 
              |> Array.map (fun (_, c) -> sprintf "%0.2f%%" (100.0 * (float c) / (float N)))
        printfn "[%s]" (String.Join("; ", percents))
        y, x

    printfn "Loading train data..." 
    let train_y, train_x = duration { return readData args.[0] }

    printfn "Loading test data..."
    let test_y, test_x = duration { return readData args.[1] }

    // create SVM model
    printfn "Training SVM model..."
    let multiclass = duration { return MultiClass.learn train_x train_y id { MultiClass.defaults with C = 1000.0f } 
        SMO.defaultOptimizationOptions}

    // predict and compute correct count
    printfn "Predicting SVM model..."
    let predict = MultiClass.predict multiclass   

    let predict_y = duration { return test_x |> Array.map (fun x -> predict x) }

    // compute statistics
    let total = Array.length test_y
    let correct = 
        let counts = (Array.zip test_y predict_y) |> Array.countBy (fun (t, p) -> t = p) |> Map.ofArray in
        match Map.tryFind true counts with
        | None -> 0
        | Some(count) -> count

    printfn "Accuracy = %f%%(%d/%d)" ((DivideByInt (float correct) total) * 100.0) correct total
    0

#if INTERACTIVE
main fsi.CommandLineArgs.[1..]
#endif
