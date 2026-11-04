"""Gemeinsame Palette fuer alle Gebaeude: Dach ins Hausblau, blaue Waende zu hellem Putz, alles dunkler und satter.
Aufruf: python recolor_all.py <City-Ordner> <Ziel-Ordner>"""
import sys, colorsys
from PIL import Image

src_dir, out_dir = sys.argv[1], sys.argv[2]
BLUE, DARK = 0.6, 0.66


def hsv(c): return colorsys.rgb_to_hsv(c[0] / 255, c[1] / 255, c[2] / 255)
def rgb(h, s, v):
    r, g, b = colorsys.hsv_to_rgb(h % 1, max(0, min(1, s)), max(0, min(1, v)))
    return (int(r * 255), int(g * 255), int(b * 255))


# roof: Dachbereich (Anteil der Bildhoehe von oben); patches: Flecken auf dem Dach bis zu diesem Anteil;
# roofHues: Farbtoene des Dachs (Grad); wallsBlue: blaue Waende zu Putz
JOBS = {
    "haus":     dict(src="building_08.png", roof=0.6, patches=0.56, roofHues=[(70, 190)], wallsBlue=False),
    "muehle":   dict(src="building_05.png", roof=0.6, patches=0.0, roofHues=[(0, 50), (340, 360)], wallsBlue=False),
    "huette_a": dict(src="building_06.png", roof=0.5, patches=0.0, roofHues=[(0, 20), (330, 360)], wallsBlue=False),
    "huette_b": dict(src="building_07.png", roof=0.54, patches=0.0, roofHues=[(15, 55)], wallsBlue=True),
}

for name, j in JOBS.items():
    im = Image.open(f"{src_dir}/{j['src']}").convert("RGBA")
    W, H = im.size
    px = im.load()
    for y in range(H):
        fy = y / H
        for x in range(W):
            r, g, b, a = px[x, y]
            if a == 0: continue
            h, s, v = hsv((r, g, b)); deg = h * 360
            nh, ns, nv = h, s, v
            inRoofHue = any(lo <= deg < hi for lo, hi in j["roofHues"])
            if v < 0.3:
                nh, ns = 0.07, 0.55                                  # Konturen dunkelbraun
            elif fy < j["roof"] and inRoofHue and s > 0.08:
                nh, ns, nv = BLUE, 0.55 + s * 0.3, v * 0.82          # Dach -> Hausblau
            elif fy < j["patches"] and 5 <= deg < 60 and s > 0.15:
                nh, ns, nv = BLUE, 0.6, v * 0.62                     # Rostflecken auf dem Dach
            elif j["wallsBlue"] and 185 <= deg < 235 and s > 0.15 and v < 0.75 and fy >= j["roof"]:
                nh, ns, nv = 0.08, 0.08, 0.78 + v * 0.22             # blaue Waende -> heller Putz
            out = rgb(nh, ns * 1.1, nv * DARK)
            px[x, y] = out + (a,)
    im.save(f"{out_dir}/{name}.png")
    print(name, im.size)
