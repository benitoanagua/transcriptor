#!/usr/bin/env python3
"""Generate Avalonia StreamGeometry resources from official Spectrum 2 icon packages.

Reads the pinned official packages, validates their SHA-512, and emits
ui/Theming/Icons.axaml with a whitelist of icons used by Transcriptor.
Each icon is emitted as a StreamGeometry with key ``icon-<nombre-en-kebab>``.

The script fails on:
  * any SVG whose root is not <svg> or that contains elements other than <path>,
  * any <path> carrying a transform attribute,
  * any whitelist name that does not exist in the package.

Fill-rule: SVG default is nonzero; Avalonia StreamGeometry default is evenodd.
If the SVG does not declare fill-rule, the geometry is prefixed with ``F1``
(nonzero) to preserve the intended rendering.
"""

from __future__ import annotations

import base64
import hashlib
import io
import json
import re
import tarfile
import urllib.request
from pathlib import Path
from xml.etree import ElementTree as ET

# --------------------------------------------------------------------------- pins

WORKFLOW_VERSION = "5.0.0"
WORKFLOW_NAME = "@adobe/spectrum-css-workflow-icons"
WORKFLOW_URL = (
    "https://registry.npmjs.org/@adobe/spectrum-css-workflow-icons/-/"
    f"spectrum-css-workflow-icons-{WORKFLOW_VERSION}.tgz"
)
WORKFLOW_SHA512 = (
    "C6N97hnwG/ZqAPsMsrxakU8F/PH4w5EUdadQermIbmCHll1ZJYdG8BL1UGu5jMTunsZkZrDgLIf1XnAnxJQhgA=="
)

UI_ICONS_VERSION = "1.1.2"
UI_ICONS_NAME = "@spectrum-css/ui-icons"
UI_ICONS_URL = (
    "https://registry.npmjs.org/@spectrum-css/ui-icons/-/ui-icons-1.1.2.tgz"
)
# SHA-512 obtained from npm view @spectrum-css/ui-icons@1.1.2 dist.integrity
UI_ICONS_SHA512 = (
    "rD0EOdabyUJ6pjfvh9eiGlvhk2huhaah+AG8Utzqh/+YgMCn+NQEUz2zxRo9Jva8GXxORLGLDtISafHguOtiOA=="
)

OUTPUT = Path("ui/Theming/Icons.axaml")

