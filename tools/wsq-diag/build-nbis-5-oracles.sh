#!/usr/bin/env zsh

set -euo pipefail

repo_root="/Users/pmtar/Development/Projects/OpenNist"
nbis_root="/tmp/nbis_v5_0_0/Rel_5.0.0"
include_dir="$nbis_root/exports/include"
lib_dir="$nbis_root/exports/lib"
tool_dir="$repo_root/tmp/wsq-diag"

gcc -O2 \
  -I "$include_dir" \
  "$tool_dir/nbis_dump.c" \
  "$lib_dir/libwsq.a" \
  "$lib_dir/libjpegl.a" \
  "$lib_dir/libihead.a" \
  "$lib_dir/libfet.a" \
  "$lib_dir/libioutil.a" \
  "$lib_dir/libutil.a" \
  -lm \
  -o "$tool_dir/nbis_dump"

gcc -O2 \
  -I "$include_dir" \
  "$tool_dir/nbis_wavelet_dump.c" \
  "$lib_dir/libwsq.a" \
  "$lib_dir/libjpegl.a" \
  "$lib_dir/libihead.a" \
  "$lib_dir/libfet.a" \
  "$lib_dir/libioutil.a" \
  "$lib_dir/libutil.a" \
  -lm \
  -o "$tool_dir/nbis_wavelet_dump"

gcc -O2 \
  -I "$include_dir" \
  "$tool_dir/nbis_scale_dump.c" \
  -lm \
  -o "$tool_dir/nbis_scale_dump"

echo "Rebuilt NBIS 5.0.0 WSQ oracle helpers in $tool_dir"
