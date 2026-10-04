EmilyDesk Build 99 - Woodland Size Verification Fix
Application version: 2.10.25
Baseline: EmilyDesk_WoodlandWeatherCalendarSizeMatch_v98.zip only.

Corrects the Stage 5 build failure "Weather Woodland baseline footprint changed."
The old test still required the previous 1081 x 494 weather drawing surface.
It now requires the intentional 1141 x 524 Woodland surface from Build 98.
Designer save/render tests also exercise Woodland weather at its new size.
All build verification stages remain enabled.

No production widget, layout, artwork, dock, bin or snapping code was changed.
Woodland weather still matches the calendar's visible frame at 100%.
Your saved Designer edits and relative text/divider positions are preserved.

Close EmilyDesk and Designer, extract this ZIP into a new folder, then run
Build.cmd inside the included EmilyDesk folder. Do not delete saved layouts.

The screenshot confirms Build 98 compiled and passed Designer verification;
it stopped at the stale Stage 5 size assertion corrected here.
This replacement was source-checked and its complete ZIP verified.
Windows compilation and executable tests could not be run in this environment.
Earlier build notes included in the folder are historical.
