#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/../.." && pwd)"
out="$root/DesignPreviews/BotanicalOptionalWidgets"
source_skin="$root/Assets/Themes/BotanicalNature/botanical-calendar-skin.png"
wallpaper="$root/Wallpapers/EmilyDesk_Botanical_Nature_Wallpaper_1_3840x2160.png"
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$out"

make_frame() {
  local width="$1" height="$2" output="$3"
  local corner_w=110 corner_h=90 middle_w=$((width - 220)) middle_h=$((height - 180))

  convert "$source_skin" -crop 220x180+0+0 +repage -resize "${corner_w}x${corner_h}!" "$tmp/tl.png"
  convert "$source_skin" -crop 1096x180+220+0 +repage -resize "${middle_w}x${corner_h}!" "$tmp/t.png"
  convert "$source_skin" -crop 220x180+1316+0 +repage -resize "${corner_w}x${corner_h}!" "$tmp/tr.png"
  convert "$source_skin" -crop 220x664+0+180 +repage -resize "${corner_w}x${middle_h}!" "$tmp/l.png"
  convert "$source_skin" -crop 1096x664+220+180 +repage -resize "${middle_w}x${middle_h}!" "$tmp/c.png"
  convert "$source_skin" -crop 220x664+1316+180 +repage -resize "${corner_w}x${middle_h}!" "$tmp/r.png"
  convert "$source_skin" -crop 220x180+0+844 +repage -resize "${corner_w}x${corner_h}!" "$tmp/bl.png"
  convert "$source_skin" -crop 1096x180+220+844 +repage -resize "${middle_w}x${corner_h}!" "$tmp/b.png"
  convert "$source_skin" -crop 220x180+1316+844 +repage -resize "${corner_w}x${corner_h}!" "$tmp/br.png"

  convert -size "${width}x${height}" xc:none \
    "$tmp/tl.png" -geometry +0+0 -composite \
    "$tmp/t.png" -geometry +${corner_w}+0 -composite \
    "$tmp/tr.png" -geometry +$((width-corner_w))+0 -composite \
    "$tmp/l.png" -geometry +0+${corner_h} -composite \
    "$tmp/c.png" -geometry +${corner_w}+${corner_h} -composite \
    "$tmp/r.png" -geometry +$((width-corner_w))+${corner_h} -composite \
    "$tmp/bl.png" -geometry +0+$((height-corner_h)) -composite \
    "$tmp/b.png" -geometry +${corner_w}+$((height-corner_h)) -composite \
    "$tmp/br.png" -geometry +$((width-corner_w))+$((height-corner_h)) -composite \
    -background "#aeb48b" -alpha remove \
    \( -size "${width}x${height}" xc:none -fill white \
       -draw "roundrectangle 2,2 $((width-3)),$((height-3)) 44,44" \) \
    -alpha off -compose CopyOpacity -composite -strip \
    "PNG32:$tmp/frame-output.png"
  mv "$tmp/frame-output.png" "$output"
}

green="#42523b"
olive="#667455"
sage="#98a780"
rose="#d9928e"
rose_dark="#a95f61"
cream="#fffaf0"
muted="#7a7569"

make_frame 600 780 "$out/botanical-calculator-skin.png"
make_frame 1100 726 "$out/botanical-currency-converter-skin.png"
make_frame 1100 654 "$out/botanical-system-info-skin.png"

cp "$out/botanical-calculator-skin.png" "$out/botanical-calculator-render.png"
convert "$out/botanical-calculator-render.png" \
  -fill "#f5ded8" -stroke "$olive" -strokewidth 3 -draw "roundrectangle 135,37 465,108 22,22" \
  -fill "$green" -stroke none -font DejaVu-Serif-Bold -pointsize 31 -gravity North -annotate +0+49 "BOTANICAL" \
  -font DejaVu-Sans -pointsize 16 -annotate +0+82 "CALCULATOR" \
  -fill "#fffdf8" -stroke "$olive" -strokewidth 3 -draw "roundrectangle 82,135 518,253 18,18" \
  -fill "$green" -stroke none -font DejaVu-Serif-Bold -pointsize 50 -gravity NorthEast -annotate +108+158 "0" \
  "$tmp/calc-base.png"

