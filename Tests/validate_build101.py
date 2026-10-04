"""Read-only v101 source checks. Not a substitute for Windows Build.cmd.

Usage: python3 Tests/validate_build101.py PATH_TO_V101_ZIP
"""
import hashlib
import json
import re
import sys
import xml.etree.ElementTree as ET
import zipfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
baseline = Path(sys.argv[1]).resolve()
expected = "4270f591ab96b4c6364b7bfcc1f51b450548302ef3795fe43ce87d24ded8469e"
assert hashlib.sha256(baseline.read_bytes()).hexdigest() == expected, "Not the approved v101 baseline"
changed = []
with zipfile.ZipFile(baseline) as archive:
    assert archive.testzip() is None, "Baseline CRC failure"
    names = set()
    protected = 0
    for item in archive.infolist():
        if item.is_dir():
            continue
        assert item.filename.startswith("EmilyDesk/"), item.filename
        relative = item.filename[len("EmilyDesk/"):]
        names.add(relative)
        path = root / relative
        assert path.is_file(), "Removed baseline file: " + relative
        original = archive.read(item)
        different = path.read_bytes() != original
        if different:
            changed.append(relative)
        if ((relative.startswith("Assets/") and
             not relative.startswith("Assets/DesignerLayouts/")) or
            relative.startswith(("Wallpapers/", "Widgets/")) or
            "Dock" in path.name or "Placement" in path.name):
            assert not different, "Protected file changed: " + relative
            protected += 1
    added = sorted(str(p.relative_to(root)) for p in root.rglob("*")
                   if p.is_file() and str(p.relative_to(root)) not in names)

layouts = list((root / "Assets/DesignerLayouts").glob("*.json"))
assert len(layouts) == 10
layers = sum(len(json.loads(p.read_text(encoding="utf-8-sig"))["Elements"]) for p in layouts)
assert layers == 508
uploaded_layout_hashes = {
    "art-deco-weather.layout.json": "d72e0bda0cad3ae38bad84a6c272cc55f6584cade8977a8d24357c5cbd8aa3e6",
    "botanical-weather.v2.layout.json": "a20a00d189baf23fb1684670490293eb8f8eedd66bdab3840b35f423e9b2192f",
    "industrial-calendar.layout.json": "2dc82c80dee6865250c2d07f16ec168cf8985fbf6caa63af95a5ff4aa94ccdb1",
    "industrial-clock.layout.json": "d3b881aa058990f6c30dc3708478c903e9746d69c3b2351670a13d7980dd832c",
    "industrial-weather.layout.json": "a6bfca6bdf730b6f85b72f0d08bfe625234ab9dc47346d76913ef0e94c54df04",
    "steampunk-calendar.layout.json": "de5d25730e41ecec9ecade70038c7b8a6ef9bba6232eb41aeab6f172ce9af2c1",
    "steampunk-weather.layout.json": "b820d219886ca82d4954f47e08408637329a9330c9f03e2aaa846c2785741068",
    "vintage-calendar.layout.json": "7cf2caa509fd82b8f015e712d7845dfa71bcf29380d5c7720a8c5f8aab18cdc3",
    "woodland-calendar.layout.json": "5e3339671be3c529499af14cd183e41a01bfe76326008ea258aeb898ebce2355",
    "woodland-weather.v2.layout.json": "3412dcc39a91255e4c802d4356e691338b96e1ce42dfba1fb8ab2567e2f9a369",
}
for name, expected_hash in uploaded_layout_hashes.items():
    assert hashlib.sha256((root / "Assets/DesignerLayouts" / name).read_bytes()).hexdigest() == expected_hash

ns = {"m": "http://schemas.microsoft.com/developer/msbuild/2003"}
projects = list((root / "src").rglob("*.csproj"))
compiled = set()
for project in projects:
    doc = ET.parse(project)
    includes = []
    for item in doc.findall(".//m:Compile", ns) + doc.findall(".//m:ProjectReference", ns):
        name = item.get("Include")
        assert name not in includes, "Duplicate compile/reference in " + str(project)
        includes.append(name)
        target = (project.parent / name.replace("\\", "/")).resolve()
        assert target.is_file(), "Missing compile/reference: " + str(target)
        if target.suffix == ".cs":
            compiled.add(target)

