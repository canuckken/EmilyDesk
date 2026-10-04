# EmilyDesk optional widget packages

Build 1351 introduces `.emilywidget`, a ZIP-based package for optional native
widgets. This is a one-time EmilyDesk platform update; later widgets and widget
updates can be distributed without making a new EmilyDesk installer.

## For users

1. Install EmilyDesk Build 1351 or later once.
2. Open **Widget Gallery > Import Widget...** or **Settings > Import Widget...**.
3. Select the `.emilywidget` file.
4. Confirm the displayed name, version, and publisher only if the source is
   trusted.
5. Find the new card in Gallery or select the **Imported** filter.

To remove one, right-click its Gallery card and select **Remove Imported
Widget...**. This does not modify the EmilyDesk installation or other widgets.

Imported files live in `%USERPROFILE%\Documents\EmilyDesk\Widgets`; the
**Open Widget Folder** command opens that location. Importing a newer version
replaces only the matching widget package. EmilyDesk refuses
same-version replacements, downgrades, bundled/native IDs, unsafe archive
paths, invalid images, incompatible SDK versions, and incompatible EmilyDesk
versions.

An imported widget is executable code, not a sandboxed picture or layout.
Only import packages supplied by a trusted person or site.

## Package layout

```text
manifest.json
WidgetAssembly.dll
icon.png
preview.png
```

The manifest names the assembly and concrete type implementing `IWidget`.
Optional packages must set `official` and `bundled` to `false`, must not use an
ID beginning with `native.`, and should default to disabled so installation
does not unexpectedly open a desktop window.

```json
{
  "id": "utility.example",
  "name": "Example Widget",
  "description": "A short description.",
  "version": "1.0.0",
  "author": "Publisher name",
  "assembly": "Example.Widget.dll",
  "type": "Example.Widget.ExampleWidget",
  "minimumSdkVersion": "2.8.1.0",
  "minimumEngineVersion": "2.10.27",
  "icon": "icon.png",
  "preview": "preview.png",
  "enabledByDefault": false,
  "category": "Utilities",
  "official": false,
  "bundled": false,
  "capabilities": [ "offline", "settings" ]
}
```

The package filename may include a friendly name and version, for example
`ExampleWidget-1.0.0.emilywidget`. Package identity and update matching use the
manifest `id`, not the filename.
