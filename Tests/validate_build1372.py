from pathlib import Path
import json

from PIL import Image


ROOT = Path(__file__).resolve().parents[1]

WIDGETS = {
    "EmberGlowCalculator": {
        "id": "utility.ember-glow-calculator",
        "name": "Ember Glow Calculator",
        "assembly": "EmilyDesk.Widgets.EmberGlowCalculator.dll",
        "skin": "ember-glow-calculator-skin.png",
        "size": (300, 420),
        "package": "EmberGlowCalculator-1.0.0.emilywidget",
    },
    "EmberGlowCurrencyConverter": {
        "id": "utility.ember-glow-currency-converter",
        "name": "Ember Glow Currency Converter",
        "assembly": "EmilyDesk.Widgets.EmberGlowCurrencyConverter.dll",
        "skin": "ember-glow-currency-converter-skin.png",
        "size": (550, 363),
        "package": "EmberGlowCurrencyConverter-1.0.0.emilywidget",
    },
    "EmberGlowSystemInfo": {
        "id": "utility.ember-glow-system-info",
        "name": "Ember Glow System Information",
        "assembly": "EmilyDesk.Widgets.EmberGlowSystemInfo.dll",
        "skin": "ember-glow-system-info-skin.png",
        "size": (550, 327),
        "package": "EmberGlowSystemInformation-1.0.0.emilywidget",
    },
}


def require(condition, message):
    if not condition:
        raise AssertionError(message)


app_constants = (ROOT / "src/XWidgetReborn.Shared/AppConstants.cs").read_text()
require('BuildId = "1372"' in app_constants, "Build ID is not 1372")

build = (ROOT / "Build.ps1").read_text()
catalog = (ROOT / "src/XWidgetReborn.WidgetSdk/OptionalWidgetThemeCatalog.cs").read_text()
require('"Ember Glow"' in catalog, "Ember Glow is missing from theme switching")
require('prefix = "ember-glow-"' in catalog, "Ember Glow target IDs are missing")

for folder, expected in WIDGETS.items():
    widget_root = ROOT / "OptionalWidgets" / folder
    manifest = json.loads((widget_root / "manifest.json").read_text())
    require(manifest["id"] == expected["id"], f"Wrong ID for {folder}")
    require(manifest["name"] == expected["name"], f"Wrong name for {folder}")
    require(manifest["version"] == "1.0.0", f"Wrong version for {folder}")
    require(manifest["assembly"] == expected["assembly"], f"Wrong assembly for {folder}")
    require("designer" in manifest["capabilities"], f"Designer missing for {folder}")
    require("theme-switching" in manifest["capabilities"], f"Theme switching missing for {folder}")

    for image_name in (expected["skin"], "preview.png"):
        with Image.open(widget_root / image_name) as image:
            require(image.size == expected["size"], f"Wrong {image_name} size for {folder}")
            require(image.mode == "RGBA", f"{image_name} must retain transparency for {folder}")
    with Image.open(widget_root / "icon.png") as icon:
        require(icon.size == (256, 256), f"Wrong icon size for {folder}")
        require(icon.mode == "RGBA", f"Icon must retain transparency for {folder}")

    project = ROOT / "src" / expected["assembly"].removesuffix(".dll")
    require(project.is_dir(), f"Project missing for {folder}")
    assembly_info = (project / "AssemblyInfo.cs").read_text()
    require('AssemblyVersion("1.0.0.0")' in assembly_info, f"Assembly version wrong for {folder}")
    require(expected["id"] in build, f"Build approval missing for {folder}")
    require(expected["package"] in build, f"Package verification missing for {folder}")

for source in (
    ROOT / "src/EmilyDesk.Widgets.Calculator/CalculatorWidget.cs",
    ROOT / "src/EmilyDesk.Widgets.CurrencyConverter/CurrencyConverterWidget.cs",
    ROOT / "src/EmilyDesk.Widgets.SystemInfo/SystemInfoWidget.cs",
):
    text = source.read_text()
    require("EMBER_GLOW_WIDGET" in text, f"Ember Glow compile branch missing in {source.name}")
    require("AntiAliasGridFit" in text, f"Anti-aliased text missing in {source.name}")
    require("IWidgetDesignerBackgroundLayerProvider" in text, f"Editable background missing in {source.name}")
    require('"Edit in Designer"' in text, f"Designer menu missing in {source.name}")

currency = (ROOT / "src/EmilyDesk.Widgets.CurrencyConverter/CurrencyConverterWidget.cs").read_text()
require('"PHP"' in currency, "PHP currency support is missing")

system_info = (ROOT / "src/EmilyDesk.Widgets.SystemInfo/SystemInfoWidget.cs").read_text()
require("MemoryMappedFile" in system_info, "Core Temp shared-memory support is missing")
require("Core Temp • Core #" in system_info, "Hottest-core number is missing")

weather = (ROOT / "src/XWidgetReborn.Widgets.Weather/NativeWeatherWidget.cs").read_text()
designer = (ROOT / "src/EmilyDesk.Designer/DesignerForm.cs").read_text()
drag_drop = (ROOT / "src/EmilyDesk.Designer/ArtworkDragDrop.cs").read_text()
require("public const int DefaultForecastDayCount = 4" in weather,
        "New-layout four-day default is missing")
require("public const int MaximumForecastDayCount = 15" in weather,
        "Provider forecast capacity is not fifteen days")
require("Math.Min(MaximumForecastDayCount, days.Count)" in weather,
        "Weather loader does not retain all provider days")
require("SavedForecastDayCount(designerLayout)" in weather,
        "Live renderer does not use the saved layout day count")
require("ReflowDesignerForecastForFourDays" not in weather,
        "Runtime still forces saved weather layouts to four days")
require("MigrateIndustrialForecastToFourDays" not in designer,
        "Designer still truncates five-day weather layouts")
require('new ToolStripMenuItem("Forecast Days")' in designer,
        "Designer forecast menu was not generalized")
require("NativeWeatherWidget.MaximumForecastDayCount" in designer,
        "Designer does not expose all provider forecast days")
require("NativeWeatherWidget.MaximumForecastDayCount" in drag_drop,
        "Connect-as menu does not expose all provider forecast days")

layout_expectations = {
    "art-deco-weather.layout.json": 5,
    "botanical-weather.v2.layout.json": 4,
    "industrial-weather.layout.json": 4,
    "steampunk-weather.layout.json": 3,
    "woodland-weather.v2.layout.json": 4,
}
for name, expected_count in layout_expectations.items():
    layout = json.loads((ROOT / "Assets/DesignerLayouts" / name).read_text(
        encoding="utf-8-sig"))
    indices = []
    for element in layout.get("Elements", []):
        element_id = element.get("Id") or ""
        if element_id.startswith("forecast-day-") and element.get("Visible", True):
            indices.append(int(element_id.rsplit("-", 1)[1]))
    require(indices and max(indices) + 1 == expected_count,
            f"{name} no longer retains its authored forecast count")

print("PASS: EmilyDesk Build 1372 optional-widget and Designer source validation")