# Whitelist: package -> list of (svg basename, xaml key, display size)
WHITELIST: dict[str, list[tuple[str, str, str]]] = {
    WORKFLOW_NAME: [
        # Transport
        ("S2_Icon_Play_20_N.svg", "icon-Play", "workflow-icon-size-100"),
        ("S2_Icon_Pause_20_N.svg", "icon-Pause", "workflow-icon-size-100"),
        # Audio
        ("S2_Icon_Microphone_20_N.svg", "icon-Microphone", "workflow-icon-size-100"),
        ("S2_Icon_VolumeTwo_20_N.svg", "icon-VolumeTwo", "workflow-icon-size-100"),
        # Window chrome
        ("S2_Icon_Close_20_N.svg", "icon-Close", "workflow-icon-size-100"),
        ("S2_Icon_Minimize_20_N.svg", "icon-Minimize", "workflow-icon-size-100"),
        # Actions
        ("S2_Icon_Delete_20_N.svg", "icon-Delete", "workflow-icon-size-75"),
        ("S2_Icon_Copy_20_N.svg", "icon-Copy", "workflow-icon-size-75"),
        ("S2_Icon_Export_20_N.svg", "icon-Export", "workflow-icon-size-75"),
        ("S2_Icon_Refresh_20_N.svg", "icon-Refresh", "workflow-icon-size-75"),
        # Navigation / filing
        ("S2_Icon_FolderOpen_20_N.svg", "icon-FolderOpen", "workflow-icon-size-100"),
        ("S2_Icon_Settings_20_N.svg", "icon-Settings", "workflow-icon-size-100"),
        # Security
        ("S2_Icon_Lock_20_N.svg", "icon-Lock", "workflow-icon-size-100"),
        ("S2_Icon_LockOpen_20_N.svg", "icon-LockOpen", "workflow-icon-size-100"),
        # Library
        ("S2_Icon_Filter_20_N.svg", "icon-Filter", "workflow-icon-size-75"),
        ("S2_Icon_Sort_20_N.svg", "icon-Sort", "workflow-icon-size-75"),
        # Transcription
        ("S2_Icon_Transcript_20_N.svg", "icon-Transcript", "workflow-icon-size-100"),
        ("S2_Icon_FileText_20_N.svg", "icon-FileText", "workflow-icon-size-75"),
        # Chevrons (replace Unicode ↑ ↓)
        ("S2_Icon_ChevronUp_20_N.svg", "icon-ChevronUp", "workflow-icon-size-75"),
        ("S2_Icon_ChevronDown_20_N.svg", "icon-ChevronDown", "workflow-icon-size-75"),
        # Alert icons
        ("S2_Icon_InfoCircle_20_N.svg", "icon-InfoCircle", "workflow-icon-size-100"),
        ("S2_Icon_CheckmarkCircle_20_N.svg", "icon-CheckmarkCircle", "workflow-icon-size-100"),
        ("S2_Icon_AlertTriangle_20_N.svg", "icon-AlertTriangle", "workflow-icon-size-100"),
        # Window chrome placeholder
        ("S2_Icon_AudioWave_20_N.svg", "icon-AudioWave", "workflow-icon-size-200"),
        ("S2_Icon_Search_20_N.svg", "icon-Search", "workflow-icon-size-75"),
    ],
    UI_ICONS_NAME: [
        # Component icons
        ("dist/combined/Checkmark100.svg", "icon-Checkmark", "workflow-icon-size-100"),
        ("dist/combined/Chevron100.svg", "icon-Chevron", "workflow-icon-size-100"),
    ],
}


def download_package(url: str, expected_sha512: str) -> bytes:
    req = urllib.request.Request(url, headers={"User-Agent": "Transcriptor-Spectrum-Icon-Generator"})
    with urllib.request.urlopen(req, timeout=60) as response:
        archive = response.read()
    actual = base64.b64encode(hashlib.sha512(archive).digest()).decode("ascii")
    if actual != expected_sha512:
        raise ValueError(f"SHA-512 mismatch for {url}: {actual} != {expected_sha512}")
    return archive


def extract_svg(archive: bytes, member_name: str) -> str:
    """Find a member whose path ends with member_name and return its SVG XML."""
    with tarfile.open(fileobj=io.BytesIO(archive), mode="r:gz") as tar:
        # The npm tarball prefixes every path with "package/"; match by suffix.
        candidates = [m for m in tar.getmembers() if m.name.endswith(member_name)]
        if not candidates:
            raise ValueError(f"Icon not found in package: {member_name}")
        # Prefer the distribution copy under dist/ over icons/ assets.
        candidates.sort(key=lambda m: (0 if "/dist/" in m.name else 1, m.name))
        member = candidates[0]
        stream = tar.extractfile(member)
        if stream is None:
            raise ValueError(f"Cannot read {member.name} from package")
        return stream.read().decode("utf-8")


