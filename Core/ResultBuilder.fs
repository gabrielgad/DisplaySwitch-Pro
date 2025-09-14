namespace DisplaySwitchPro

// Shared result computation expression builder
// Provides functional error handling with railway-oriented programming
module ResultBuilder =
    
    /// Result computation expression builder for functional error handling
    type ResultBuilder() =
        member _.Bind(x, f) = Result.bind f x
        member _.Return(x) = Ok x
        member _.ReturnFrom(x) = x
        member _.Zero() = Ok ()
        member _.Combine(a, b) = Result.bind (fun () -> b) a
        member _.Delay(f) = f
        member _.Run(f) = f()
        member _.For(seq, body) = 
            seq |> Seq.fold (fun acc item -> 
                Result.bind (fun () -> body item) acc) (Ok ())

    /// Global instance of the result computation expression
    let result = ResultBuilder()