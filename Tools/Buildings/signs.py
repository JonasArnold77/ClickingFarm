"""Schilder in die Gebaeudebilder malen, damit man ihren Zweck erkennt (nach dem Umfaerben ausfuehren).
Aufruf: python signs.py <Buildings-Ordner> <Assets-Ordner>"""
import sys
from PIL import Image, ImageDraw, ImageFont

bdir, assets = sys.argv[1], sys.argv[2]
FONT = f"{assets}/Resources/Fonts/CurseCasual.ttf"
WOOD, WOOD_HI, FRAME = (104, 66, 38), (128, 84, 50), (46, 29, 18)
CREAM, INK = (242, 228, 192), (40, 25, 15)


def font(px): return ImageFont.truetype(FONT, px)


def board(im, cx, cy, w, h, text, icon=None, hang=False):
    """Holzschild mit Rahmen, Text in Creme mit dunkler Kontur, optional Bild links vom Text."""
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = cx - w // 2, cy - h // 2, cx + w // 2, cy + h // 2
    if hang:   # zwei Ketten nach oben
        for hx in (x0 + 18, x1 - 18):
            d.line([(hx, y0 - 16), (hx, y0)], fill=FRAME, width=4)
    d.rounded_rectangle([x0, y0, x1, y1], radius=8, fill=FRAME)
    d.rounded_rectangle([x0 + 4, y0 + 4, x1 - 4, y1 - 4], radius=6, fill=WOOD)
    d.line([(x0 + 8, y0 + 8), (x1 - 8, y0 + 8)], fill=WOOD_HI, width=2)
    size = int(h * 0.62)
    f = font(size)
    tw = d.textlength(text, font=f)
    iw = 0
    if icon is not None:
        ih = int(h * 0.78)
        ic = icon.resize((int(icon.size[0] * ih / icon.size[1]), ih), Image.LANCZOS)
        iw = ic.size[0] + 6
    while tw + iw > w - 16 and size > 10:
        size -= 1; f = font(size); tw = d.textlength(text, font=f)
    tx = cx - (tw + iw) / 2 + iw
    if icon is not None:
        im.alpha_composite(ic, (int(cx - (tw + iw) / 2), int(cy - ic.size[1] / 2)))
    d.text((tx, cy), text, font=f, fill=CREAM, anchor="lm", stroke_width=max(2, size // 9), stroke_fill=INK)


def knolli_icon(s=64):
    """Kleiner Knolli: braune Knolle mit Augen und Blatt."""
    im = Image.new("RGBA", (s, s), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.ellipse([6, 14, s - 6, s - 4], fill=INK)
    d.ellipse([10, 18, s - 10, s - 8], fill=(176, 122, 70))
    for ex in (s * 0.38, s * 0.62):
        d.ellipse([ex - 6, s * 0.48 - 7, ex + 6, s * 0.48 + 7], fill=(250, 245, 235))
        d.ellipse([ex - 3, s * 0.5 - 3, ex + 3, s * 0.5 + 4], fill=INK)
    d.polygon([(s * 0.5, 16), (s * 0.35, 2), (s * 0.55, 6)], fill=(90, 150, 60), outline=INK)
    return im


def shield_icon(s=64):
    im = Image.new("RGBA", (s, s), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    pts = [(s * 0.15, s * 0.1), (s * 0.85, s * 0.1), (s * 0.85, s * 0.5), (s * 0.5, s * 0.95), (s * 0.15, s * 0.5)]
    d.polygon(pts, fill=INK)
    inner = [(s * 0.22, s * 0.17), (s * 0.78, s * 0.17), (s * 0.78, s * 0.48), (s * 0.5, s * 0.85), (s * 0.22, s * 0.48)]
    d.polygon(inner, fill=(40, 70, 140))
    d.line([(s * 0.5, s * 0.2), (s * 0.5, s * 0.82)], fill=(220, 190, 90), width=5)
    d.line([(s * 0.26, s * 0.4), (s * 0.74, s * 0.4)], fill=(220, 190, 90), width=5)
    return im


coin = Image.open(f"{assets}/HappyHarvest/Art/UI/Sprite_coin_icon.png").convert("RGBA")
seed = Image.open(f"{assets}/HappyHarvest/Art/Crops/Wheat/Sprites/Sprite_Wheat_seedbag.png").convert("RGBA")
seed = seed.crop(seed.getbbox())

# Laden: Schriftzug in die leere Werbetafel, Muenze ins runde Schild
im = Image.open(f"{bdir}/laden.png").convert("RGBA"); d = ImageDraw.Draw(im)
f = font(78)
d.text((240, 305), "LADEN", font=f, fill=(70, 44, 24), anchor="mm", stroke_width=3, stroke_fill=(230, 210, 170))
for cx in (110, 370):
    c = coin.resize((54, 51), Image.LANCZOS); im.alpha_composite(c, (cx - 27, 280))
c = coin.resize((62, 59), Image.LANCZOS); im.alpha_composite(c, (520 - 31, 390 - 30))
im.save(f"{bdir}/laden.png")

board_jobs = [
    ("haus", (212, 268, 150, 44, "HOF", None, False)),
    ("huette_a", (144, 238, 220, 46, "KNOLLIS", knolli_icon(), True)),
    ("huette_b", (141, 232, 220, 46, "KNOLLIS", knolli_icon(), True)),
    ("muehle", (230, 403, 270, 50, "SAATGUT", seed, False)),
    ("turm_holz", (160, 285, 220, 60, "WACHE", shield_icon(), False)),
    ("turm_stein", (160, 285, 220, 60, "WACHE", shield_icon(), False)),
]
for name, args in board_jobs:
    im = Image.open(f"{bdir}/{name}.png").convert("RGBA")
    board(im, *args)
    im.save(f"{bdir}/{name}.png")
    print("Schild:", name)
