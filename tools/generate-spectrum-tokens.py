#!/usr/bin/env python3
"""Generate Avalonia resources from the pinned official Spectrum token package."""

from __future__ import annotations

import argparse
import base64
import hashlib
import json
import re
import tarfile
import urllib.request
from pathlib import Path


VERSION = "15.6.0"
PACKAGE_NAME = "@adobe/spectrum-tokens"
TARBALL_URL = (
    "https://registry.npmjs.org/@adobe/spectrum-tokens/-/"
    f"spectrum-tokens-{VERSION}.tgz"
)
TARBALL_SHA512 = (
    "eMciiuA/2yt0dKLko2LI1QLktb9RohlckXR4brCTIxX/"
    "l5T2jfxP5DjuFEAqJUTxRnEVVrbkMtVWnyZdl7J5Ow=="
)
OUTPUT = Path("ui/Theming/Tokens.axaml")

COLOR_TOKENS = (
    "gray-25", "gray-50", "gray-75", "gray-100", "gray-200", "gray-300",
    "gray-400", "gray-500", "gray-600", "gray-700", "gray-800", "gray-900",
    "gray-1000", "blue-700", "blue-800", "blue-900", "blue-1000",
    "background-base-color", "background-elevated-color",
    "background-layer-1-color", "background-layer-2-color",
    "background-pasteboard-color", "accent-background-color-default",
    "accent-background-color-hover", "accent-background-color-down",
    "accent-content-color-default", "accent-subtle-background-color-default",
    "neutral-subtle-background-color-default",
    "focus-indicator-color", "negative-background-color-default",
    "negative-background-color-hover", "negative-background-color-down",
    "negative-subtle-background-color-default", "negative-content-color-default",
    "positive-background-color-default", "positive-subtle-background-color-default",
    "positive-color-900", "notice-background-color-default",
    "notice-subtle-background-color-default", "notice-color-900",
    "notice-color-1000", "transparent-white-25", "transparent-white-100",
)

DOUBLE_TOKENS = (
    "spacing-25", "spacing-50", "spacing-75", "spacing-85", "spacing-100",
    "spacing-200", "spacing-300", "spacing-350", "spacing-400", "spacing-500",
    "spacing-600", "spacing-700", "spacing-800", "spacing-900",
    "component-height-50", "component-height-75", "component-height-100",
    "component-height-200", "component-height-300", "component-height-400",
    "font-size-25", "font-size-50", "font-size-75", "font-size-100",
    "font-size-200", "font-size-300", "font-size-400", "font-size-500",
    "font-size-600", "font-size-700", "font-size-800", "font-size-900",
    "line-height-font-size-50", "line-height-font-size-75",
    "line-height-font-size-100", "line-height-font-size-200",
    "line-height-font-size-300", "line-height-font-size-400",
    "line-height-font-size-500", "field-width-small", "field-width-medium",
    "focus-indicator-gap", "focus-indicator-thickness",
    "status-light-dot-size-small", "status-light-dot-size-medium",
    "status-light-text-to-visual-100",
    "text-to-control-75", "text-to-control-100",
    "checkbox-control-size-small", "checkbox-control-size-medium",
    "radio-button-control-size-small", "radio-button-control-size-medium",
    "radio-button-selection-indicator",
    "switch-control-height-small", "switch-control-height-medium",
    "switch-control-width-small", "switch-control-width-medium",
    "switch-handle-size-small", "switch-handle-size-medium",
    "switch-handle-selected-size-small", "switch-handle-selected-size-medium",
    "slider-handle-size-small", "slider-handle-size-medium",
    "slider-track-thickness",
    "progress-circle-size-small", "progress-circle-size-medium",
    "progress-circle-thickness-small", "progress-circle-thickness-medium",
    "divider-thickness-small", "divider-thickness-medium",
    "toast-height", "toast-maximum-width",
    "tooltip-maximum-width",
    "alert-dialog-minimum-width", "alert-dialog-maximum-width",
    "overlay-opacity",
    "opacity-disabled", "corner-radius-0-value", "list-view-max-height",
    "settings-form-max-width",
)

CORNER_RADIUS_TOKENS = (
    "corner-radius-0", "corner-radius-75", "corner-radius-100",
    "corner-radius-200", "corner-radius-300", "corner-radius-400",
    "corner-radius-500", "corner-radius-600", "corner-radius-700",
    "corner-radius-800",
)

