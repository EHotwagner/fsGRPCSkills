---
name: fsgrpc-setup
description: >-
  Use when the user asks to "set up gRPC", "add gRPC to my F# project",
  "initialize gRPC", "configure gRPC tooling", "install gRPC packages",
  "create a gRPC project", or needs to bootstrap gRPC infrastructure in an
  F# solution.
version: 0.1.0
---

# gRPC Setup for F#

Set up gRPC tooling, packages, and project configuration for F# projects.

## Overview

This skill initialises all infrastructure needed to use gRPC from F#. It covers
three approaches and helps the user choose the right one:

| Approach | When to use | Key packages |
|----------|-------------|--------------|
| **Contract-first (FsGrpc)** | Existing .proto files, want idiomatic F# records & DUs | `FsGrpc`, `buf` CLI, `protoc-gen-fsgrpc` on PATH |
| **Contract-first (standard)** | Existing .proto files, okay with C# interop types | `Grpc.AspNetCore`, `Google.Protobuf`, `Grpc.Tools` |
| **Code-first (protobuf-net)** | No .proto files, F#-native types as contracts | `protobuf-net.Grpc.AspNetCore`, `protobuf-net.Grpc`, `protobuf-net-fsharp` |

## Workflow

### 1. Determine the approach

Ask the user:

- **Do you have existing .proto files?** If yes, recommend contract-first.
  - Want idiomatic F# (records, DUs)? Use **FsGrpc**.
  - Need maximum compatibility with C# ecosystem? Use **standard Grpc.Tools**.
- **Building from scratch in F#?** Recommend **code-first with protobuf-net**.

### 2. Create or update the .fsproj

#### Contract-first with FsGrpc

```xml
<ItemGroup>
  <PackageReference Include="FsGrpc" Version="1.0.6" />
</ItemGroup>
```

> **Do not** add a `FsGrpc.Tools` PackageReference. Earlier versions of this
> skill recommended `FsGrpc.Tools 1.0.6` with MSBuild auto-generation, but
> that package is not published on nuget.org (highest is `0.6.3`) and its
> MSBuild targets still expect `protoc-gen-fsgrpc` to be resolvable on
> PATH. Use the manual generation workflow described in `fsgrpc-proto`
> instead — it is the only reproducible path at the time of writing.

Requires the `buf` CLI **and** the `protoc-gen-fsgrpc` plugin installed:

```bash
# 1. Install buf (https://buf.build/docs/installation)
# macOS
brew install bufbuild/buf/buf
# Linux
curl -sSL https://github.com/bufbuild/buf/releases/latest/download/buf-Linux-x86_64 -o /usr/local/bin/buf && chmod +x /usr/local/bin/buf
# Or via npm
npm install -g @bufbuild/buf

# 2. Install the F# protoc plugin. No prebuilt binary is published for this
#    plugin, so the helper script in this skill builds it from source against
#    a pinned dmgtech/fsgrpc commit and installs a wrapper on PATH:
./fsgrpc-setup/scripts/install-protoc-gen-fsgrpc.sh
```

See `fsgrpc-setup/scripts/install-protoc-gen-fsgrpc.sh --help` for why this
extra step is necessary and how to pin the source ref / target framework.

#### Contract-first with standard tooling

```xml
<ItemGroup>
  <PackageReference Include="Grpc.AspNetCore" Version="2.67.0" />
  <PackageReference Include="Google.Protobuf" Version="3.29.3" />
  <PackageReference Include="Grpc.Tools" Version="2.69.0"
                    PrivateAssets="all" IncludeAssets="build" />
</ItemGroup>

<ItemGroup>
  <Protobuf Include="Protos\*.proto" GrpcServices="Both" />
</ItemGroup>
```

> **Note**: Grpc.Tools generates C# classes. F# projects consume these via a
> project reference to a C# class library that holds the generated code.

#### Code-first with protobuf-net

**Server project (.fsproj)**:
```xml
<ItemGroup>
  <PackageReference Include="protobuf-net.Grpc.AspNetCore" Version="1.1.1" />
  <PackageReference Include="protobuf-net-fsharp" Version="0.1.0" />
</ItemGroup>
```

