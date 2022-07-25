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

namespace Semagle.Logging

open Hopac
open Semagle.Logging.Message

type LoggerBuilder(logger : Logger) =
    let log (level : LogLevel) (message : unit -> string) =
        logger.log level (fun level -> message() |> event level) |> queueIgnore

    member builder.Yield (()) = ()

    [<CustomOperation("verbose")>]
    member builder.verbose (_ : unit, [<ProjectionParameter>] message : unit -> string) =
        log Verbose message

    [<CustomOperation("debug")>]
    member builder.debug (_ : unit, [<ProjectionParameter>] message : unit -> string) =
        log Debug message

    [<CustomOperation("info")>]
    member builder.info (_ : unit, [<ProjectionParameter>] message : unit -> string) =
        log Info message

    [<CustomOperation("warn")>]
    member builder.warn (_ : unit, [<ProjectionParameter>] message : unit -> string) =
        log Warn message

    [<CustomOperation("error")>]
    member builder.error (_ : unit, [<ProjectionParameter>] message : unit -> string) =
        log Error message

    [<CustomOperation("fatal")>]
    member builder.fatal (_ : unit, [<ProjectionParameter>] message : unit -> string) =
        log Fatal message

    [<CustomOperation("time")>]
    member builder.time (_ : unit, [<ProjectionParameter>] f: unit -> 'result) =
        let watch = System.Diagnostics.Stopwatch.StartNew()
        let result = f ()
        watch.Stop()
        log Info (fun () -> sprintf "Elapsed Time: %f" ((float watch.ElapsedMilliseconds) / 1000.0))
        result