cp "$tmp/calc-base.png" "$tmp/calc-buttons.png"
calc_labels=("C" "+/-" "%" "÷" "7" "8" "9" "×" "4" "5" "6" "−" "1" "2" "3" "+" "0" "." "=")
index=0
for row in 0 1 2 3; do
  for col in 0 1 2 3; do
    x1=$((88 + col*108)); y1=$((279 + row*91)); x2=$((x1+82)); y2=$((y1+66))
    fill="#f5e5df"; [ "$col" -eq 3 ] && fill="#dca19d"
    label="${calc_labels[$index]}"; index=$((index+1))
    convert "$tmp/calc-buttons.png" -fill "$fill" -stroke "$olive" -strokewidth 2 \
      -draw "roundrectangle $x1,$y1 $x2,$y2 14,14" -fill "$green" -stroke none \
      -font DejaVu-Serif -pointsize 28 -gravity NorthWest -annotate +$((x1+28))+$((y1+17)) "$label" "$tmp/calc-buttons.png"
  done
done
for col in 0 1; do
  x1=$((88 + col*108)); y1=643; x2=$((x1+82)); y2=709
  label="${calc_labels[$index]}"; index=$((index+1))
  convert "$tmp/calc-buttons.png" -fill "#f5e5df" -stroke "$olive" -strokewidth 2 \
    -draw "roundrectangle $x1,$y1 $x2,$y2 14,14" -fill "$green" -stroke none \
    -font DejaVu-Serif -pointsize 28 -gravity NorthWest -annotate +$((x1+28))+$((y1+17)) "$label" "$tmp/calc-buttons.png"
done
convert "$tmp/calc-buttons.png" -fill "#dca19d" -stroke "$olive" -strokewidth 2 \
  -draw "roundrectangle 304,643 494,709 14,14" -fill "$green" -stroke none \
  -font DejaVu-Serif-Bold -pointsize 30 -gravity NorthWest -annotate +378+657 "=" \
  "$out/botanical-calculator-render.png"

convert "$out/botanical-currency-converter-skin.png" \
  -fill "#f5ded8" -stroke "$olive" -strokewidth 3 -draw "roundrectangle 240,28 860,112 24,24" \
  -fill "$green" -stroke none -font DejaVu-Serif-Bold -pointsize 37 -gravity North -annotate +0+42 "CURRENCY" \
  -font DejaVu-Sans -pointsize 17 -annotate +0+83 "CONVERTER" \
  -font DejaVu-Sans-Bold -pointsize 26 -gravity NorthWest -annotate +124+134 "AMOUNT" \
  -fill "#fffdf8" -stroke "$olive" -strokewidth 3 -draw "roundrectangle 100,174 1000,314 22,22" \
  -fill "$green" -stroke none -font DejaVu-Serif-Bold -pointsize 55 -annotate +145+208 "1.00" \
  -fill "$sage" -stroke "$olive" -strokewidth 2 -draw "roundrectangle 840,200 965,288 18,18" \
  -fill "$cream" -stroke none -font DejaVu-Serif-Bold -pointsize 29 -annotate +870+225 "CAD" \
  -fill "$rose_dark" -font DejaVu-Sans-Bold -pointsize 25 -gravity North -annotate +0+328 "CONVERTS TO" \
  -fill "$rose" -stroke none -draw "rectangle 155,365 945,368" \
  -fill "#fffdf8" -stroke "$olive" -strokewidth 3 -draw "roundrectangle 100,388 1000,528 22,22" \
  -fill "$green" -stroke none -font DejaVu-Serif-Bold -pointsize 55 -gravity NorthWest -annotate +145+421 "0.7194" \
  -fill "$sage" -stroke "$olive" -strokewidth 2 -draw "roundrectangle 840,414 965,502 18,18" \
  -fill "$cream" -stroke none -font DejaVu-Serif-Bold -pointsize 29 -annotate +870+439 "USD" \
  -fill "$green" -font DejaVu-Sans-Bold -pointsize 22 -gravity North -annotate +0+549 "1 CAD = 0.719448 USD" \
  -fill "$muted" -font DejaVu-Sans -pointsize 17 -annotate +0+583 "Rates by Exchange Rate API · 2026-09-20" \
  -fill "$rose_dark" -font DejaVu-Sans-Bold -pointsize 17 -annotate +0+635 "DOUBLE-CLICK TO CHANGE" \
  "$out/botanical-currency-converter-render.png"

