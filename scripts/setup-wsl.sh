#!/usr/bin/env bash
# Prepara WSL (Arch): SDK de .NET, ffmpeg, python, whisper.cpp y el modelo small.
set -euo pipefail

sudo pacman -S --needed --noconfirm dotnet-sdk ffmpeg python git cmake base-devel

if [ ! -d "$HOME/whisper.cpp" ]; then
  git clone https://github.com/ggml-org/whisper.cpp "$HOME/whisper.cpp"
fi
cd "$HOME/whisper.cpp"
cmake -B build
cmake --build build -j --config Release

mkdir -p "$HOME/.local/bin" "$HOME/models"
ln -sf "$HOME/whisper.cpp/build/bin/whisper-cli" "$HOME/.local/bin/whisper-cli"
bash ./models/download-ggml-model.sh small
mv -f ./models/ggml-small.bin "$HOME/models/ggml-small.bin"

echo "Listo. Asegúrate de tener ~/.local/bin en el PATH."
