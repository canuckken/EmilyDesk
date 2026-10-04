#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "$0")" && pwd)"
theme_dir="$(cd "$root_dir/../.." && pwd)"
wallpaper="$theme_dir/ThemePackages/EmberGlow/wallpapers/ember-glow-wallpaper-1-3840x2160.png"
output="$root_dir/ember-glow-optional-widgets-approval-preview.png"

convert "$wallpaper" \
  -resize '1600x900^' -gravity center -extent 1600x900 \
  -fill '#120900' -colorize 30% \
  -font DejaVu-Serif-Bold -pointsize 34 -fill '#f4d9ad' \
  -stroke '#3a1704' -strokewidth 2 -gravity north \
  -annotate +0+38 'EMBER GLOW OPTIONAL WIDGETS' \
  -stroke none -font DejaVu-Sans -pointsize 17 -fill '#d6ad72' \
  -annotate +0+88 'Calculator  •  Currency Converter  •  System Information' \
  -gravity northwest \
  "$root_dir/calculator-preview.png" -geometry +100+245 -composite \
  "$root_dir/currency-preview.png" -geometry +495+145 -composite \
  "$root_dir/system-info-preview.png" -geometry +900+525 -composite \
  "$output"

printf '%s\n' "$output"
