"""Stufen-Varianten der Gebaeude: Bronze, Silber, Gold (name_t1/_t2/_t3.png).
Je Stufe wird das Schild zur Metallplatte mit Nieten und auf dem Dach sitzt eine Zier (Bronze: Kugel, Silber: Kugel
mit Spitze, Gold: Stern). Wandelemente (Tuer- und Fensterrahmen, Ecksteine; Regeln in trim_rules.py, auf den
Originalbildern des City-Pakets erkannt) nehmen die Metallfarbe an, die Schattierung bleibt.
Nach signs.py ausfuehren. Aufruf: python metals.py <Buildings-Ordner> <Assets-Ordner>"""
import sys
from PIL import Image, ImageDraw
import signs
import colorsys
from trim_rules import RULES, mask

bdir, assets = sys.argv[1], sys.argv[2]
signs.FONT = f"{assets}/Resources/Fonts/CurseCasual.ttf"
PAD = 120  # Platz ueber dem Dach fuer die Zier

# Rahmen, Flaeche, Glanzlinie, Schrift, Schriftkontur
PALS = {
    1: ((84, 46, 22), (178, 110, 60), (232, 170, 110), (58, 30, 12), (240, 196, 150)),     # Bronze
    2: ((70, 76, 88), (182, 190, 204), (244, 248, 252), (40, 46, 58), (250, 252, 255)),    # Silber
    3: ((112, 74, 10), (226, 174, 52), (255, 234, 140), (86, 52, 8), (255, 244, 190)),     # Gold
}

seed = Image.open(f"{assets}/HappyHarvest/Art/Crops/Wheat/Sprites/Sprite_Wheat_seedbag.png").convert("RGBA")
seed = seed.crop(seed.getbbox())
JOBS = {   # Schild wie in signs.py, Dachspitze (x, y) im Bild ohne Polster
    "haus":       ((212, 268, 150, 44, "HOF", None, False), (212, 4)),
    "huette_a":   ((144, 238, 220, 46, "KNOLLIS", signs.knolli_icon(), True), (144, 6)),
    "huette_b":   ((141, 232, 220, 46, "KNOLLIS", signs.knolli_icon(), True), (141, 6)),
    "muehle":     ((230, 403, 270, 50, "SAATGUT", seed, False), (318, 6)),
    "turm_holz":  ((160, 285, 220, 60, "WACHE", signs.shield_icon(), False), (160, 4)),
    "turm_stein": ((160, 285, 220, 60, "WACHE", signs.shield_icon(), False), (160, 4)),
}


def rivets(d, x0, y0, x1, y1, pal):
    frame, _, hi, _, _ = pal
    for x, y in ((x0 + 9, y0 + 9), (x1 - 9, y0 + 9), (x0 + 9, y1 - 9), (x1 - 9, y1 - 9)):
        d.ellipse([x - 4, y - 4, x + 4, y + 4], fill=hi, outline=frame, width=2)


def finial(im, x, y, tier, pal):
    """Zier auf der Dachspitze: Stange, darauf Kugel (Bronze), Kugel mit Spitze (Silber) oder Stern (Gold)."""
    frame, fill, hi, _, _ = pal
    d = ImageDraw.Draw(im)
    top = y - 50
    d.line([(x, y + 6), (x, top)], fill=frame, width=11)
    d.line([(x, y + 6), (x, top)], fill=fill, width=5)
    if tier == 3:
        import math
        r1, r2, cy = 40, 17, top - 30
        pts = []
        for i in range(10):
            a = -math.pi / 2 + i * math.pi / 5
            r = r1 if i % 2 == 0 else r2
            pts.append((x + r * math.cos(a), cy + r * math.sin(a)))
        d.polygon(pts, fill=frame)
        inner = [(x + (px - x) * 0.78, cy + (py - cy) * 0.78) for px, py in pts]
        d.polygon(inner, fill=fill)
        d.ellipse([x - 9, cy - 16, x + 2, cy - 5], fill=hi)
    else:
        r = 20
        cy = top - r + 2
        if tier == 2:
            d.polygon([(x - 9, cy - r + 3), (x + 9, cy - r + 3), (x, cy - r - 36)], fill=frame)
            d.polygon([(x - 4, cy - r + 2), (x + 4, cy - r + 2), (x, cy - r - 28)], fill=hi)
        d.ellipse([x - r, cy - r, x + r, cy + r], fill=frame)
        d.ellipse([x - r + 5, cy - r + 5, x + r - 5, cy + r - 5], fill=fill)
        d.ellipse([x - 9, cy - 11, x, cy - 2], fill=hi)