THICKNESS_TOKENS = {
    "spacing-0-thickness": ("corner-radius-0",) * 4,
    "spacing-25-thickness": ("spacing-25",) * 4,
    "spacing-50-thickness": ("spacing-50",) * 4,
    "spacing-75-thickness": ("spacing-75",) * 4,
    "spacing-85-thickness": ("spacing-85",) * 4,
    "spacing-100-thickness": ("spacing-100",) * 4,
    "spacing-200-thickness": ("spacing-200",) * 4,
    "spacing-300-thickness": ("spacing-300",) * 4,
    "spacing-400-thickness": ("spacing-400",) * 4,
    "spacing-100x75-thickness": ("spacing-100", "spacing-75"),
    "spacing-200x75-thickness": ("spacing-200", "spacing-75"),
    "spacing-200x85-thickness": ("spacing-200", "spacing-85"),
    "spacing-300x75-thickness": ("spacing-300", "spacing-75"),
    "spacing-100x50-thickness": ("spacing-100", "spacing-50"),
    "spacing-200x50-thickness": ("spacing-200", "spacing-50"),
    "spacing-85x50-thickness": ("spacing-85", "spacing-50"),
    "spacing-200x0-thickness": ("spacing-200", "corner-radius-0"),
    "spacing-100x0-thickness": ("spacing-100", "corner-radius-0"),
    "spacing-0x100-thickness": ("corner-radius-0", "spacing-100"),
    "spacing-0x200-thickness": ("corner-radius-0", "spacing-200"),
    "padding-top-spacing-100-thickness": (
        "corner-radius-0", "spacing-100", "corner-radius-0", "corner-radius-0"
    ),
    "padding-bottom-spacing-100-thickness": (
        "corner-radius-0", "corner-radius-0", "corner-radius-0", "spacing-100"
    ),
    "border-bottom-1-thickness": (
        "corner-radius-0", "corner-radius-0", "corner-radius-0", "spacing-25"
    ),
    "border-bottom-focus-thickness": (
        "corner-radius-0", "corner-radius-0", "corner-radius-0", "focus-indicator-thickness"
    ),
    "border-left-1-thickness": (
        "spacing-25", "corner-radius-0", "corner-radius-0", "corner-radius-0"
    ),
    "radio-button-selection-indicator-thickness": ("radio-button-selection-indicator",) * 4,
    "progress-circle-thickness-small-thickness": ("progress-circle-thickness-small",) * 4,
    "progress-circle-thickness-medium-thickness": ("progress-circle-thickness-medium",) * 4,
    "margin-bottom-spacing-100-thickness": (
        "corner-radius-0", "corner-radius-0", "corner-radius-0", "spacing-100"
    ),
}

FONT_WEIGHT_TOKENS = {
    "regular-font-weight": "Regular",
    "medium-font-weight": "Medium",
    "bold-font-weight": "Bold",
    "extra-bold-font-weight": "ExtraBold",
}


def download_package() -> bytes:
    request = urllib.request.Request(
        TARBALL_URL, headers={"User-Agent": "Transcriptor-Spectrum-Token-Generator"}
    )
    with urllib.request.urlopen(request, timeout=30) as response:
        archive = response.read()
    actual = base64.b64encode(hashlib.sha512(archive).digest()).decode("ascii")
    if actual != TARBALL_SHA512:
        raise ValueError(f"El SHA-512 del paquete no coincide: {actual}")
    return archive


def load_variables(package_dir: Path | None) -> dict[str, object]:
    if package_dir is not None:
        metadata = json.loads((package_dir / "package.json").read_text())
        if metadata.get("name") != PACKAGE_NAME or metadata.get("version") != VERSION:
            raise ValueError(f"Se requiere {PACKAGE_NAME}@{VERSION}.")
        return json.loads((package_dir / "dist/json/variables.json").read_text())

    archive = download_package()
    with tarfile.open(fileobj=__import__("io").BytesIO(archive), mode="r:gz") as tar:
        member = tar.getmember("package/dist/json/variables.json")
        stream = tar.extractfile(member)
        if stream is None:
            raise ValueError("El paquete no contiene dist/json/variables.json.")
        return json.load(stream)


def resolve_value(token: object, variant: str) -> object:
    if not isinstance(token, dict):
        return token
    sets = token.get("sets")
    if isinstance(sets, dict):
        selected = sets.get("dark") or sets.get("desktop")
        if selected is None:
            selected = sets.get("default")
        if selected is not None:
            return resolve_value(selected, variant)
    if "value" in token:
        return token["value"]
    raise ValueError(f"No se pudo resolver el token para {variant}: {token!r}")