# Strip comments and literal strings before checking structural delimiters.
# This is a lexical check only, not a C# compiler or type checker.
literals = re.compile(r'//[^\n]*|/\*[\s\S]*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'')
matching = {"}": "{", ")": "(", "]": "["}
for path in sorted(compiled):
    source = path.read_text(encoding="utf-8-sig")
    clean = literals.sub("", source)
    stack = []
    for token in clean:
        if token in "{([":
            stack.append(token)
        elif token in "})]":
            assert stack and stack.pop() == matching[token], "Delimiter mismatch: " + str(path)
    assert not stack, "Unclosed delimiter: " + str(path)
for relative in added:
    if relative.startswith("src/") and relative.endswith(".cs"):
        assert (root / relative).resolve() in compiled, "Uncompiled new source: " + relative

for path in [root / "Build.ps1", root / "Installer.iss", root / "Installer-UserMode.iss"]:
    assert "2.10.27" in path.read_text(encoding="utf-8-sig")

installer = (root / "Installer-UserMode.iss").read_text(encoding="utf-8-sig")
assert "DefaultDirName={autopf}\\EmilyDesk" in installer
assert re.search(r"(?m)^PrivilegesRequired=admin\s*$", installer)
assert re.search(r"(?m)^ArchitecturesInstallIn64BitMode=x64compatible\s*$", installer)
assert "VersionInfoVersion=2.10.27.0" in installer
assert "VersionInfoProductVersion=2.10.27" in installer
assert "deploy-service-and-verify" in installer
assert "remove-service-deployment" in installer
assert not re.search(r"(?m)^Source: .*Compatibility-Bootstrap", installer)
assert not re.search(r"(?m)^Name: .*One-time Compatibility Bootstrap", installer)
assert (root / "Installer.iss").read_text(encoding="utf-8-sig").startswith("#error")

features = {
    "font dropdown": (root / "src/EmilyDesk.Designer/DesignerLayout.cs", "DesignerFontNameConverter"),
    "widget font": (root / "src/EmilyDesk.Designer/DesignerForm.cs", "Choose Font from Widget Folder"),
    "divider layer": (root / "src/EmilyDesk.Designer/DesignerForm.cs", "Add Divider Layer"),
    "weather backgrounds": (root / "src/EmilyDesk.Designer/DesignerForm.cs", "main-background"),
    "save prompt": (root / "src/EmilyDesk.Designer/DesignerForm.cs", "ConfirmSaveLiveEdits"),
    "opacity hover": (root / "src/XWidgetReborn.Runtime/Core/WidgetWindow.cs", "host.fullOpacityOnHover"),
    "four-day fallback": (root / "src/XWidgetReborn.WeatherCore/Service.cs", "EnsureCompleteForecast"),
    "botanical detail icons": (root / "src/XWidgetReborn.Widgets.Weather/NativeWeatherWidget.cs", "IsBotanical"),
    "smooth large-PNG resizing": (root / "src/EmilyDesk.Designer/DesignerCanvas.cs", "MaximumPreviewImageDimension"),
}
for label, (path, marker) in features.items():
    assert marker in path.read_text(encoding="utf-8-sig"), "Missing " + label
assert json.loads((root / "ProviderProfiles/weatherapi.json").read_text())["query"]["days"] == "4"
drag_drop = (root / "src/EmilyDesk.Designer/ArtworkDragDrop.cs").read_text(encoding="utf-8-sig")
assert "64 * 1024 * 1024" not in drag_drop
assert "64L * 1024L * 1024L" not in drag_drop
assert "smaller than 64 MB" not in drag_drop
weather_source = (root / "src/XWidgetReborn.Widgets.Weather/NativeWeatherWidget.cs").read_text(encoding="utf-8-sig")
assert "DrawEditableDesignerBackground" not in weather_source
assert weather_source.count("RenderEditableDesignerBackground") == 6
editable_weather = (root / "src/XWidgetReborn.Widgets.Weather/EditableWeather.cs").read_text(encoding="utf-8-sig")
assert "ThemeSkinCache.GetAlphaBounds(imagePath)" in editable_weather
assert "mirroredArtDecoDetails" in editable_weather
designer_canvas = (root / "src/EmilyDesk.Designer/DesignerCanvas.cs").read_text(encoding="utf-8-sig")
assert "editableBackground ||" in designer_canvas
assert "GetPreviewImage(path)" in designer_canvas
assert "CompositingQuality.HighSpeed" in designer_canvas
designer_form = (root / "src/EmilyDesk.Designer/DesignerForm.cs").read_text(encoding="utf-8-sig")
assert "Image image = _canvas.GetPreviewImage(path);" in designer_form

active_sources = [
    root / "src/XWidgetReborn.Shared/AppConstants.cs",
    root / "Compatibility-Audit.ps1",
    root / "Stage3-OfficialWidgets-Test.ps1",
]
for path in active_sources:
    text = path.read_text(encoding="utf-8-sig")
    assert "api.accuweather.com" not in text
    assert "127.0.0.1:45873" in text

print("PASS: approved v101 SHA256 and ZIP CRC; no original files removed")
print("PASS:", protected, "protected files unchanged; 10 uploaded layouts / 508 layers preserved")
print("PASS:", len(projects), "project XMLs;", len(compiled), "C# files with balanced delimiters and valid compile references")
print("PASS: all requested Designer fixes have source-level coverage")
print("PASS: secure Program Files installer, loopback endpoint, upgrade cleanup, and data-preserving uninstall declarations")
print("CHANGED ORIGINAL FILES:\n" + "\n".join(sorted(changed)))
print("ADDED FILES:\n" + "\n".join(added))
print("NOT RUN: Windows compilation, executable tests, interactive UI validation")
