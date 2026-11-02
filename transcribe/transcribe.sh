#!/usr/bin/env bash
# Transcribe las llamadas que aún no tienen transcripcion.txt.
# Requiere: ffmpeg, python3 y whisper.cpp (whisper-cli) con un modelo ggml.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [ -z "${TRANSCRIPTOR_DIR:-}" ]; then
  win_home="$(cmd.exe /c 'echo %USERPROFILE%' 2>/dev/null | tr -d '\r')"
  TRANSCRIPTOR_DIR="$(wslpath "$win_home")/Transcriptor/Grabaciones"
fi
MODEL="${WHISPER_MODEL:-$HOME/models/ggml-small.bin}"
LANG_CODE="${WHISPER_LANG:-es}"
WHISPER_BIN="${WHISPER_BIN:-whisper-cli}"

[ -d "$TRANSCRIPTOR_DIR" ] || { echo "No existe la carpeta: $TRANSCRIPTOR_DIR" >&2; exit 1; }
[ -f "$MODEL" ] || { echo "No se encontró el modelo: $MODEL (ejecuta scripts/setup-wsl.sh)" >&2; exit 1; }
command -v "$WHISPER_BIN" >/dev/null || { echo "No se encontró $WHISPER_BIN en el PATH" >&2; exit 1; }

tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
count=0

while IFS= read -r -d '' call_dir; do
  [ -f "$call_dir/transcripcion.txt" ] && continue
  [ -f "$call_dir/yo.wav" ] || [ -f "$call_dir/cliente.wav" ] || continue
  echo "→ $call_dir"
  for track in yo cliente; do
    src="$call_dir/$track.wav"
    [ -f "$src" ] || continue
    ffmpeg -loglevel error -y -i "$src" -ar 16000 -ac 1 -c:a pcm_s16le "$tmp/$track.wav"
    "$WHISPER_BIN" -m "$MODEL" -l "$LANG_CODE" -f "$tmp/$track.wav" -osrt -of "$call_dir/$track" >/dev/null 2>&1
  done
  python3 "$HERE/merge.py" "$call_dir"
  count=$((count + 1))
done < <(find "$TRANSCRIPTOR_DIR" -mindepth 2 -maxdepth 2 -type d -print0 | sort -z)

echo "Listo: $count llamada(s) transcrita(s)."
