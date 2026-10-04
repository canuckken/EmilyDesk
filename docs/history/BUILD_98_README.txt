EmilyDesk Build 98 - Woodland Weather / Calendar Size Match
Application version: 2.10.24
Baseline: the approved EmilyDesk Build 97, unchanged except for this size fix.

Woodland Nature weather now matches the calendar's visible frame at 100%:
approximately 503 x 309 pixels, instead of approximately 476 x 291.
The calendar itself is unchanged.

Only the weather output surface is enlarged. Its background, text, icons and
dividers scale together. Saved Designer coordinates, font settings, custom
images, panel layout and other edits are NOT rewritten or reset.
Your selected scale percentage is preserved; compare both widgets at 100%.

All other theme sizes, docks, bins, artwork, and the approved snapping logic
are unchanged. The weather visible-edge anchor uses the enlarged output size
so its interaction and screen-edge positioning follow the enlarged frame.

Close EmilyDesk and Designer, extract into a new folder, and run Build.cmd
inside the included EmilyDesk folder. Do not delete your saved layouts.

The normal Windows build retains all 21 Designer save/render checks and adds
Woodland frame-size, theme-switching, slide-panel and saved-layout checks.
Results: BuildLogs/designer-save-tests.txt.

Packaging validation: source/asset preservation and size calculations checked.
Windows compilation and interactive desktop behavior remain untested here.
