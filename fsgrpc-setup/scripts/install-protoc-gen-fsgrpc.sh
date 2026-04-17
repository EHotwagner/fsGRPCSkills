#!/usr/bin/env bash
# install-protoc-gen-fsgrpc.sh — Build and install protoc-gen-fsgrpc from source.
#
# Why this script exists
# -----------------------
# The `FsGrpc` NuGet package (currently 1.0.6 from dmgtech/fsgrpc) ships the
# F# runtime types used by generated protobuf code, but **there is no public
# distribution of the matching `protoc-gen-fsgrpc` plugin binary**:
#
#   * No prebuilt plugin is attached to any GitHub release
#     (https://github.com/dmgtech/fsgrpc/releases has only source tarballs).
#   * `FsGrpc.Tools 1.0.6` is NOT published on nuget.org. The highest version
#     on the feed is 0.6.3, and its MSBuild targets still expect
#     `protoc-gen-fsgrpc` to be present on PATH.
#   * The plugin source was removed from the dmgtech/fsgrpc main branch in
#     commit cbc3860 ("chore: remove unused protocgen projecto"). The last
#     commit that contains a buildable plugin is a52b8a7.
#   * The plugin in a52b8a7 unconditionally emits Focal-based optics modules
#     (`IPrism'`, `ILens'`, ...) that reference a `Focal.Core` runtime the
#     published FsGrpc 1.0.6 NuGet does not depend on. Generated code
#     therefore fails to compile against FsGrpc 1.0.6 unless the optics
#     branch is skipped.
#
# This script automates the only reproducible path that actually works today:
# clone dmgtech/fsgrpc at a52b8a7, retarget to a current .NET SDK, patch one
# line in ProtoCodeGen.fs to skip optics emission, `dotnet publish`, and
# install a wrapper on PATH.
#
# Usage
# -----
#   install-protoc-gen-fsgrpc.sh [--prefix DIR] [--tfm TFM] [--ref GIT_REF]
#                                [--force] [--help]
#
#   --prefix DIR   Install prefix; wrapper lands at DIR/bin/protoc-gen-fsgrpc
#                  and payload at DIR/share/protoc-gen-fsgrpc/.
#                  Default: $HOME/.local
#   --tfm TFM      .NET target framework to publish for (e.g. net8.0, net10.0).
#                  Default: autodetect — highest net* SDK visible to `dotnet`.
#   --ref GIT_REF  dmgtech/fsgrpc commit/branch/tag to build from.
#                  Default: a52b8a7 (last commit before plugin removal).
#   --force        Rebuild even if protoc-gen-fsgrpc already resolves on PATH.
#   --help         Print this help and exit.
#
# Requirements
# ------------
#   * dotnet SDK 6.0 or newer (7.0+ strongly recommended; net6.0 is EOL).
#   * git
#   * bash
#
# Output
# ------
#   $PREFIX/bin/protoc-gen-fsgrpc            — bash wrapper (invokes dotnet)
#   $PREFIX/share/protoc-gen-fsgrpc/*.dll    — plugin payload
#
# If $PREFIX/bin is not on PATH this script prints a one-line export hint.

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

info()  { echo -e "${BLUE}[info]${NC} $1"; }
ok()    { echo -e "${GREEN}[ok]${NC} $1"; }
warn()  { echo -e "${YELLOW}[warn]${NC} $1" >&2; }
die()   { echo -e "${RED}[error]${NC} $1" >&2; exit 1; }

PREFIX="${HOME}/.local"
TFM=""
REF="a52b8a7"
FORCE=0
REPO_URL="https://github.com/dmgtech/fsgrpc.git"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --prefix) PREFIX="$2"; shift 2 ;;
        --tfm)    TFM="$2"; shift 2 ;;
        --ref)    REF="$2"; shift 2 ;;
        --force)  FORCE=1; shift ;;
        --help|-h)
            sed -n '3,60p' "$0" | sed 's/^# \{0,1\}//'
            exit 0
            ;;
        *) die "Unknown flag: $1 (use --help)" ;;
    esac
done

command -v git >/dev/null    || die "git is required but not on PATH"
command -v dotnet >/dev/null || die "dotnet SDK is required but not on PATH"

# Short-circuit if already installed and working, unless --force.
if [[ $FORCE -eq 0 ]] && command -v protoc-gen-fsgrpc >/dev/null 2>&1; then
    EXISTING="$(command -v protoc-gen-fsgrpc)"
    ok "protoc-gen-fsgrpc already on PATH at $EXISTING — skip with --force to rebuild."
    exit 0
fi

