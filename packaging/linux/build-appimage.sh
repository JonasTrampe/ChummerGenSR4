#!/usr/bin/env bash
# Builds a single-file, self-contained AppImage for Chummer.Avalonia - Phase 5 of
# PORTING_PLAN.md ("a single, user-friendly .AppImage that bundles the required runtimes
# ... so users can run it on any modern Linux distribution without manual dependency management").
#
# Usage: packaging/linux/build-appimage.sh [RID]
#   RID defaults to linux-x64. Pass linux-arm64 for an ARM64 build.
#
# Requires: dotnet SDK (for publish), and either an `appimagetool` already on PATH or network
# access to download the official AppImage/appimagetool release (cached under packaging/linux/
# after the first run).
set -euo pipefail

RID="${1:-linux-x64}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
PUBLISH_DIR="${REPO_ROOT}/.artifacts/publish/${RID}"
APPDIR="${REPO_ROOT}/.artifacts/appimage/Chummer.AppDir"
OUT_DIR="${REPO_ROOT}/.artifacts/appimage"

echo "==> Publishing Chummer.Avalonia (self-contained, ${RID})..."
rm -rf "${PUBLISH_DIR}"
dotnet publish "${REPO_ROOT}/Chummer.Avalonia/Chummer.Avalonia.csproj" \
    -c Release -r "${RID}" --self-contained true \
    -p:PublishSingleFile=false \
    -o "${PUBLISH_DIR}"

echo "==> Assembling AppDir..."
rm -rf "${APPDIR}"
mkdir -p "${APPDIR}/usr/bin" "${APPDIR}/usr/share/applications" "${APPDIR}/usr/share/icons/hicolor/256x256/apps"

cp -r "${PUBLISH_DIR}/." "${APPDIR}/usr/bin/"
chmod +x "${APPDIR}/usr/bin/Chummer.Avalonia"

install -m 755 "${SCRIPT_DIR}/AppRun" "${APPDIR}/AppRun"
install -m 644 "${SCRIPT_DIR}/chummer.desktop" "${APPDIR}/chummer.desktop"
install -m 644 "${SCRIPT_DIR}/chummer.desktop" "${APPDIR}/usr/share/applications/chummer.desktop"
install -m 644 "${SCRIPT_DIR}/chummer.png" "${APPDIR}/chummer.png"
install -m 644 "${SCRIPT_DIR}/chummer.png" "${APPDIR}/usr/share/icons/hicolor/256x256/apps/chummer.png"

APPIMAGETOOL="$(command -v appimagetool || true)"
if [ -z "${APPIMAGETOOL}" ]; then
    CACHED="${SCRIPT_DIR}/appimagetool-x86_64.AppImage"
    if [ ! -x "${CACHED}" ]; then
        echo "==> Downloading appimagetool (not found on PATH)..."
        curl -fL -o "${CACHED}" \
            "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
        chmod +x "${CACHED}"
    fi
    APPIMAGETOOL="${CACHED}"
fi

echo "==> Building the .AppImage..."
mkdir -p "${OUT_DIR}"
ARCH=x86_64 "${APPIMAGETOOL}" "${APPDIR}" "${OUT_DIR}/Chummer-${RID}.AppImage"

echo "==> Done: ${OUT_DIR}/Chummer-${RID}.AppImage"
