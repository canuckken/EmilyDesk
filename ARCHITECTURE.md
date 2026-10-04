# XWidget Reborn Platform Architecture

## Product direction

XWidget Reborn is an independent desktop widget platform. It is not a patched or replacement build of the original XWidget executable. Existing XWidget widgets are supported through an optional compatibility adapter.

## Dependency rule

```
Dashboard -> Runtime -> Platform Core
                       -> Native Widgets
                       -> Plugins
                       -> Legacy.XWidget Adapter
```

The platform core must never depend on original XWidget binaries, file locations, registry keys, or object models. Compatibility code may depend on legacy formats, but the dependency must point inward through neutral runtime contracts.

## Runtime responsibility model

`RuntimeHost` is the Engine coordinator. It composes services and delegates
work; it does not discover plug-ins, implement widget lifecycle transitions, or
own shared-service implementations.

Runtime dependencies are registered by contract. Logging, settings,
weather, the widget registry, and event dispatch are resolved through
`IRuntimeLoggingService`, `IRuntimeSettingsService`,
`IRuntimeWeatherService`, `IWidgetRegistry`, and `IEventDispatcher`. Concrete
implementations remain confined to the composition root in `RuntimeHost`.

The lightweight service set is:

- `IWidgetRegistry`: manifest discovery and installed catalog
- `ILifecycleManager`: six-state widget lifecycle
- `IRuntimePackageManager`: Runtime-side package activation/catalog refresh
- `IRuntimeLoggingService`: Information, Warning, Error, and Debug logging
- `IRuntimeSettingsService`: enabled state, position, topmost state, migrations
- `IEventDispatcher`: cross-component widget events

The existing weather compatibility service remains registered. The container
is deliberately a small typed instance map; it does not perform automatic
construction, lifetime scopes, or reflection-based dependency injection.

## Weather API startup ownership

The Dashboard selects exactly one owner for the local loopback endpoint:

1. Retain an already healthy Weather Engine.
2. If the administrative Windows service is registered, start and verify that
   service. The existing setup helper is used for elevation only when Windows
   denies a direct service start.
3. Only when no service is registered, start the per-user in-process
   `BridgeHost`.

The production installer reserves `http://127.0.0.1:45873/` only for
LocalSystem and installs the service plus its dependencies below Program Files.
It also removes the obsolete AccuWeather hosts-file mapping during upgrade.
The Dashboard verifies the HTTP status endpoint before continuing.

Package archive validation and transactional file installation remain in the
Dashboard. `IRuntimePackageManager` does not duplicate that logic: it owns the
Runtime boundary that refreshes installed packages after a completed Dashboard
transaction.

```
RuntimeHost
  -> ServiceContainer
       -> IRuntimeLoggingService
       -> IRuntimeSettingsService
       -> IRuntimeWeatherService
       -> IWidgetRegistry
       -> ILifecycleManager
       -> IRuntimePackageManager
       -> IEventDispatcher
  -> WidgetRegistry
       -> manifest discovery
       -> installed-widget catalog and lookup
  -> LifecycleManager
       -> Discovered -> Loaded -> Running
       -> Running <-> Suspended
       -> Stopped / Failed
  -> EventDispatcher
       -> WidgetInstalled / WidgetRemoved
       -> WidgetStarted / WidgetStopped / WidgetRestarted
       -> WidgetCrashed / WidgetFailed
```

`WidgetInstance` is limited to the mechanics of one widget contract, scheduler
subscription, and window. Persistent enabled state and architectural lifecycle
state belong to `LifecycleManager`.

Runtime business components receive logging and settings services through
composition. `RuntimeLog` is only the file sink behind
`IRuntimeLoggingService`; Runtime components do not write to it directly.
Events are used for catalog and lifecycle facts, while commands such as start,
stop, suspend, resume, and package refresh remain direct method calls.

This boundary is the foundation for package installation, registry refresh,
hot reload coordination, and additional runtime services.

## Widget package installation

`.xwrwidget` files are ZIP archives containing `manifest.json` at the archive
root. The Dashboard validates archive paths, size limits, required metadata,
SDK compatibility, managed assembly identity, widget contract/type shape, and
optional image assets in an isolated AppDomain.

Validated packages are extracted to a private staging directory and atomically
moved to:

```
<installation directory>\Widgets\<widget id>
```

The Dashboard then sends a generic registry-refresh command. `WidgetRegistry`
rescans manifests, publishes `WidgetInstalled`, and updates Engine status. The
open gallery reads that status and selects the newly installed widget without
restarting the Dashboard or Engine. Runtime construction still passes through
`LifecycleManager`, so package installation does not bypass lifecycle or
persistence rules.

### Legacy `.xwp` packages

The Gallery also accepts original XWidget `.xwp` ZIP archives. The legacy
installer validates archive size, entry count, entry sizes, and every path
before extraction. It locates one folder containing both `widget.xwl` and
`main.xul`, including the common
`Widgets/<widget folder>/` package layout, and installs the complete subtree
transactionally under:

```
%USERPROFILE%\Documents\XWidget\Widgets\<widget folder>
```

Existing folders are never silently overwritten. Replacement requires an
explicit user choice and uses the same backup/move/rollback pattern as native
packages.

Some original XWidget package writers emitted an incorrect per-disk ZIP entry
count. For compatibility, the installer corrects that one legacy header field
in a private temporary copy after confirming the archive is not split; the
selected source package is never modified.

