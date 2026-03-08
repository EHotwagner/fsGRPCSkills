module GrpcSkills.Tests.ContractValidationTests

open Xunit
open System
open System.Reflection
open GrpcSkills.Tests.Contracts
open ProtoBuf

/// Validate that all contracts have correct protobuf attributes.

// ── ProtoContract Attribute Validation ──

[<Fact>]
let ``GreetRequest has ProtoContract attribute`` () =
    let attr = typeof<GreetRequest>.GetCustomAttribute<ProtoContractAttribute>()
    Assert.NotNull(attr)

[<Fact>]
let ``GreetReply has ProtoContract attribute`` () =
    let attr = typeof<GreetReply>.GetCustomAttribute<ProtoContractAttribute>()
    Assert.NotNull(attr)

[<Fact>]
let ``SearchRequest has ProtoContract attribute`` () =
    let attr = typeof<SearchRequest>.GetCustomAttribute<ProtoContractAttribute>()
    Assert.NotNull(attr)

[<Fact>]
let ``OrderLine has ProtoContract attribute`` () =
    let attr = typeof<OrderLine>.GetCustomAttribute<ProtoContractAttribute>()
    Assert.NotNull(attr)

[<Fact>]
let ``Order has ProtoContract attribute`` () =
    let attr = typeof<Order>.GetCustomAttribute<ProtoContractAttribute>()
    Assert.NotNull(attr)

[<Fact>]
let ``Person has ProtoContract attribute`` () =
    let attr = typeof<Person>.GetCustomAttribute<ProtoContractAttribute>()
    Assert.NotNull(attr)

[<Fact>]
let ``Address has ProtoContract attribute`` () =
    let attr = typeof<Address>.GetCustomAttribute<ProtoContractAttribute>()
    Assert.NotNull(attr)

// ── ServiceContract Attribute Validation ──

[<Fact>]
let ``IGreeterService has ServiceContract attribute`` () =
    let attr = typeof<IGreeterService>.GetCustomAttribute<System.ServiceModel.ServiceContractAttribute>()
    Assert.NotNull(attr)

[<Fact>]
let ``ISearchService has ServiceContract attribute`` () =
    let attr = typeof<ISearchService>.GetCustomAttribute<System.ServiceModel.ServiceContractAttribute>()
    Assert.NotNull(attr)

[<Fact>]
let ``IOrderService has ServiceContract attribute`` () =
    let attr = typeof<IOrderService>.GetCustomAttribute<System.ServiceModel.ServiceContractAttribute>()
    Assert.NotNull(attr)

[<Fact>]
let ``ITickerService has ServiceContract attribute`` () =
    let attr = typeof<ITickerService>.GetCustomAttribute<System.ServiceModel.ServiceContractAttribute>()
    Assert.NotNull(attr)

[<Fact>]
let ``IChatService has ServiceContract attribute`` () =
    let attr = typeof<IChatService>.GetCustomAttribute<System.ServiceModel.ServiceContractAttribute>()
    Assert.NotNull(attr)

// ── OperationContract Validation ──

let hasOperationContract (t: Type) (methodName: string) =
    let method = t.GetMethod(methodName)
    Assert.NotNull(method)
    let attr = method.GetCustomAttribute<System.ServiceModel.OperationContractAttribute>()
    Assert.NotNull(attr)

[<Fact>]
let ``IGreeterService.SayHello has OperationContract`` () =
    hasOperationContract typeof<IGreeterService> "SayHello"

[<Fact>]
let ``ISearchService.Search has OperationContract`` () =
    hasOperationContract typeof<ISearchService> "Search"

[<Fact>]
let ``IOrderService.PlaceOrder has OperationContract`` () =
    hasOperationContract typeof<IOrderService> "PlaceOrder"

[<Fact>]
let ``ITickerService.Subscribe has OperationContract`` () =
    hasOperationContract typeof<ITickerService> "Subscribe"

[<Fact>]
let ``IChatService.Chat has OperationContract`` () =
    hasOperationContract typeof<IChatService> "Chat"

// ── ProtoMember Field Number Uniqueness ──

let getProtoMemberNumbers (t: Type) =
    t.GetProperties()
    |> Array.choose (fun p ->
        let attr = p.GetCustomAttribute<ProtoMemberAttribute>()
        if attr <> null then Some (p.Name, attr.Tag) else None)
    |> Array.toList

[<Fact>]
let ``GreetRequest has unique field numbers`` () =
    let numbers = getProtoMemberNumbers typeof<GreetRequest>
    let tags = numbers |> List.map snd
    Assert.Equal(tags.Length, (tags |> List.distinct |> List.length))

[<Fact>]
let ``SearchRequest has unique field numbers`` () =
    let numbers = getProtoMemberNumbers typeof<SearchRequest>
    let tags = numbers |> List.map snd
    Assert.Equal(tags.Length, (tags |> List.distinct |> List.length))

[<Fact>]
let ``Order has unique field numbers`` () =
    let numbers = getProtoMemberNumbers typeof<Order>
    let tags = numbers |> List.map snd
    Assert.Equal(tags.Length, (tags |> List.distinct |> List.length))

[<Fact>]
let ``Person has unique field numbers`` () =
    let numbers = getProtoMemberNumbers typeof<Person>
    let tags = numbers |> List.map snd
    Assert.Equal(tags.Length, (tags |> List.distinct |> List.length))

// ── ProtoMember Field Numbers are Positive ──

[<Fact>]
let ``All ProtoMember tags are positive`` () =
    let types =
        [ typeof<GreetRequest>; typeof<GreetReply>; typeof<SearchRequest>
          typeof<SearchResult>; typeof<SearchReply>; typeof<OrderLine>
          typeof<Order>; typeof<OrderConfirmation>; typeof<Address>
          typeof<Person>; typeof<TickData>; typeof<SubscribeRequest>
          typeof<ChatMessage>; typeof<DataChunk>; typeof<UploadResult>
          typeof<OrderResult> ]

    for t in types do
        let numbers = getProtoMemberNumbers t
        for (name, tag) in numbers do
            Assert.True(tag > 0, $"{t.Name}.{name} has non-positive tag {tag}")

// ── Service Method Return Type Validation ──

[<Fact>]
let ``IGreeterService.SayHello returns ValueTask`` () =
    let method = typeof<IGreeterService>.GetMethod("SayHello")
    Assert.True(method.ReturnType.IsGenericType)
    Assert.Equal(typedefof<System.Threading.Tasks.ValueTask<_>>, method.ReturnType.GetGenericTypeDefinition())

[<Fact>]
let ``ITickerService.Subscribe returns IAsyncEnumerable`` () =
    let method = typeof<ITickerService>.GetMethod("Subscribe")
    Assert.True(method.ReturnType.IsGenericType)
    Assert.Equal(
        typedefof<System.Collections.Generic.IAsyncEnumerable<_>>,
        method.ReturnType.GetGenericTypeDefinition())

[<Fact>]
let ``IChatService.Chat accepts and returns IAsyncEnumerable`` () =
    let method = typeof<IChatService>.GetMethod("Chat")
    Assert.True(method.ReturnType.IsGenericType)
    Assert.Equal(
        typedefof<System.Collections.Generic.IAsyncEnumerable<_>>,
        method.ReturnType.GetGenericTypeDefinition())
    let param = method.GetParameters().[0]
    Assert.True(param.ParameterType.IsGenericType)
    Assert.Equal(
        typedefof<System.Collections.Generic.IAsyncEnumerable<_>>,
        param.ParameterType.GetGenericTypeDefinition())
