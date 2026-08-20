#!/bin/bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RELEASES_DIR="$REPO_ROOT/Releases"
APP_NAME="DeezFuelGauge"
APP_BUNDLE_NAME="Deez Fuel Gauge.app"
APP_PATH="$REPO_ROOT/$APP_BUNDLE_NAME"
INFO_PLIST="$REPO_ROOT/packaging/macos/Info.plist"
PROJECT="$REPO_ROOT/DeezFuelGauge/DeezFuelGauge.csproj"
ARM64_PUBLISH="$REPO_ROOT/.publish/osx-arm64"
X64_PUBLISH="$REPO_ROOT/.publish/osx-x64"
ARM64_HOST="$ARM64_PUBLISH/$APP_NAME"
X64_HOST="$X64_PUBLISH/$APP_NAME"
UNIVERSAL_HOST="$REPO_ROOT/.publish/universal/$APP_NAME"
UNIVERSAL_LIB_DIR="$REPO_ROOT/.publish/universal"
ZIP_NAME="deez-fuel-gauge-mac-Universal.zip"
ZIP_PATH="$RELEASES_DIR/$ZIP_NAME"

find_dotnet() {
    if command -v dotnet >/dev/null 2>&1; then
        command -v dotnet
        return 0
    fi

    local candidate
    for candidate in \
        "$HOME/.dotnet/dotnet" \
        "/usr/local/share/dotnet/dotnet" \
        "/opt/homebrew/bin/dotnet" \
        "/usr/local/bin/dotnet"
    do
        if [[ -x "$candidate" ]]; then
            echo "$candidate"
            return 0
        fi
    done

    return 1
}

DOTNET="$(find_dotnet)"
if [[ -z "$DOTNET" ]]; then
    echo ".NET 8 SDK not found." >&2
    exit 1
fi

dotnet_publish() {
    local rid="$1"
    local arch="$2"
    local output="$3"
    "$DOTNET" publish "$PROJECT" \
        -c Release \
        -r "$rid" \
        --arch "$arch" \
        --self-contained true \
        -p:UseAppHost=true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=false \
        -o "$output" \
        --nologo
}

rm -rf "$REPO_ROOT/.publish"
mkdir -p "$RELEASES_DIR" "$REPO_ROOT/.publish/universal" "$UNIVERSAL_LIB_DIR"

dotnet_publish osx-arm64 arm64 "$ARM64_PUBLISH"
dotnet_publish osx-x64 x64 "$X64_PUBLISH"

if [[ ! -x "$ARM64_HOST" || ! -x "$X64_HOST" ]]; then
    echo "Published app host was not created." >&2
    exit 1
fi

if command -v lipo >/dev/null 2>&1; then
    lipo -create "$ARM64_HOST" "$X64_HOST" -output "$UNIVERSAL_HOST"
else
    case "$(uname -m)" in
        arm64) cp "$ARM64_HOST" "$UNIVERSAL_HOST" ;;
        x86_64) cp "$X64_HOST" "$UNIVERSAL_HOST" ;;
        *) echo "Unsupported macOS architecture: $(uname -m)" >&2; exit 1 ;;
    esac
fi

chmod +x "$UNIVERSAL_HOST"

if [[ -d "$APP_PATH" ]]; then
    rm -rf "$APP_PATH"
fi

mkdir -p "$APP_PATH/Contents/MacOS" "$APP_PATH/Contents/Resources"
cp "$INFO_PLIST" "$APP_PATH/Contents/Info.plist"
printf 'APPL????' > "$APP_PATH/Contents/PkgInfo"
if [[ -f "$REPO_ROOT/packaging/icons/AppIcon.icns" ]]; then
    cp "$REPO_ROOT/packaging/icons/AppIcon.icns" "$APP_PATH/Contents/Resources/AppIcon.icns"
fi
if [[ -d "$REPO_ROOT/packaging/macos/AppIcon.icon" ]]; then
    cp -R "$REPO_ROOT/packaging/macos/AppIcon.icon" "$APP_PATH/Contents/Resources/"
fi
cp "$UNIVERSAL_HOST" "$APP_PATH/Contents/MacOS/$APP_NAME"
chmod +x "$APP_PATH/Contents/MacOS/$APP_NAME"

UNIVERSAL_LIB_DIR="$REPO_ROOT/.publish/universal"
for lib in libAvaloniaNative.dylib libHarfBuzzSharp.dylib libSkiaSharp.dylib; do
    if [[ ! -f "$ARM64_PUBLISH/$lib" || ! -f "$X64_PUBLISH/$lib" ]]; then
        echo "Published native library was not created: $lib" >&2
        exit 1
    fi
    if command -v lipo >/dev/null 2>&1; then
        lipo -create "$ARM64_PUBLISH/$lib" "$X64_PUBLISH/$lib" -output "$UNIVERSAL_LIB_DIR/$lib"
        cp "$UNIVERSAL_LIB_DIR/$lib" "$APP_PATH/Contents/MacOS/$lib"
    else
        case "$(uname -m)" in
            arm64) cp "$ARM64_PUBLISH/$lib" "$APP_PATH/Contents/MacOS/$lib" ;;
            x86_64) cp "$X64_PUBLISH/$lib" "$APP_PATH/Contents/MacOS/$lib" ;;
            *) echo "Unsupported macOS architecture: $(uname -m)" >&2; exit 1 ;;
        esac
    fi
done

if command -v codesign >/dev/null 2>&1; then
    codesign --force --deep --sign - "$APP_PATH" >/dev/null 2>&1 || true
fi

if command -v xattr >/dev/null 2>&1; then
    xattr -cr "$APP_PATH" >/dev/null 2>&1 || true
fi

rm -f "$ZIP_PATH"
ditto -c -k --sequesterRsrc --keepParent "$APP_PATH" "$ZIP_PATH"

rm -rf "$REPO_ROOT/.publish"

echo "$ZIP_PATH"
