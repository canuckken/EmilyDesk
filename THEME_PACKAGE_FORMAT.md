# EmilyDesk theme packages

An `.emilytheme` file is a ZIP archive with `theme.json` at its root. It is
installed for the current user from **Dashboard → Settings → Import Theme**.
Installing or replacing a theme never removes personal layouts or settings.

Required `assets` keys:

- `weather`, `weatherDetails`, `calendar`, `clock`
- `recycleEmpty`, `recycleFull`, `dock`
- `weatherIcons` (a directory containing numbered weather PNGs)

Required `layouts` keys:

- `weather`, `calendar`, `clock`

Optional matching Gallery artwork keys:

- `galleryWeather`, `galleryCalendar`, `galleryClock`
- `galleryRecycleBin`, `galleryDock`

When an imported package supplies these previews, Gallery uses the matching
widget image and never falls back to an unrelated built-in theme preview.

Optional wallpaper keys:

- `wallpaper1Wide`, `wallpaper1Tall`
- `wallpaper2Wide`, `wallpaper2Tall`

Wide wallpapers use 3840x2160 and Tall wallpapers use 3840x2400. Imported
themes that provide these keys appear in Dashboard Wallpaper settings without
requiring their images to be installed in EmilyDesk's main Wallpapers folder.

Image and layout paths must be relative to the package root. Layout JSON may
refer to packaged files with `theme://<theme-id>/<relative-path>`. IDs use
lowercase letters, numbers, and hyphens; for example `ember-glow`.

The importer rejects absolute paths, parent-directory traversal, symbolic
links, unsupported file types, missing required assets, more than 1,024 archive
entries, packages over 512 MB, and individual entries over 256 MB. Replacement
uses a staging directory and restores the previous theme if the new package
cannot be committed.

See `ThemePackages/EmberGlow/theme.json` for a complete example.
