"""Create compact optional-widget preview skins from approved Ember Glow art."""

from pathlib import Path
from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Themes/EmberGlow/ember-glow-calendar-skin.png"
OUTPUT = Path(__file__).resolve().parent


def _paste(canvas, source, source_box, destination_box):
    width = destination_box[2] - destination_box[0]
    height = destination_box[3] - destination_box[1]
    piece = source.crop(source_box).resize(
        (width, height), Image.Resampling.LANCZOS
    )
    canvas.alpha_composite(piece, destination_box[:2])


def generate(filename, width, height):
    source = Image.open(SOURCE).convert("RGBA")
    canvas = Image.new("RGBA", (width, height))
    source_left, source_right = 165, 1335
    source_top, source_bottom = 165, 1085
    side = 32
    top = 36
    bottom = 36

    # Corners
    _paste(canvas, source, (0, 0, source_left, source_top),
           (0, 0, side, top))
    _paste(canvas, source, (source_right, 0, source.width, source_top),
           (width - side, 0, width, top))
    _paste(canvas, source, (0, source_bottom, source_left, source.height),
           (0, height - bottom, side, height))
    _paste(canvas, source,
           (source_right, source_bottom, source.width, source.height),
           (width - side, height - bottom, width, height))

    # Rails and center panel. Adjacent slices overlap by one pixel to prevent
    # transparent seams at non-integer Windows display scales.
    _paste(canvas, source, (source_left, 0, source_right, source_top),
           (side - 1, 0, width - side + 1, top))
    _paste(canvas, source,
           (source_left, source_bottom, source_right, source.height),
           (side - 1, height - bottom, width - side + 1, height))
    _paste(canvas, source, (0, source_top, source_left, source_bottom),
           (0, top - 1, side, height - bottom + 1))
    _paste(canvas, source,
           (source_right, source_top, source.width, source_bottom),
           (width - side, top - 1, width, height - bottom + 1))
    _paste(canvas, source,
           (source_left, source_top, source_right, source_bottom),
           (side - 1, top - 1, width - side + 1,
            height - bottom + 1))
    canvas.save(OUTPUT / filename, optimize=True)


if __name__ == "__main__":
    generate("ember-glow-calculator-skin.png", 300, 420)
    generate("ember-glow-currency-converter-skin.png", 550, 363)
    generate("ember-glow-system-info-skin.png", 550, 327)