def svg_path_data(svg_xml: str) -> str:
    """Parse an SVG and return the <path d> with fill-rule handling.

    Raises ValueError if the SVG contains elements other than <path>, or if a
    <path> carries a transform attribute.
    """
    root = ET.fromstring(svg_xml)
    # Normalise namespace
    tag = root.tag.split("}")[-1] if "}" in root.tag else root.tag
    if tag != "svg":
        raise ValueError(f"SVG root is <{tag}>, expected <svg>")

    fill_rule = root.get("fill-rule")  # may be None

    path_parts: list[str] = []
    for elem in root.iter():
        # Skip the root <svg> element itself.
        if elem is root:
            continue
        etag = elem.tag.split("}")[-1] if "}" in elem.tag else elem.tag
        if etag == "path":
            transform = elem.get("transform")
            if transform:
                raise ValueError("SVG <path> carries a transform attribute")
            d = elem.get("d")
            if d is None:
                raise ValueError("SVG <path> has no d attribute")
            path_parts.append(d.strip())
        elif etag not in ("defs", "title", "desc", "metadata"):
            # Allow structural/metadata elements, reject anything graphical
            raise ValueError(f"SVG contains unsupported element <{etag}>")

    if not path_parts:
        raise ValueError("SVG contains no <path d> element")

    path_d = " ".join(path_parts)

    # If no fill-rule declared, SVG default is nonzero; Avalonia defaults to
    # evenodd, so prefix with F1 to preserve nonzero.
    if fill_rule is None or fill_rule == "nonzero":
        return "F1 " + path_d.strip()
    if fill_rule == "evenodd":
        return "F0 " + path_d.strip()
    raise ValueError(f"Unsupported fill-rule: {fill_rule}")


def kebab_name(display_name: str) -> str:
    return re.sub(r"[^A-Za-z0-9]+", "-", display_name).strip("-").lower()


def xaml(icons: dict[str, tuple[str, str]]) -> str:
    """icons: {xaml_key: (geometry_data, size_token)}"""
    lines = [
        '<ResourceDictionary xmlns="https://github.com/avaloniaui"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
        f"  <!-- Generado desde {WORKFLOW_NAME}@{WORKFLOW_VERSION} y {UI_ICONS_NAME}@{UI_ICONS_VERSION}; no editar manualmente. -->",
        "",
    ]
    for key in sorted(icons):
        geometry, _size = icons[key]
        lines.append(f'  <StreamGeometry x:Key="{key}">{geometry}</StreamGeometry>')
    lines.append("")
    lines.append("</ResourceDictionary>")
    lines.append("")
    return "\n".join(lines)


def main() -> None:
    import argparse

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--workflow-dir",
        type=Path,
        help="Directorio extraído de spectrum-css-workflow-icons.",
    )
    parser.add_argument(
        "--ui-icons-dir",
        type=Path,
        help="Directorio extraído de spectrum-css/ui-icons.",
    )
    parser.add_argument("--output", type=Path, default=OUTPUT)
    args = parser.parse_args()

    workflow_archive = None
    ui_icons_archive = None

    if args.workflow_dir:
        # Load from extracted directory
        svgs_dir = args.workflow_dir / "package" / "dist" / "assets" / "svg"
        if not svgs_dir.is_dir():
            svgs_dir = args.workflow_dir / "dist" / "assets" / "svg"
        # We still need the archive for SHA validation; load from default
        workflow_archive = download_package(WORKFLOW_URL, WORKFLOW_SHA512)
        workflow_root = args.workflow_dir
    else:
        workflow_archive = download_package(WORKFLOW_URL, WORKFLOW_SHA512)
        workflow_root = None

    if args.ui_icons_dir:
        ui_icons_archive = download_package(UI_ICONS_URL, UI_ICONS_SHA512)
        ui_icons_root = args.ui_icons_dir
    else:
        ui_icons_archive = download_package(UI_ICONS_URL, UI_ICONS_SHA512)
        ui_icons_root = None

    icons: dict[str, tuple[str, str]] = {}

    for package_name, entries in WHITELIST.items():
        archive = workflow_archive if package_name == WORKFLOW_NAME else ui_icons_archive
        for member_name, xaml_key, size_token in entries:
            svg_xml = extract_svg(archive, member_name)
            geometry = svg_path_data(svg_xml)
            icons[xaml_key] = (geometry, size_token)

    args.output.write_text(xaml(icons), encoding="utf-8")
    print(f"Generated {len(icons)} icons in {args.output}")


if __name__ == "__main__":
    main()
