module GrpcSkills.Tests.SerializationTests

open Xunit
open ProtoBuf
open System.IO
open System.Collections.Generic
open GrpcSkills.Tests.Contracts

/// Helper to roundtrip serialize/deserialize a value.
let roundtrip<'T> (value: 'T) : 'T =
    Setup.ensureRegistered ()
    use ms = new MemoryStream()
    Serializer.Serialize(ms, value)
    ms.Position <- 0L
    Serializer.Deserialize<'T>(ms)

// ── Basic Record Serialization ──

[<Fact>]
let ``GreetRequest roundtrips correctly`` () =
    let original = { Name = "World" }
    let result = roundtrip original
    Assert.Equal("World", result.Name)

[<Fact>]
let ``GreetReply roundtrips correctly`` () =
    let original = { GreetReply.Message = "Hello, World!" }
    let result = roundtrip original
    Assert.Equal("Hello, World!", result.Message)

// ── Optional Field Serialization ──

[<Fact>]
let ``SearchRequest with Some values roundtrips`` () =
    let original =
        { Query = "grpc"
          MaxResults = Some 10
          Category = Some "tutorials" }
    let result = roundtrip original
    Assert.Equal("grpc", result.Query)
    Assert.Equal(Some 10, result.MaxResults)
    Assert.Equal(Some "tutorials", result.Category)

[<Fact>]
let ``SearchRequest with None values roundtrips`` () =
    let original =
        { Query = "grpc"
          MaxResults = None
          Category = None }
    let result = roundtrip original
    Assert.Equal("grpc", result.Query)
    Assert.Equal(None, result.MaxResults)
    Assert.Equal(None, result.Category)

[<Fact>]
let ``SearchRequest with mixed option values roundtrips`` () =
    let original =
        { Query = "fsharp"
          MaxResults = Some 5
          Category = None }
    let result = roundtrip original
    Assert.Equal(Some 5, result.MaxResults)
    Assert.Equal(None, result.Category)

// ── Collection Serialization ──

[<Fact>]
let ``Order with array of lines roundtrips`` () =
    let tags = Dictionary<string,string>()
    tags.["priority"] <- "high"
    tags.["source"] <- "web"
    let original =
        { Id = "ord-001"
          Lines =
            [| { ProductId = "P1"; Quantity = 2; UnitPrice = 9.99m }
               { ProductId = "P2"; Quantity = 1; UnitPrice = 24.99m } |]
          Tags = tags }
    let result = roundtrip original
    Assert.Equal("ord-001", result.Id)
    Assert.Equal(2, result.Lines.Length)
    Assert.Equal("P1", result.Lines.[0].ProductId)
    Assert.Equal(2, result.Lines.[0].Quantity)
    Assert.Equal(9.99m, result.Lines.[0].UnitPrice)
    Assert.Equal("P2", result.Lines.[1].ProductId)

[<Fact>]
let ``Order with empty array roundtrips`` () =
    let original =
        { Id = "ord-002"
          Lines = [||]
          Tags = Dictionary<string,string>() }
    let result = roundtrip original
    Assert.Equal("ord-002", result.Id)
    Assert.Empty(result.Lines)

[<Fact>]
let ``Order with dictionary values roundtrips`` () =
    let tags = Dictionary<string,string>()
    tags.["a"] <- "1"
    tags.["b"] <- "2"
    tags.["c"] <- "3"
    let original =
        { Id = "ord-003"
          Lines = [||]
          Tags = tags }
    let result = roundtrip original
    Assert.Equal(3, result.Tags.Count)
    Assert.Equal("1", result.Tags.["a"])
    Assert.Equal("2", result.Tags.["b"])
    Assert.Equal("3", result.Tags.["c"])

// ── Nested Record Serialization ──

[<Fact>]
let ``Person with nested Address roundtrips`` () =
    let original =
        { Name = "Alice"
          Age = 30
          Address =
            { Street = "123 Main St"
              City = "Springfield"
              PostCode = "62701" }
          Email = Some "alice@example.com" }
    let result = roundtrip original
    Assert.Equal("Alice", result.Name)
    Assert.Equal(30, result.Age)
    Assert.Equal("123 Main St", result.Address.Street)
    Assert.Equal("Springfield", result.Address.City)
    Assert.Equal("62701", result.Address.PostCode)
    Assert.Equal(Some "alice@example.com", result.Email)