# Autodetect TFM if the caller didn't pin one. Prefer the newest installed
# runtime; fall back to the SDK list.
if [[ -z "$TFM" ]]; then
    # e.g. "Microsoft.NETCore.App 10.0.1 [/usr/share/dotnet/shared/...]"
    TFM="$(dotnet --list-runtimes 2>/dev/null \
           | awk '/^Microsoft\.NETCore\.App / {print $2}' \
           | sort -V | tail -n 1 \
           | awk -F. '{ printf "net%d.%d\n", $1, $2 }')"
    if [[ -z "$TFM" ]]; then
        TFM="net8.0"
        warn "Could not autodetect a .NET runtime; defaulting to $TFM"
    else
        info "Autodetected target framework: $TFM"
    fi
fi

WORK="$(mktemp -d -t fsgrpc-build-XXXXXX)"
trap 'rm -rf "$WORK"' EXIT

info "Cloning $REPO_URL at $REF into $WORK"
git clone --quiet --no-checkout "$REPO_URL" "$WORK/fsgrpc"
(cd "$WORK/fsgrpc" && git checkout --quiet "$REF")

SRC="$WORK/fsgrpc"

# Retarget both the plugin and its FsGrpc runtime dependency to the requested
# TFM. The a52b8a7 sources target net6.0, which is EOL on many desktops.
info "Retargeting plugin + FsGrpc runtime to $TFM"
sed -i "s#<TargetFramework>net6.0</TargetFramework>#<TargetFramework>${TFM}</TargetFramework>#" \
    "$SRC/protoc-gen-fsgrpc/protoc-gen-fsgrpc.fsproj" \
    "$SRC/FsGrpc/FsGrpc.fsproj"

# Patch: disable optics emission.
#
# ProtoCodeGen.fs line ~1087 reads
#   if Seq.isEmpty opticsMessageDefs then Frag []
# which is the condition that decides whether to emit the `<NS>.Optics`
# sub-namespace referencing `Focal.Core`. The published FsGrpc 1.0.6 NuGet
# does not take a Focal.Core runtime dependency, so any generated code that
# lands in the optics branch fails to compile for consumers on that package.
# Forcing the branch off produces output byte-compatible with the committed
# `.gen.fs` files in projects that pin FsGrpc 1.0.6.
info "Applying optics-skip patch to ProtoCodeGen.fs"
PATCH_TARGET="$SRC/protoc-gen-fsgrpc/ProtoCodeGen.fs"
if ! grep -q "if Seq.isEmpty opticsMessageDefs then Frag \[\]" "$PATCH_TARGET"; then
    die "Optics-skip anchor not found in $PATCH_TARGET — has the plugin source drifted? Re-run with --ref <sha> pinned."
fi
sed -i 's#if Seq.isEmpty opticsMessageDefs then Frag \[\]#if true || Seq.isEmpty opticsMessageDefs then Frag []#' "$PATCH_TARGET"

PUBLISH_OUT="$WORK/publish"
info "Running dotnet publish (this takes ~30-60s on first run)"
dotnet publish "$SRC/protoc-gen-fsgrpc/protoc-gen-fsgrpc.fsproj" \
    -c Release -o "$PUBLISH_OUT" --nologo -v quiet

[[ -f "$PUBLISH_OUT/protoc-gen-fsgrpc.dll" ]] \
    || die "Build succeeded but protoc-gen-fsgrpc.dll was not produced at $PUBLISH_OUT"

# Install the payload + wrapper.
SHARE_DIR="$PREFIX/share/protoc-gen-fsgrpc"
BIN_DIR="$PREFIX/bin"
WRAPPER="$BIN_DIR/protoc-gen-fsgrpc"

info "Installing payload → $SHARE_DIR"
mkdir -p "$SHARE_DIR" "$BIN_DIR"
rm -rf "$SHARE_DIR"/*
cp -r "$PUBLISH_OUT"/. "$SHARE_DIR"/

info "Installing wrapper → $WRAPPER"
cat > "$WRAPPER" <<'WRAPPER_EOF'
#!/usr/bin/env bash
# protoc-gen-fsgrpc wrapper.
# Installed by fsGRPCSkills install-protoc-gen-fsgrpc.sh.
# Payload lives alongside this script under ../share/protoc-gen-fsgrpc/.
set -eu
here="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"
exec dotnet "$here/../share/protoc-gen-fsgrpc/protoc-gen-fsgrpc.dll" "$@"
WRAPPER_EOF
chmod +x "$WRAPPER"

ok "Installed protoc-gen-fsgrpc ($TFM, ref $REF, optics-disabled)"
info "  wrapper : $WRAPPER"
info "  payload : $SHARE_DIR"

# PATH hint. Do not mutate the user's shell profile.
case ":${PATH:-}:" in
    *":$BIN_DIR:"*) ;;
    *)
        warn "$BIN_DIR is not on your PATH. Add it with:"
        echo "    export PATH=\"$BIN_DIR:\$PATH\""
        ;;
esac
