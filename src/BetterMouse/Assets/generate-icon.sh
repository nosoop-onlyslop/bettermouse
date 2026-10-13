#!/usr/bin/env bash
# Regenerates bettermouse.ico from bettermouse.svg.
#
#   ./generate-icon.sh
#
# Requires ImageMagick 7 (`magick`) with an SVG delegate (RSVG). The generated
# .ico is tracked, so a normal build/test/CI run does not need ImageMagick.
set -euo pipefail
cd "$(dirname "$0")"

magick -background none -density 256x256 bettermouse.svg \
  -filter Lanczos \
  -define icon:auto-resize="256,128,64,48,40,32,24,20,16" \
  bettermouse.ico

echo "Wrote bettermouse.ico (16,20,24,32,40,48,64,128,256)"
