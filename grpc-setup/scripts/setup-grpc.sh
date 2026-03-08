#!/usr/bin/env bash
# setup-grpc.sh — Bootstrap gRPC infrastructure for an F# solution
#
# Usage:
#   ./setup-grpc.sh --approach codefirst|contractfirst [--name MySolution] [--dir .]
#
# Flags:
#   --approach   Required. "codefirst" or "contractfirst"
#   --name       Solution name (default: current directory name)
#   --dir        Target directory (default: current directory)
#   --help       Show this help message

set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

info()  { echo -e "${BLUE}[info]${NC} $1"; }
ok()    { echo -e "${GREEN}[ok]${NC} $1"; }
warn()  { echo -e "${YELLOW}[warn]${NC} $1"; }
error() { echo -e "${RED}[error]${NC} $1"; }

APPROACH=""
NAME=""
DIR="."

while [[ $# -gt 0 ]]; do
    case $1 in
        --approach) APPROACH="$2"; shift 2 ;;
        --name)     NAME="$2"; shift 2 ;;
        --dir)      DIR="$2"; shift 2 ;;
        --help)
            head -12 "$0" | tail -10
            exit 0
            ;;
        *) error "Unknown option: $1"; exit 1 ;;
    esac
done

if [[ -z "$APPROACH" ]]; then
    error "Missing --approach. Use 'codefirst' or 'contractfirst'."
    exit 1
fi

cd "$DIR"
if [[ -z "$NAME" ]]; then
    NAME="$(basename "$(pwd)")"
fi

info "Setting up gRPC solution: $NAME (approach: $APPROACH)"

# Create solution
if [[ ! -f "$NAME.sln" ]]; then
    dotnet new sln -n "$NAME" --force
    ok "Created solution: $NAME.sln"
else
    warn "Solution $NAME.sln already exists"
fi

if [[ "$APPROACH" == "codefirst" ]]; then
    # Shared contracts project
    info "Creating Shared contracts project..."
    mkdir -p Shared
    dotnet new classlib -lang F# -n Shared -o Shared --framework net9.0 --force
    rm -f Shared/Library.fs

    dotnet add Shared/Shared.fsproj package protobuf-net --version 3.2.45
    dotnet add Shared/Shared.fsproj package protobuf-net-fsharp --version 0.1.0
    dotnet add Shared/Shared.fsproj package protobuf-net.Grpc --version 1.1.1
    dotnet add Shared/Shared.fsproj package System.ServiceModel.Primitives --version 8.1.0
    ok "Created Shared project with protobuf-net packages"

    # Server project
    info "Creating Server project..."
    mkdir -p Server
    dotnet new web -lang F# -n Server -o Server --framework net9.0 --force
    dotnet add Server/Server.fsproj package protobuf-net.Grpc.AspNetCore --version 1.1.1
    dotnet add Server/Server.fsproj package protobuf-net-fsharp --version 0.1.0
    dotnet add Server/Server.fsproj reference Shared/Shared.fsproj
    ok "Created Server project with protobuf-net.Grpc.AspNetCore"

    # Client project
    info "Creating Client project..."
    mkdir -p Client
    dotnet new console -lang F# -n Client -o Client --framework net9.0 --force
    rm -f Client/Program.fs
    dotnet add Client/Client.fsproj package Grpc.Net.Client --version 2.67.0
    dotnet add Client/Client.fsproj package protobuf-net.Grpc.Native --version 1.1.1
    dotnet add Client/Client.fsproj package protobuf-net-fsharp --version 0.1.0
    dotnet add Client/Client.fsproj reference Shared/Shared.fsproj
    ok "Created Client project with protobuf-net.Grpc.Native"

elif [[ "$APPROACH" == "contractfirst" ]]; then
    # Proto directory
    info "Creating Protos directory..."
    mkdir -p Protos

    # C# proto library (standard tooling generates C#)
    info "Creating ProtoLib (C#) project..."
    mkdir -p ProtoLib
    dotnet new classlib -lang C# -n ProtoLib -o ProtoLib --framework net9.0 --force
    rm -f ProtoLib/Class1.cs
    dotnet add ProtoLib/ProtoLib.csproj package Grpc.AspNetCore --version 2.67.0
    dotnet add ProtoLib/ProtoLib.csproj package Google.Protobuf --version 3.29.3
    dotnet add ProtoLib/ProtoLib.csproj package Grpc.Tools --version 2.69.0
    ok "Created ProtoLib project for C# code generation"

    # Server project
    info "Creating Server project..."
    mkdir -p Server
    dotnet new web -lang F# -n Server -o Server --framework net9.0 --force
    dotnet add Server/Server.fsproj package Grpc.AspNetCore --version 2.67.0
    dotnet add Server/Server.fsproj reference ProtoLib/ProtoLib.csproj
    ok "Created Server project"

    # Client project
    info "Creating Client project..."
    mkdir -p Client
    dotnet new console -lang F# -n Client -o Client --framework net9.0 --force
    rm -f Client/Program.fs
    dotnet add Client/Client.fsproj package Grpc.Net.Client --version 2.67.0
    dotnet add Client/Client.fsproj package Google.Protobuf --version 3.29.3
    dotnet add Client/Client.fsproj reference ProtoLib/ProtoLib.csproj
    ok "Created Client project"
else
    error "Unknown approach: $APPROACH. Use 'codefirst' or 'contractfirst'."
    exit 1
fi

# Add all projects to solution
info "Adding projects to solution..."
for proj in $(find . -name "*.fsproj" -o -name "*.csproj" | sort); do
    dotnet sln add "$proj" 2>/dev/null || true
done
ok "All projects added to solution"

# Verify build
info "Verifying build..."
if dotnet build --nologo -v q 2>&1; then
    ok "Solution builds successfully!"
else
    warn "Build has warnings or errors. Review the output above."
fi

echo ""
ok "gRPC setup complete for $NAME ($APPROACH approach)"
info "Next steps:"
if [[ "$APPROACH" == "codefirst" ]]; then
    echo "  1. Define contracts in Shared/Contracts.fs"
    echo "  2. Implement services in Server/Services/"
    echo "  3. Configure host in Server/Program.fs"
    echo "  4. Write client code in Client/Program.fs"
else
    echo "  1. Write .proto files in Protos/"
    echo "  2. Add <Protobuf> items to ProtoLib.csproj"
    echo "  3. Implement services in Server/"
    echo "  4. Write client code in Client/Program.fs"
fi
