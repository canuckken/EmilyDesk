EmilyDesk Build 97 - Designer Layers and Nature Weather Save Fix
Application version: 2.10.23

Baseline: EmilyDesk_DesignerSaveAndRealisticBins_v96.zip only.
This is a complete source/build package with the EmilyDesk top-level folder.

Changes:
- Art Deco, Vintage and Modern calendars and clocks now have editable built-in
  layers instead of a flat preview with an empty layer list.
- Vintage and Modern weather have editable main and forecast-panel layers.
- Woodland Nature and Botanical Nature weather render the complete saved
  layout, including added layers, styling, visibility and details-panel edits.
- Saved nature weather settings no longer depend on changed-layer ID hints.
- Changing the style of a details field no longer reapplies its initial style.
- Moving, resizing or deleting Art Deco details fields no longer causes the
  native default panel to silently replace the saved panel.
- Existing overlays and intentional deletions are preserved during migration.
- Normal builds now check existing built-in edits, deletions, reopening,
  details panels and migration, as well as the existing added-layer checks
  across all 21 clock/calendar/weather theme combinations.

Preserved:
All approved docks, realistic empty/full recycle-bin artwork and bin behavior,
the absence of bin label/count boxes, and the working snapping implementation.
No XWidgetTheme folder is included or restored.

Build and test:
1. Close EmilyDesk and its Designer.
2. Extract this ZIP to a NEW folder. Do not merge it into an old source folder.
3. Open the included EmilyDesk folder and run Build.cmd on Windows.
4. Use the generated build as usual. Existing user settings/layouts are retained.

The isolated Designer tests run automatically during the build. They do not
modify your live layouts or request live weather. Results are written to
BuildLogs/designer-save-tests.txt; a failure writes designer-save-error.txt.

Verification performed for this package:
Offline source/project integrity and byte-for-byte preservation checks passed.
Windows compilation, runtime tests and interactive rendering were NOT run in
the packaging environment, which has no Windows/.NET Framework toolchain.
Please check your saved edits in the running widgets after building.
