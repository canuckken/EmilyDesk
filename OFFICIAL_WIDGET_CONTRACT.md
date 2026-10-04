# XWidget Reborn Official Bundled Widget Contract

## Purpose

An official bundled widget is a native Widget SDK plug-in shipped with XWidget Reborn. It uses the normal `IWidget` lifecycle and may opt into host-managed settings by implementing `IOfficialWidget`. Third-party `IWidget` plug-ins remain source and binary compatible.

## Manifest

Official widgets use the existing package manifest and add:

- `category`: stable gallery category, such as `Clock`.
- `official`: `true` only for project-maintained widgets.
- `bundled`: `true` when installed with the product.
- `minimumEngineVersion`: minimum compatible Reborn engine version.
- `capabilities`: descriptive feature identifiers such as `settings`, `scale`, and `opacity`.

Identity is the manifest `id`. It must remain stable across upgrades because lifecycle, position, enabled state, topmost state, and settings are keyed by it. Display names may change without changing identity.

## Runtime contract

The required `IWidget` contract remains responsible for metadata, lifecycle, scheduling, and rendering. An official widget may also implement `IOfficialWidget`:

- `AttachHost` receives an `IWidgetHostContext`.
- `ShowSettings` opens widget-specific settings.
- `ResetSettings` returns the widget to documented defaults.
- `GetMenuCommands` supplies optional widget-specific context-menu commands.

The host context provides namespaced persistent string settings, host opacity, host scale, preferred logical size, rectangular/elliptical/rounded-rectangle window shape, and invalidation. Widgets must not access another widget's settings.

An interactive native widget may additionally implement `IWidgetPointerInput`. `PointerDown` returns `true` only when the widget claims the pointer (for example, a Calendar navigation button). An unclaimed left press remains host-managed window dragging. Move, up, and leave notifications let widgets render hover/pressed states without adding child windows or taking ownership of general movement.

An official widget with selectable visual themes may implement `IWidgetThemeProvider`. The shared host then exposes those themes in the standard context menu. Theme application and persistence remain widget-owned because the theme controls widget rendering rather than window behavior.

An official widget with structurally different built-in renderers may implement `IWidgetStyleProvider`. Stable style IDs are persisted independently from themes. The host supplies the common Style submenu; the widget owns renderer selection, compatible-theme normalization, guarded rendering, and the mandatory built-in Modern fallback. See `OFFICIAL_WIDGET_STYLE_SYSTEM.md`.

## Persistence and migration

Host state is stored below `%LOCALAPPDATA%\XWidget Reborn\WidgetState`:

- `<id>.position`
- `<id>.enabled`
- `<id>.topmost`
- `<id>.<key>.setting`

Keeping `native.clock` as the Clock identity automatically preserves its placement and lifecycle state from earlier builds. New Clock settings use defaults when no setting file exists. Upgrades must not delete existing state or create a second widget identity.

## Common behavior

Official widgets use the existing widget surface for drag, close, pause/resume, topmost, and lifecycle behavior. Settings should update the existing instance, preserve its position, and avoid duplicate instances. Scale changes logical size through the host. Opacity is host-owned and bounded to a usable range. Menu checked state is queried from the widget whenever its context menu opens so persisted or recently changed settings cannot leave stale indicators.

The shared official-widget context menu provides Scale, Opacity, Always On Top, Lock Position, Click Through, optional Theme, advanced Widget Settings, and Close Widget. Lock and click-through state use namespaced host settings. Selecting a click-through widget in the Gallery restores pointer interaction, providing a reliable path back to its context menu.

The current lifecycle manager supports one live instance per stable widget ID. Multiple instances are intentionally unsupported until instance identity and per-instance state are defined.

## Gallery behavior

The virtual gallery receives category and official/bundled metadata through the existing engine catalog. Official widgets display an `Official` category label and retain the normal Running/Not running badge. The gallery remains a single virtual drawing surface; this contract does not introduce per-widget controls.

## Performance requirements

Rendering must be side-effect free, avoid file I/O, and dispose temporary drawing resources. Updates should use the slowest scheduler rate that meets the selected behavior. Settings persistence occurs only when values change, not during paint. Widgets must release host references and callbacks when stopped.

Per-pixel shapes are rendered by the shared host. Widgets request a shape and paint their normal alpha-aware surface; they must not add their own layered-window presentation, custom drag loop, or native child controls.

## Packaging

The manifest, widget assembly, and preview/icon are packaged in the widget folder. Package inspection preserves the official metadata fields. Official status is a distribution trust designation; setting `official: true` in an untrusted third-party package does not by itself establish provenance.