**Client project (.fsproj)**:
```xml
<ItemGroup>
  <PackageReference Include="protobuf-net.Grpc.Native" Version="1.1.1" />
  <PackageReference Include="protobuf-net-fsharp" Version="0.1.0" />
</ItemGroup>
```

**Shared contracts project (.fsproj)**:
```xml
<ItemGroup>
  <PackageReference Include="protobuf-net" Version="3.2.45" />
  <PackageReference Include="protobuf-net-fsharp" Version="0.1.0" />
  <PackageReference Include="System.ServiceModel.Primitives" Version="8.1.0" />
</ItemGroup>
```

### 3. Register protobuf-net-fsharp serializer (code-first only)

At application startup, before any serialization occurs, register each F# record
type individually using `Serialiser.registerRecordRuntimeTypeIntoModel`:

```fsharp
open ProtoBuf.FSharp

// In Program.fs or Startup, before building the host:
let model = Serialiser.defaultModel

Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetRequest> model |> ignore
Serialiser.registerRecordRuntimeTypeIntoModel typeof<GreetReply> model |> ignore
// ... register all F# record types used in contracts
```

> **Note**: The module is `ProtoBuf.FSharp.Serialiser` (British spelling).

### 4. Project structure

#### Recommended solution layout (code-first)

```
MySolution/
├── MySolution.sln
├── Shared/                  # Contract definitions
│   ├── Shared.fsproj
│   └── Contracts.fs         # F# records + service interfaces
├── Server/
│   ├── Server.fsproj        # References Shared
│   ├── Program.fs
│   └── Services/
│       └── GreeterService.fs
└── Client/
    ├── Client.fsproj        # References Shared
    └── Program.fs
```

#### Recommended solution layout (contract-first with FsGrpc)

```
MySolution/
├── MySolution.sln
├── Protos/                  # .proto source files
│   ├── buf.yaml
│   └── greeter.proto
├── Generated/               # FsGrpc-generated F# code
│   ├── Generated.fsproj
│   └── Greeter.fs           # Auto-generated
├── Server/
│   ├── Server.fsproj        # References Generated
│   └── Program.fs
└── Client/
    ├── Client.fsproj        # References Generated
    └── Program.fs
```

### 5. Verify setup

```bash
dotnet build
```

If using FsGrpc, also verify buf is available:

```bash
buf --version
```

## Best Practices

- **DO** use `protobuf-net-fsharp` with code-first to handle F# records, options, and DUs correctly
- **DO** register each F# record type with `Serialiser.registerRecordRuntimeTypeIntoModel` before any serialization
- **DO** keep shared contracts in a separate project referenced by both server and client
- **DO** pin package versions in production projects
- **DON'T** mix contract-first and code-first in the same service
- **DON'T** use standard Grpc.Tools directly in F# projects; use a C# intermediary or FsGrpc
- **DON'T** forget to install the buf CLI **and** `protoc-gen-fsgrpc` (via
  `scripts/install-protoc-gen-fsgrpc.sh`) when using FsGrpc — both are required

## Additional Resources

- [grpc-dotnet GitHub](https://github.com/grpc/grpc-dotnet)
- [protobuf-net.Grpc](https://github.com/protobuf-net/protobuf-net.Grpc)
- [protobuf-net-fsharp](https://github.com/mlivernoche/protobuf-net-fsharp)
- [FsGrpc](https://github.com/mzgkits/FsGrpc)
- Templates: `templates/codefirst.fsproj`, `templates/contractfirst.fsproj`
- Scripts: `scripts/setup-grpc.sh`, `scripts/install-protoc-gen-fsgrpc.sh`

## Implementation Workflow

1. Determine approach (contract-first vs code-first)
2. Create solution and project structure
3. Add NuGet package references
4. Install external tooling if needed (buf CLI + `protoc-gen-fsgrpc` for FsGrpc)
5. Register F# serializer if using protobuf-net
6. Run `dotnet build` to verify
