EmilyDesk Build 100 - Your Saved Designer Layouts
Application version: 2.10.26
Baseline: EmilyDesk_WoodlandSizeBuildFix_v99.zip only.

Includes the newest saved version of all 10 current widget layouts supplied
in your two ZIP files. No positions, dimensions, fonts, colours, layer order,
visibility, deleted layers or details-panel settings were changed.

Included edits:
  Art Deco: weather
  Botanical Nature: weather
  Industrial: clock, calendar, weather
  Steampunk: calendar, weather
  Vintage: calendar
  Woodland Nature: calendar, weather

The layouts are shipped in Assets/DesignerLayouts as read-only defaults.
They load automatically in both Designer and the live widgets when there is
no personal save. Existing personal layouts always take priority; between
local and shared saves, the newest file still wins (local wins a tie).
Saving further Designer edits continues to write your user-local layout,
not the packaged default. Build/install never copies over personal layouts.

The Steampunk layout referenced the built-in icon pack through an absolute
path on your PC. The packaged copy now selects the same built-in theme pack
without that machine-specific path. No other layout content was changed.

Both uploaded ZIPs are preserved unchanged in LayoutImportBackup. The old
woodland-weather.layout.json is retained there only; the current widget uses
woodland-weather.v2.layout.json. LAYOUT_IMPORT_V100.txt records each selection.

Close EmilyDesk and Designer, extract into a new folder, and run Build.cmd
inside the included EmilyDesk folder. No manual layout copying is needed.
Do not delete your existing saved layouts.

All approved docks, recycle bins, snapping, and Woodland sizing are preserved.
Factory geometry checks remain enabled and use factory layouts; separate
Designer checks exercise all 10 imported layouts in isolated temporary folders.

Source, layout preservation, asset references and ZIP integrity were checked.
Windows compilation and executable/visual checks could not be run here.
Build.cmd runs the Windows verification sequence on your PC.
Earlier build notes included in this folder are historical.