def token_value(variables: dict[str, object], name: str, variant: str) -> object:
    try:
        return resolve_value(variables[name], variant)
    except KeyError as error:
        raise ValueError(f"El token oficial no existe: {name}") from error


def dimension(value: object) -> str:
    text = str(value).strip()
    if text.endswith("px"):
        text = text[:-2]
    return text


def color(value: object) -> str:
    text = str(value).strip()
    match = re.fullmatch(r"rgba?\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)(?:\s*,\s*([\d.]+))?\s*\)", text)
    if match is None:
        if text.startswith("#"):
            return text.upper()
        raise ValueError(f"Formato de color no soportado: {text}")
    red, green, blue = (int(match.group(index)) for index in (1, 2, 3))
    alpha = float(match.group(4)) if match.group(4) else 1
    if alpha < 1:
        return f"#{round(alpha * 255):02X}{red:02X}{green:02X}{blue:02X}"
    return f"#{red:02X}{green:02X}{blue:02X}"


def xaml(variables: dict[str, object]) -> str:
    lines = [
        '<ResourceDictionary xmlns="https://github.com/avaloniaui"',
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">',
        f"  <!-- Generado desde {PACKAGE_NAME}@{VERSION}; no editar manualmente. -->",
    ]
    for name in COLOR_TOKENS:
        value = color(token_value(variables, name, "dark"))
        lines.append(f'  <SolidColorBrush x:Key="{name}">{value}</SolidColorBrush>')
    for name in DOUBLE_TOKENS:
        if name == "list-view-max-height":
            value = str(
                sum(
                    float(dimension(token_value(variables, "component-height-200", "desktop")))
                    for _ in range(5)
                )
                + float(dimension(token_value(variables, "spacing-350", "desktop")))
            )
        elif name == "settings-form-max-width":
            value = str(
                3 * float(dimension(token_value(variables, "field-width-medium", "desktop")))
            )
        else:
            source_name = "corner-radius-0" if name == "corner-radius-0-value" else name
            value = dimension(token_value(variables, source_name, "desktop"))
        lines.append(f'  <x:Double x:Key="{name}">{value}</x:Double>')
    for name in CORNER_RADIUS_TOKENS:
        value = dimension(token_value(variables, name, "dark"))
        lines.append(f'  <CornerRadius x:Key="{name}">{value}</CornerRadius>')
    for name, source_names in THICKNESS_TOKENS.items():
        values = [dimension(token_value(variables, source, "desktop")) for source in source_names]
        if len(values) == 1:
            values *= 4
        elif len(values) == 2:
            values = [values[0], values[1], values[0], values[1]]
        lines.append(f'  <Thickness x:Key="{name}">{",".join(values)}</Thickness>')
    for name, avalonia_weight in FONT_WEIGHT_TOKENS.items():
        source_weight = str(token_value(variables, name, "dark"))
        if source_weight.lower().replace("-", "") != avalonia_weight.lower():
            raise ValueError(f"El token oficial {name} cambió: {source_weight}")
        lines.append(f'  <FontWeight x:Key="{name}">{avalonia_weight}</FontWeight>')

    code_font = str(token_value(variables, "code-font-family", "dark"))
    if code_font != "Source Code Pro":
        raise ValueError(f"El token oficial code-font-family cambió: {code_font}")
    lines.extend(
        (
            '  <FontFamily x:Key="source-sans-3-font-family">'
            "avares://Transcriptor.UI/Assets/Fonts/SourceSans3.ttf#Source Sans 3, Segoe UI"
            "</FontFamily>",
            '  <FontFamily x:Key="code-font-family">'
            "avares://Transcriptor.UI/Assets/Fonts/SourceCodePro.ttf#Source Code Pro, Consolas"
            "</FontFamily>",
            "</ResourceDictionary>",
            "",
        )
    )
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--package-dir",
        type=Path,
        help="Directorio extraído del paquete oficial; si se omite, descarga y valida su SHA-512.",
    )
    parser.add_argument("--output", type=Path, default=OUTPUT)
    arguments = parser.parse_args()
    arguments.output.write_text(xaml(load_variables(arguments.package_dir)), encoding="utf-8")


if __name__ == "__main__":
    main()