`WidgetRegistry` discovers valid legacy folders alongside native manifests and
publishes them in the same gallery catalog with stable `legacy.*` diagnostic
IDs. Opening a legacy gallery entry delegates `widget.xwl` to the installed
original XWidget executable through `LegacyExternalWidget`. Reborn does not
pretend to interpret XUL, scripts, or original XWidget object models natively.
It also does not infer activation from registration in XWidget's dock. The
adapter tracks the specific external XUL surface created for each launch. A
background watcher reports confirmed surface closure to the Runtime lifecycle;
only then is restore intent cleared. Orderly Engine shutdown suppresses
user-close persistence changes, preserving the enabled flags of widgets that
were still open for the next restore.

Persisted activation is stored per widget under
`%LOCALAPPDATA%\XWidget Reborn\WidgetState` as `<widget-id>.enabled`.
Discovery alone never creates restore intent. Activation is saved only after a
visible surface is created, while explicit close is saved only after that
surface has closed.

Widget activation uses identity-owned handles rather than window titles.
Native widgets use their `WidgetWindow.Handle`; legacy widgets use the exact
XUL surface handle captured when their `widget.xwl` launch completes. The
Dashboard grants foreground permission to the Engine, and the legacy adapter
transfers that permission to the owning XWidget process. Activation restores
and briefly promotes the target above the Dashboard, immediately removes the
temporary topmost state, and requests foreground once. Existing instances use
the same tracked handle, so activation does not create duplicates or change a
widget's persisted always-on-top preference.

The Dashboard installer and Runtime registry share
`LegacyWidgetIdentity.LibraryPath`, so extraction and discovery always use the
same library. They also share `LegacyWidgetMetadataReader`. Legacy metadata is
read case-insensitively, with `displayName` taking precedence over the
skin-authoring `name` field; this prevents internal values such as `Untitled1`
from leaking into the Gallery. The Dashboard reports completion only after the
Engine has refreshed its registry and rediscovered the stable widget ID with
the expected display name.

## Phase 2 migration policy

The original XWidget host may be launched temporarily as a fallback for legacy widgets not yet implemented by the adapter. This bridge is transitional and must not gain new features. All new capabilities belong to the independent Runtime.

## Visual Widget Gallery

The Dashboard coordinates one resizable, DPI-scaled Widget Gallery window.
The Gallery is modeless and unowned: both windows remain enabled and may move
normally through the desktop z-order. `MainForm` retains the live Gallery
reference, restores and activates that instance when requested again, and
clears the reference on `FormClosed`; duplicate Gallery windows are therefore
not created. The previous narrow list/details browser has been replaced by a
wrapping card grid whose
preview remains the dominant element. `GalleryWidgetItem` adapts neutral
`WidgetDescriptor` records into presentation state, `WidgetPreviewResolver`
owns preview discovery and path caching, `WidgetGalleryCard` owns card
rendering and direct interaction, and `WidgetManagerForm` coordinates search,
filters, commands, installation, and incremental card updates.

Preview resolution runs outside the UI thread and follows this order:

1. explicit descriptor preview/thumbnail metadata;
2. known preview, thumbnail, screenshot, default, or background assets;
3. the largest suitable representative image in the installed widget folder;
4. a generated neutral placeholder.

Legacy candidates reject small images and names associated with buttons,
controls, hands, needles, condition icons, and other fragments. Thumbnails are
cloned into memory and source images are closed immediately, preventing file
locks. Resolved source paths are cached per widget ID for the Gallery process.
Preview discovery and decoding remain on a cancellable worker. Completed
previews are delivered to the UI in batches of four, and a generation token
causes results from a closed or rebuilt Gallery to be disposed instead of
applied to stale cards.

The 750 ms running-state timer schedules a non-overlapping background status
read. If a read is still active, that tick is skipped. Completion marshals only
changed card state to the UI thread; the Running filter inserts or removes only
the affected cards rather than rebuilding the complete grid. Pending status
and preview workers are cancelled or ignored during disposal.

Single-click selects a card. Double-clicking its preview or using its Launch
link sends the existing Engine open command. A running card sends the same
command, allowing `LifecycleManager` to activate the existing instance rather
than create a duplicate. Context actions expose Open/Bring to Front, conditional
Close, Details, and Open Widget Folder. Uninstall is intentionally disabled
until a transactional uninstall service exists.

The Runtime status file remains the single running-state source. Native
`WidgetWindow` instances and legacy widgets with a confirmed visible tracked
surface appear in `widgets=`. The Gallery polls that compact state and changes
only cards whose running state changed; the Running filter is reflowed only
when required. Closing a tracked legacy surface still clears persisted restore
intent through the lifecycle watcher.

Known limitations: uninstall is unavailable, future Weather/Clock/System/
Utility categories require explicit package metadata, and the original XWidget
dock remains installed and available. A later migration may suppress that dock
after direct legacy launching and lifecycle ownership are fully independent.

## Runtime foundation in 2.3.0

- Dedicated `XWidgetReborn.Runtime.exe` process
- Runtime host and widget manager
- Explicit widget-instance lifecycle
- Borderless draggable widget windows
- Standalone native welcome widget
- Tolerant legacy `widget.xml` adapter
- Dashboard startup integration
- Build and packaging integration
