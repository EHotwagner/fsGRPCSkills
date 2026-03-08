namespace MyApp.Server.Services

open System.Threading.Tasks
open ProtoBuf.Grpc
open MyApp.Shared.Contracts

/// Greeter service implementation (code-first).
type GreeterService() =
    interface IGreeterService with
        member _.SayHello(request, _context) =
            let reply =
                { Message = $"Hello, {request.Name}!" }
            ValueTask<GreetReply>(reply)
