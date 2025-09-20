namespace DisplaySwitchPro

/// Enhanced Result type utilities for functional composition and error handling
/// Provides advanced combinators and utilities beyond the basic Result module
module EnhancedResult =

    /// Enhanced Result type alias for better readability
    type Result<'T, 'E> = Result<'T, 'E>

    /// Core Result utilities with enhanced composition capabilities
    module Result =

        /// Map function for transforming successful values
        let map f result =
            match result with
            | Ok value -> Ok (f value)
            | Error error -> Error error

        /// Bind function for chaining Result computations
        let bind f result =
            match result with
            | Ok value -> f value
            | Error error -> Error error

        /// Apply function for applicative-style composition
        let apply fResult xResult =
            match fResult, xResult with
            | Ok f, Ok x -> Ok (f x)
            | Error e, _ -> Error e
            | _, Error e -> Error e

        /// Map over the error value
        let mapError f result =
            match result with
            | Ok value -> Ok value
            | Error error -> Error (f error)

        /// Convert an option to a Result with a specified error
        let ofOption error option =
            match option with
            | Some value -> Ok value
            | None -> Error error

        /// Convert a Result to an option, discarding errors
        let toOption result =
            match result with
            | Ok value -> Some value
            | Error _ -> None

        /// Check if result is Ok
        let isOk result =
            match result with
            | Ok _ -> true
            | Error _ -> false

        /// Check if result is Error
        let isError result =
            match result with
            | Ok _ -> false
            | Error _ -> true

        /// Get the value from Ok result or throw exception
        let get result =
            match result with
            | Ok value -> value
            | Error _ -> failwith "Cannot get value from Error result"

        /// Get the error from Error result or throw exception
        let getError result =
            match result with
            | Ok _ -> failwith "Cannot get error from Ok result"
            | Error error -> error

        /// Get the value from Ok result or return default
        let defaultValue defaultVal result =
            match result with
            | Ok value -> value
            | Error _ -> defaultVal

        /// Get the value from Ok result or compute default from error
        let defaultWith f result =
            match result with
            | Ok value -> value
            | Error error -> f error

        /// Traverse function for processing lists while preserving the first error
        let traverse (f: 'a -> Result<'b, 'e>) (list: 'a list) : Result<'b list, 'e> =
            let rec loop acc remaining =
                match remaining with
                | [] -> Ok (List.rev acc)
                | head :: tail ->
                    match f head with
                    | Ok value -> loop (value :: acc) tail
                    | Error error -> Error error
            loop [] list

        /// Traverse function that collects all errors
        let traverseAll (f: 'a -> Result<'b, 'e>) (list: 'a list) : Result<'b list, 'e list> =
            let results = list |> List.map f
            let errors = results |> List.choose (function Error e -> Some e | Ok _ -> None)
            let successes = results |> List.choose (function Ok s -> Some s | Error _ -> None)

            if List.isEmpty errors then
                Ok successes
            else
                Error errors

        /// Sequence function for converting a list of Results to a Result of list
        let sequence (results: Result<'a, 'e> list) : Result<'a list, 'e> =
            traverse id results

        /// Sequence function that collects all errors
        let sequenceAll (results: Result<'a, 'e> list) : Result<'a list, 'e list> =
            traverseAll id results

        /// Choose function for filtering and mapping Results
        let choose (f: 'a -> Result<'b option, 'e>) (list: 'a list) : Result<'b list, 'e> =
            let rec loop acc remaining =
                match remaining with
                | [] -> Ok (List.rev acc)
                | head :: tail ->
                    match f head with
                    | Ok (Some value) -> loop (value :: acc) tail
                    | Ok None -> loop acc tail
                    | Error error -> Error error
            loop [] list

        /// Collect function for separating Ok and Error results
        let collect (results: Result<'a, 'e> list) : ('a list * 'e list) =
            let successes = results |> List.choose (function Ok s -> Some s | Error _ -> None)
            let errors = results |> List.choose (function Error e -> Some e | Ok _ -> None)
            (successes, errors)

        /// Partition function for separating based on a predicate
        let partition (predicate: 'a -> bool) (result: Result<'a, 'e>) : (Result<'a, 'e> * Result<'a, 'e>) =
            match result with
            | Ok value ->
                if predicate value then
                    (Ok value, Error (failwith "No error available"))
                else
                    (Error (failwith "No error available"), Ok value)
            | Error error -> (Error error, Error error)

        /// Fold over a Result value
        let fold (onOk: 'a -> 'c) (onError: 'e -> 'c) (result: Result<'a, 'e>) : 'c =
            match result with
            | Ok value -> onOk value
            | Error error -> onError error

        /// Try to execute a function and catch exceptions as Result
        let tryWith (f: unit -> 'a) : Result<'a, exn> =
            try
                Ok (f ())
            with
            | ex -> Error ex

        /// Try to execute a function with input and catch exceptions as Result
        let tryWithInput (f: 'a -> 'b) (input: 'a) : Result<'b, exn> =
            try
                Ok (f input)
            with
            | ex -> Error ex

    /// Enhanced computation expression builder for Result
    type EnhancedResultBuilder() =
        member _.Bind(x, f) = Result.bind f x
        member _.Return(x) = Ok x
        member _.ReturnFrom(x) = x
        member _.Zero() = Ok ()
        member _.Combine(a, b) = Result.bind (fun () -> b) a
        member _.Delay(f) = f
        member _.Run(f) = f()
        member _.TryWith(body, handler) =
            try body()
            with ex -> handler ex
        member _.TryFinally(body, compensation) =
            try body()
            finally compensation()
        member _.For(sequence, body) =
            sequence
            |> Seq.fold (fun acc item ->
                Result.bind (fun () -> body item) acc) (Ok ())
        member _.While(guard, body) =
            let rec loop () =
                if guard() then
                    Result.bind (fun () -> loop()) (body())
                else
                    Ok ()
            loop()

    /// Global instance of the enhanced result computation expression
    let enhancedResult = EnhancedResultBuilder()

    /// Utility functions for working with validation Results
    module Validation =

        /// Applicative operators for validation
        let (<!>) = Result.map
        let (<*>) = Result.apply

        /// Lift a two-argument function into Result context
        let lift2 f a b =
            f <!> a <*> b

        /// Lift a three-argument function into Result context
        let lift3 f a b c =
            f <!> a <*> b <*> c

        /// Lift a four-argument function into Result context
        let lift4 f a b c d =
            f <!> a <*> b <*> c <*> d

        /// Validate all items in a list and collect errors
        let validateAll validations value =
            validations
            |> List.fold (fun acc validate ->
                match acc, validate value with
                | Ok (), Ok () -> Ok ()
                | Ok (), Error e -> Error [e]
                | Error errs, Ok () -> Error errs
                | Error errs1, Error e -> Error (e :: errs1)
            ) (Ok ())

        /// Validate at least one condition passes
        let validateAny validations value =
            let results = validations |> List.map (fun v -> v value)
            let hasSuccess = results |> List.exists Result.isOk
            if hasSuccess then
                Ok ()
            else
                let errors = results |> List.choose (function Error e -> Some e | Ok _ -> None)
                Error errors

        /// Create a validator that checks a predicate
        let createValidator predicate errorMsg =
            fun value ->
                if predicate value then Ok ()
                else Error errorMsg

        /// Combine validators with AND logic
        let andValidators validators =
            fun value ->
                validators
                |> List.fold (fun acc validator ->
                    match acc, validator value with
                    | Ok (), Ok () -> Ok ()
                    | Error errs, Ok () -> Error errs
                    | Ok (), Error e -> Error [e]
                    | Error errs, Error e -> Error (e :: errs)
                ) (Ok ())

        /// Combine validators with OR logic
        let orValidators validators =
            fun value ->
                let results = validators |> List.map (fun v -> v value)
                let successes = results |> List.filter Result.isOk
                if not (List.isEmpty successes) then
                    Ok ()
                else
                    let errors = results |> List.choose (function Error e -> Some e | Ok _ -> None)
                    Error errors

    /// Pipeline utilities for chaining operations
    module Pipeline =

        /// Forward composition operator for Results
        let (>=>) f g = fun x -> Result.bind g (f x)

        /// Kleisli composition (same as >=>)
        let kleisli f g = f >=> g

        /// Pipe operator for Results
        let (>>|) result f = Result.map f result

        /// Bind operator alias
        let (>>=) result f = Result.bind f result

        /// Apply operator alias
        let (<|>) f result = Result.apply f result

        /// Alternative operator - return first successful result
        let (<|>) result1 result2 =
            match result1 with
            | Ok _ -> result1
            | Error _ -> result2

        /// Tee operator - execute side effect and pass through result
        let tee (sideEffect: 'a -> unit) (result: Result<'a, 'e>) : Result<'a, 'e> =
            match result with
            | Ok value ->
                sideEffect value
                Ok value
            | Error error -> Error error

        /// Tap operator - execute side effect on error and pass through result
        let tap (sideEffect: 'e -> unit) (result: Result<'a, 'e>) : Result<'a, 'e> =
            match result with
            | Ok value -> Ok value
            | Error error ->
                sideEffect error
                Error error

    /// Async Result utilities for handling asynchronous computations
    module AsyncResult =

        /// Create an async Result from a Result
        let ofResult result = async { return result }

        /// Create an async Result from an async value
        let ofAsync asyncValue = async {
            let! value = asyncValue
            return Ok value
        }

        /// Map over async Result
        let map f asyncResult = async {
            let! result = asyncResult
            return Result.map f result
        }

        /// Bind for async Result
        let bind f asyncResult = async {
            let! result = asyncResult
            match result with
            | Ok value -> return! f value
            | Error error -> return Error error
        }

        /// Apply for async Result
        let apply fAsyncResult xAsyncResult = async {
            let! fResult = fAsyncResult
            let! xResult = xAsyncResult
            return Result.apply fResult xResult
        }

        /// Map error for async Result
        let mapError f asyncResult = async {
            let! result = asyncResult
            return Result.mapError f result
        }

        /// Catch exceptions in async computations as Results
        let catch asyncComputation = async {
            try
                let! value = asyncComputation
                return Ok value
            with
            | ex -> return Error ex
        }

        /// Traverse for async Results
        let traverse f list = async {
            let rec loop acc remaining = async {
                match remaining with
                | [] -> return Ok (List.rev acc)
                | head :: tail ->
                    let! result = f head
                    match result with
                    | Ok value ->
                        return! loop (value :: acc) tail
                    | Error error ->
                        return Error error
            }
            return! loop [] list
        }

    /// Helper functions for working with specific Result patterns
    module Helpers =

        /// Create a Result from a try/catch pattern
        let tryCreate creator input =
            Result.tryWithInput creator input

        /// Create a Result from an option with custom error
        let fromOption error option =
            Result.ofOption error option

        /// Create a Result that validates a condition
        let validateCondition predicate error value =
            if predicate value then Ok value
            else Error error

        /// Create a Result that validates not null
        let validateNotNull error value =
            if not (isNull (box value)) then Ok value
            else Error error

        /// Create a Result that validates a string is not empty
        let validateNotEmpty error (str: string) =
            if not (System.String.IsNullOrWhiteSpace(str)) then Ok str
            else Error error

        /// Create a Result that validates a list is not empty
        let validateNotEmptyList error list =
            if not (List.isEmpty list) then Ok list
            else Error error

        /// Create a Result that validates a range
        let validateRange min max error value =
            if value >= min && value <= max then Ok value
            else Error error

        /// Combine multiple validations on the same value
        let validateValue value validators =
            validators
            |> List.fold (fun acc validator ->
                Result.bind validator acc
            ) (Ok value)