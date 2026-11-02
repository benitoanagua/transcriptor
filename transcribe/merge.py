#!/usr/bin/env python3
"""Une yo.srt y cliente.srt en transcripcion.txt y transcripcion.srt, ordenados por tiempo."""
import re
import sys
from pathlib import Path

TS = re.compile(r"(\d+):(\d+):(\d+)[,.](\d+)\s*-->\s*(\d+):(\d+):(\d+)[,.](\d+)")


def to_s(h, m, s, ms):
    return int(h) * 3600 + int(m) * 60 + int(s) + int(ms) / 1000


def parse(path, label):
    out = []
    p = Path(path)
    if not p.exists():
        return out
    for block in re.split(r"\n\s*\n", p.read_text(encoding="utf-8").strip()):
        lines = [l for l in block.splitlines() if l.strip()]
        for i, line in enumerate(lines):
            m = TS.search(line)
            if m:
                g = m.groups()
                text = " ".join(lines[i + 1:]).strip()
                if text:
                    out.append((to_s(*g[:4]), to_s(*g[4:]), label, text))
                break
    return out


def stamp(t, comma=False):
    h, rem = divmod(int(t), 3600)
    m, s = divmod(rem, 60)
    base = f"{h:02d}:{m:02d}:{s:02d}"
    return f"{base},{int(round((t - int(t)) * 1000)):03d}" if comma else base


def main(folder):
    d = Path(folder)
    segs = sorted(parse(d / "yo.srt", "Yo") + parse(d / "cliente.srt", "Cliente"))
    (d / "transcripcion.txt").write_text(
        "\n".join(f"[{stamp(a)}] {who}: {text}" for a, _, who, text in segs) + "\n", encoding="utf-8")
    (d / "transcripcion.srt").write_text(
        "\n".join(f"{i}\n{stamp(a, True)} --> {stamp(b, True)}\n{who}: {text}\n"
                  for i, (a, b, who, text) in enumerate(segs, 1)), encoding="utf-8")


if __name__ == "__main__":
    main(sys.argv[1])