# Grundfarbe des Metalls je Stufe (die Schattierung kommt aus der Helligkeit des Bildes)
METAL = {1: (196, 124, 66), 2: (198, 205, 218), 3: (236, 184, 58)}
city = f"{assets}/City Assets Pack - Basic"
TRIMS = {}
for _name, (_src, _wall, _rule) in RULES.items():
    _skip = (lambda x, y: 430 <= y <= 540) if _name == "laden" else None   # Markise bleibt Stoff
    TRIMS[_name] = mask(Image.open(f"{city}/{_src}").convert("RGBA"), _wall, _rule, _skip)


def tower_trim(im):
    """Wachturm: Querbalken und Streben unter dem Dach (Holzpixel in diesem Band)."""
    m = set(); px = im.load(); W, H = im.size
    for y in range(172, 218):
        for x in range(W):
            r, g, b, a = px[x, y]
            if a < 200: continue
            h, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            if 10 <= h * 360 < 50 and s > 0.2: m.add((x, y))
    return m


def metallize(im, pts, tier, dy=0):
    """Pixel in Metall: Grundfarbe mal Helligkeit (Schattierung bleibt), Glanz auf hellen Stellen."""
    base = METAL[tier]; px = im.load()
    for (x, y) in pts:
        r, g, b, a = px[x, y + dy]
        if a == 0: continue
        v = max(r, g, b) / 255
        k = 0.45 + 0.9 * v
        hi = max(0.0, v - 0.75) * 1.6   # Glanzlicht
        c = tuple(int(min(255, base[i] * k + 255 * hi)) for i in range(3))
        px[x, y + dy] = c + (a,)


def padded(im):
    out = Image.new("RGBA", (im.size[0], im.size[1] + PAD), (0, 0, 0, 0))
    out.alpha_composite(im, (0, PAD))
    return out


for tier, pal in PALS.items():
    frame, fill, hi, txt, ink = pal
    for name, (sign, (rx, ry)) in JOBS.items():
        src = Image.open(f"{bdir}/{name}.png").convert("RGBA")
        trims = TRIMS.get(name) or (tower_trim(src) if name.startswith("turm") else set())
        im = padded(src)
        metallize(im, trims, tier, PAD)
        cx, cy, w, h = sign[0], sign[1] + PAD, sign[2], sign[3]
        signs.board(im, cx, cy, w, h, *sign[4:], pal=pal)
        rivets(ImageDraw.Draw(im), cx - w // 2, cy - h // 2, cx + w // 2, cy + h // 2, pal)
        finial(im, rx, ry + PAD, tier, pal)
        im.save(f"{bdir}/{name}_t{tier}.png")

    # Laden: Metallrahmen um die Werbetafel, Schriftzug in Metall, Ring ums runde Schild, Zier auf dem Dach
    im = padded(Image.open(f"{bdir}/laden.png").convert("RGBA"))
    metallize(im, TRIMS["laden"], tier, PAD)
    d = ImageDraw.Draw(im)
    x0, y0, x1, y1 = 72, 236 + PAD, 408, 372 + PAD
    d.rectangle([x0, y0, x1, y1], outline=frame, width=12)
    d.rectangle([x0 + 3, y0 + 3, x1 - 3, y1 - 3], outline=fill, width=6)
    d.line([(x0 + 6, y0 + 5), (x1 - 6, y0 + 5)], fill=hi, width=2)
    rivets(d, x0, y0, x1, y1, pal)
    d.text((240, 305 + PAD), "LADEN", font=signs.font(78), fill=fill, anchor="mm", stroke_width=4, stroke_fill=frame)
    d.ellipse([520 - 50, 390 + PAD - 50, 520 + 50, 390 + PAD + 50], outline=frame, width=9)
    d.ellipse([520 - 47, 390 + PAD - 47, 520 + 47, 390 + PAD + 47], outline=fill, width=4)
    finial(im, 240, 18 + PAD, tier, pal)
    im.save(f"{bdir}/laden_t{tier}.png")
    print("Stufe", tier, "fertig")
