# Official Widget Style System

## Scope

The Phase G.2 style system supports small, built-in renderer families without
changing widget identity, lifecycle, scheduling, interaction, or persistence.
It intentionally does not load external code. `Clock.Modern` and
`Calendar.Modern` are permanent built-in fallbacks; Steampunk demonstrates a
second renderer family.

Styles and themes are separate:

- A style changes structure, layout, typography, framing, and drawing details.
- A theme supplies compatible colors for the active style.

## Designer text rendering and colour editing

Anti-aliased text is the standard for running widgets, the Designer canvas,
and generated Gallery previews. New text renderers must use the approved
anti-aliased rendering mode and must not fall back to single-bit, system-default,
or pixel-grid text rendering.

Every editable text layer uses the shared visual colour picker. It provides a
large saturation/shade field (up is lighter and down is darker), a separate hue
strip, exact red/green/blue/alpha controls, an original-versus-selected preview,
and quick colour swatches. Exact `#AARRGGBB` and `R, G, B, A` entry remains
available for users who already know the required value.

Boolean Designer choices are always shown as clearly named checkboxes or
checked toolbar commands. Raw `True`/`False` selection is an internal storage
detail and must not be exposed as the normal editing experience. Advanced
Settings must hide Boolean properties that already have a friendly checkbox.

## Public metadata contract

An official widget opts in by implementing `IWidgetStyleProvider`. It returns
`WidgetStyleMetadata` records containing a stable ID, display name,
description, compatible widget type, preferred and minimum sizes, preview
asset, and supported themes. The Engine automatically creates the shared
Style submenu and checks the active item.

`IWidgetThemeProvider` remains independent. Its `Themes` collection may change
when `Style` changes, and the Engine rebuilds the Theme submenu whenever the
context menu opens.

## Adding another built-in style

1. Add a renderer implementing the widget project's small internal renderer
   interface (`IClockStyleRenderer` or `ICalendarStyleRenderer`).
2. Keep timekeeping, calendar navigation, settings, pointer routing, and host
   interaction in the widget; the renderer receives current state and draws
   only appearance.
3. Add a stable style ID and metadata record to the widget's `Styles`
   collection.
4. Map the ID to the renderer in `ActiveRenderer`.
5. Add compatible themes and a safe default theme.
6. Add a preview asset beside the widget manifest.
7. Exercise Modern, the new style, invalid-ID fallback, all supported sizes,
   transparency, and existing commands before packaging.

Every render call is guarded. A renderer exception is written to the Engine
log through the host diagnostic channel (and to `Trace` when no host is
attached), the style is reset to built-in Modern, and Modern renders
immediately in the same frame. Invalid persisted IDs normalize to Modern
during settings load. Style changes persist under the widget's existing
namespaced `style` setting and do not alter position, scale, opacity, topmost,
or lifecycle state.

## Future external packages

A future `.xwrstyle` archive may contain a manifest, preview assets, and one or
more renderer assemblies grouped by compatible widget type. Before external
loading is implemented it requires signature/trust policy, assembly isolation,
API-version checks, validation, failure quarantine, and transactional
installation. Phase G.2 deliberately ships only compiled built-in renderers.
