module GrpcSkills.Tests.RegistrationTests

open Xunit
open ProtoBuf
open ProtoBuf.FSharp
open System.IO
open System.Collections.Generic
open GrpcSkills.Tests.Contracts

/// Tests that validate F# type registration with protobuf-net-fsharp.

[<Fact>]
let ``Registration of record types does not throw`` () =
    Setup.ensureRegistered ()

let roundtrip<'T> (value: 'T) : 'T =
    Setup.ensureRegistered ()
    use ms = new MemoryStream()
    Serializer.Serialize(ms, value)
    ms.Position <- 0L
    Serializer.Deserialize<'T>(ms)

// ── Option Type Serialization ──

[<Fact>]
let ``Option Some serializes after registration`` () =
    let original = { Query = "test"; MaxResults = Some 42; Category = Some "cat" }
    let result = roundtrip original
    Assert.Equal("test", result.Query)
    Assert.Equal(Some 42, result.MaxResults)
    Assert.Equal(Some "cat", result.Category)

[<Fact>]
let ``Option None serializes after registration`` () =
    let original = { Query = "test"; MaxResults = None; Category = None }
    let result = roundtrip original
    Assert.Equal("test", result.Query)
    Assert.Equal(None, result.MaxResults)
    Assert.Equal(None, result.Category)

[<Fact>]
let ``Nested option in Person works`` () =
    let original =
        { Name = "Test"; Age = 25
          Address = { Street = "X"; City = "Y"; PostCode = "Z" }
          Email = Some "test@example.com" }
    let result = roundtrip original
    Assert.Equal(Some "test@example.com", result.Email)

[<Fact>]
let ``Nested None option in Person works`` () =
    let original =
        { Name = "Test"; Age = 25
          Address = { Street = "X"; City = "Y"; PostCode = "Z" }
          Email = None }
    let result = roundtrip original
    Assert.Equal(None, result.Email)

// ── Array Serialization ──

[<Fact>]
let ``Array serializes correctly`` () =
    let original =
        { Results =
            [| { Id = "1"; Title = "A"; Score = 1.0 }
               { Id = "2"; Title = "B"; Score = 2.0 } |]
          TotalCount = 2 }
    let result = roundtrip original
    Assert.Equal(2, result.Results.Length)

[<Fact>]
let ``Empty array serializes correctly`` () =
    let original = { Results = [||]; TotalCount = 0 }
    let result = roundtrip original
    Assert.Empty(result.Results)

// ── Dictionary Serialization ──

[<Fact>]
let ``Dictionary serializes correctly`` () =
    let tags = Dictionary<string,string>()
    tags.["a"] <- "1"
    tags.["b"] <- "2"
    let original = { Id = "m1"; Lines = [||]; Tags = tags }
    let result = roundtrip original
    Assert.Equal(2, result.Tags.Count)
    Assert.Equal("1", result.Tags.["a"])

[<Fact>]
let ``Empty Dictionary serializes correctly`` () =
    let original = { Id = "m2"; Lines = [||]; Tags = Dictionary<string,string>() }
    let result = roundtrip original
    Assert.True(result.Tags = null || result.Tags.Count = 0)
