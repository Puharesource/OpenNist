#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
FINGERJET_ROOT="/tmp/opennist_fingerjetfxose_2/FingerJetFXOSE/libFRFXLL"
SOURCE_FILE="$SCRIPT_DIR/nfiq2_fingerjet_oracle.cpp"
OUTPUT_FILE="$SCRIPT_DIR/nfiq2_fingerjet_oracle"

c++ -std=c++17 \
  "$SOURCE_FILE" \
  -I"$FINGERJET_ROOT/src/algorithm" \
  -I"$FINGERJET_ROOT/src/lib" \
  -I"$FINGERJET_ROOT/src/FRFXLL" \
  -I"/Users/pmtar/usr/local/nfiq2/include/opencv4" \
  -L"/Users/pmtar/usr/local/nfiq2/lib" \
  -Wl,-rpath,"/Users/pmtar/usr/local/nfiq2/lib" \
  -lopencv_core \
  -lopencv_imgproc \
  -lopencv_imgcodecs \
  -framework Accelerate \
  -framework OpenCL \
  -o "$OUTPUT_FILE"

printf 'Built %s\n' "$OUTPUT_FILE"