[<Fact>]
let ``Person with None email roundtrips`` () =
    let original =
        { Name = "Bob"
          Age = 25
          Address = { Street = "456 Oak Ave"; City = "Shelbyville"; PostCode = "37160" }
          Email = None }
    let result = roundtrip original
    Assert.Equal("Bob", result.Name)
    Assert.Equal(None, result.Email)

// ── Streaming Types Serialization ──

[<Fact>]
let ``TickData roundtrips correctly`` () =
    let original = { Symbol = "MSFT"; Price = 432.10; Volume = 15000 }
    let result = roundtrip original
    Assert.Equal("MSFT", result.Symbol)
    Assert.Equal(432.10, result.Price)
    Assert.Equal(15000, result.Volume)

[<Fact>]
let ``ChatMessage roundtrips correctly`` () =
    let original = { User = "alice"; Text = "Hello there!" }
    let result = roundtrip original
    Assert.Equal("alice", result.User)
    Assert.Equal("Hello there!", result.Text)

[<Fact>]
let ``DataChunk roundtrips correctly`` () =
    let original = { Data = "base64data=="; Index = 42 }
    let result = roundtrip original
    Assert.Equal("base64data==", result.Data)
    Assert.Equal(42, result.Index)

[<Fact>]
let ``UploadResult roundtrips correctly`` () =
    let original = { ChunksReceived = 100 }
    let result = roundtrip original
    Assert.Equal(100, result.ChunksReceived)

// ── OrderResult Pattern ──

[<Fact>]
let ``OrderResult ok roundtrips`` () =
    let original = OrderResult.ok { OrderId = "abc"; Total = 49.98m }
    let result = roundtrip original
    Assert.Equal(0, result.ErrorCode)
    Assert.True(System.String.IsNullOrEmpty(result.Error))
    Assert.Equal("abc", result.Confirmation.OrderId)
    Assert.Equal(49.98m, result.Confirmation.Total)

[<Fact>]
let ``OrderResult error roundtrips`` () =
    let original = OrderResult.error 400 "Bad request"
    let result = roundtrip original
    Assert.Equal(400, result.ErrorCode)
    Assert.Equal("Bad request", result.Error)

// ── Edge Cases ──

[<Fact>]
let ``Empty string fields roundtrip`` () =
    let original = { Name = "" }
    let result = roundtrip<GreetRequest> original
    // protobuf default for string is ""
    Assert.Equal("", result.Name)

[<Fact>]
let ``Unicode string fields roundtrip`` () =
    let original = { Name = "\u0414\u043C\u0438\u0442\u0440\u0438\u0439 \u{1F600}" }
    let result = roundtrip original
    Assert.Equal("\u0414\u043C\u0438\u0442\u0440\u0438\u0439 \u{1F600}", result.Name)

[<Fact>]
let ``Large array roundtrips`` () =
    let lines = [| for i in 1..1000 -> { ProductId = $"P{i}"; Quantity = i; UnitPrice = decimal i } |]
    let original = { Id = "big"; Lines = lines; Tags = Dictionary<string,string>() }
    let result = roundtrip original
    Assert.Equal(1000, result.Lines.Length)
    Assert.Equal("P500", result.Lines.[499].ProductId)

[<Fact>]
let ``Decimal precision preserved`` () =
    let original = { ProductId = "X"; Quantity = 1; UnitPrice = 123456.789m }
    let result = roundtrip original
    Assert.Equal(123456.789m, result.UnitPrice)

[<Fact>]
let ``Negative values roundtrip`` () =
    let original = { ProductId = "X"; Quantity = -5; UnitPrice = -10.0m }
    let result = roundtrip original
    Assert.Equal(-5, result.Quantity)
    Assert.Equal(-10.0m, result.UnitPrice)

[<Fact>]
let ``Zero values roundtrip`` () =
    let original = { ProductId = ""; Quantity = 0; UnitPrice = 0m }
    let result = roundtrip original
    Assert.Equal(0, result.Quantity)
    Assert.Equal(0m, result.UnitPrice)

[<Fact>]
let ``SearchReply with results array roundtrips`` () =
    let original =
        { Results =
            [| { Id = "1"; Title = "Result 1"; Score = 0.99 }
               { Id = "2"; Title = "Result 2"; Score = 0.85 } |]
          TotalCount = 42 }
    let result = roundtrip original
    Assert.Equal(2, result.Results.Length)
    Assert.Equal(42, result.TotalCount)
    Assert.Equal(0.99, result.Results.[0].Score)
