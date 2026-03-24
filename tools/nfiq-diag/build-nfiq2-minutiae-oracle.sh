#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

NFIQ_ROOT="${OPENNIST_NFIQ2_ROOT:-${NFIQ2_ROOT:-/Users/pmtar/usr/local/nfiq2}}"
SOURCE_FILE="$SCRIPT_DIR/nfiq2_minutiae_oracle.cpp"
OUTPUT_FILE="$SCRIPT_DIR/nfiq2_minutiae_oracle"

c++ -std=c++17 \
  "$SOURCE_FILE" \
  -I"$SCRIPT_DIR" \
  -I"$NFIQ_ROOT/include" \
  -I"$NFIQ_ROOT/include/opencv4" \
  -I"/tmp/opennist_nfiq2/NFIQ2/NFIQ2Algorithm/include" \
  -L"$NFIQ_ROOT/lib" \
  -Wl,-rpath,"$NFIQ_ROOT/lib" \
  -lnfiq2 \
  -lFRFXLL \
  -lopencv_core \
  -lopencv_imgproc \
  -lopencv_imgcodecs \
  -lopencv_ml \
  -lnfir \
  -framework Accelerate \
  -framework OpenCL \
  -lz \
  -o "$OUTPUT_FILE"

printf 'Built %s\n' "$OUTPUT_FILE"