convert "$out/botanical-system-info-skin.png" \
  -fill "#f5ded8" -stroke "$olive" -strokewidth 3 -draw "roundrectangle 210,28 890,108 24,24" \
  -fill "$green" -stroke none -font DejaVu-Serif-Bold -pointsize 36 -gravity North -annotate +0+47 "SYSTEM INFORMATION" \
  -fill "$olive" -stroke none -draw "rectangle 552,132 555,576" \
  -fill "$green" -font DejaVu-Sans-Bold -pointsize 25 -gravity NorthWest \
  -annotate +105+139 "CPU" -annotate +105+235 "MEMORY" -annotate +105+331 "SYSTEM DISK" -annotate +105+427 "SYSTEM" \
  -fill "$green" -font DejaVu-Sans-Bold -pointsize 24 -annotate +398+139 "8%" -annotate +338+235 "5.1 GB / 15.9 GB" -annotate +388+331 "53% used" \
  -fill "#e7ded0" -stroke "$olive" -strokewidth 2 \
  -draw "roundrectangle 105,181 500,199 9,9" -draw "roundrectangle 105,277 500,295 9,9" -draw "roundrectangle 105,373 500,391 9,9" \
  -fill "$rose" -stroke none -draw "roundrectangle 106,182 150,198 8,8" -draw "roundrectangle 106,278 276,294 8,8" -draw "roundrectangle 106,374 315,390 8,8" \
  -fill "$green" -font DejaVu-Sans-Bold -pointsize 21 -annotate +105+469 "Windows 10 IoT Enterprise LTSC" \
  -fill "$muted" -font DejaVu-Sans-Bold -pointsize 20 -annotate +105+506 "EMILYDESK-PC  ·  64-bit" \
  -fill "$green" -font DejaVu-Sans-Bold -pointsize 24 -annotate +600+139 "CPU TEMPERATURE" \
  -fill "$green" -font DejaVu-Serif-Bold -pointsize 63 -annotate +600+176 "36°C" \
  -fill "$muted" -font DejaVu-Sans -pointsize 19 -annotate +600+252 "Core Temp · Core #3" \
  -fill "$green" -font DejaVu-Sans-Bold -pointsize 24 -annotate +600+308 "NETWORK" \
  -fill "$muted" -font DejaVu-Sans -pointsize 20 -annotate +600+348 "DOWN" -annotate +600+389 "UP" \
  -fill "$green" -font DejaVu-Sans-Bold -pointsize 22 -annotate +733+346 "81.4 KB/s" -annotate +733+387 "3.6 KB/s" \
  -fill "$green" -font DejaVu-Sans-Bold -pointsize 24 -annotate +600+452 "UPTIME" \
  -fill "$green" -font DejaVu-Sans-Bold -pointsize 24 -annotate +600+494 "1d 9h 30m" \
  "$out/botanical-system-info-render.png"

convert "$out/botanical-calculator-render.png" -resize 270x351 "$out/calculator-preview.png"
convert "$out/botanical-currency-converter-render.png" -resize 460x304 "$out/currency-preview.png"
convert "$out/botanical-system-info-render.png" -resize 480x285 "$out/system-preview.png"
convert "$wallpaper" -resize 1920x1080^ -gravity center -extent 1920x1080 \
  -fill "rgba(255,250,240,0.84)" -stroke "$olive" -strokewidth 3 \
  -draw "roundrectangle 65,50 1855,1040 30,30" \
  -fill "$green" -stroke none -font DejaVu-Serif-Bold -pointsize 52 -gravity North -annotate +0+78 "BOTANICAL NATURE OPTIONAL WIDGETS" \
  -fill "$muted" -font DejaVu-Sans -pointsize 24 -annotate +0+144 "Transparent edges · compact layouts · every background editable" \
  -gravity NorthWest \
  "$out/calculator-preview.png" -geometry +130+275 -composite \
  "$out/currency-preview.png" -geometry +595+305 -composite \
  "$out/system-preview.png" -geometry +1240+315 -composite \
  -fill "$green" -font DejaVu-Sans-Bold -pointsize 28 -gravity NorthWest \
  -annotate +180+670 "Calculator" -annotate +702+670 "Currency Converter" -annotate +1340+670 "System Information" \
  -fill "$muted" -font DejaVu-Sans -pointsize 21 \
  -annotate +180+711 "270 × 351" -annotate +755+711 "460 × 304" -annotate +1384+711 "480 × 285" \
  -fill "$green" -font DejaVu-Sans-Bold -pointsize 23 -gravity North -annotate +0+860 "Right-click → Appearance → Theme" \
  -fill "$muted" -font DejaVu-Sans -pointsize 20 -annotate +0+900 "Switch between installed Steampunk, Industrial, Woodland Nature, and Botanical Nature versions" \
  -strip -quality 92 "$out/Botanical_Optional_Widgets_Preview_Revised.jpg"

sync -f "$out/Botanical_Optional_Widgets_Preview_Revised.jpg"
identify "$out/Botanical_Optional_Widgets_Preview_Revised.jpg" "$out"/*-skin.png "$out"/*-render.png "$out"/*-preview.png
