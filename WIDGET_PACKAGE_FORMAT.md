# XWidget Reborn Widget Package Format

## Container

A widget package uses the `.xwrwidget` extension and is a ZIP archive. The
archive must contain `manifest.json` at its root. Package paths must be relative
and remain inside the widget directory.

The archive contains the compiled widget assembly named by `assembly`, the
manifest, every resource stored with the native widget source (including the
configured icon/preview), and any other files required by that widget.

Limits:

- 100 MB compressed package
- 100 MB total expanded content
- 50 MB per entry
- 256 entries

## Required manifest fields

- `id`: stable package/widget identifier using letters, numbers, dots, hyphens,
  or underscores
- `name`: gallery display name
- `version`: widget version
- `author`: widget author or publisher
- `description`: gallery description
- `assembly`: relative path to the managed widget DLL
- `type`: fully qualified type implementing
  `XWidgetReborn.WidgetSdk.IWidget`

## Optional manifest fields

- `minimumSdkVersion`: minimum compatible Widget SDK assembly version
- `icon`: relative path to a valid image
- `preview`: relative path to a valid gallery preview image
- `enabledByDefault`: whether a newly discovered widget starts automatically

## Installation behavior

The Dashboard validates the complete archive before changing the installed
widget catalog. A valid package is atomically installed under
`Widgets\<id>`. If the same widget version is already installed, the Dashboard
explains that no installation is needed. An older package cannot replace a
newer installed version. A newer package shows the widget's display name and
versions and offers **Replace existing widget** or **Cancel**.

Replacement is transactional: the existing widget directory is moved to a
temporary backup, the validated package is moved into place, and the backup is
restored if the new package cannot be installed. After installation or
replacement, the Dashboard requests an Engine registry refresh and updates the
open gallery.

User-facing package messages use the manifest display name. Stable internal
widget IDs are retained only for registry coordination and diagnostic logs.

Opening and closing a package uses its manifest ID and the same lifecycle path
as the built-in Reborn Clock.

## Build output

`Build.ps1` discovers every native widget directory under `Widgets` that has a
`manifest.json`. After compiling the solution, it resolves the manifest's
assembly from the matching Release output, copies the complete widget resource
directory, and creates:

`Output\Packages\<ManifestNameWithoutSpaces>.xwrwidget`

The current built-in widget therefore produces
`Output\Packages\RebornClock.xwrwidget`.

Every generated package is reopened and checked for its root manifest,
assembly, and configured images. The build then launches the Dashboard's real
package installer in verification mode against an isolated temporary `Widgets`
directory. It verifies fresh installation, same-version detection,
non-destructive cancellation, and transactional newer-version replacement. The
build fails if any scenario, archive validation, assembly/type validation, or
file installation fails. The temporary verification installation is removed
when the build ends.
