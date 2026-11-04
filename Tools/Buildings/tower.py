import sys
from PIL import Image, ImageDraw
out = sys.argv[1]
W, H = 320, 660
img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
ink = (74, 44, 26, 255); wood = (160, 102, 56, 255); woodHi = (190, 128, 74, 255)
legTop, ground = 330, H - 14
# Schatten
d.ellipse([40, ground - 14, W - 40, ground + 10], fill=(0, 0, 0, 60))
def beam(p0, p1, w):
    d.line([p0, p1], fill=ink, width=w + 8)
    d.line([p0, p1], fill=wood, width=w)
# Streben (X) und Riegel zwischen den Beinen
xl, xr = 92, W - 92
beam((xl, legTop + 30), (xr, ground - 40), 14)
beam((xr, legTop + 30), (xl, ground - 40), 14)
beam((xl, legTop + 150), (xr, legTop + 150), 14)
# Beine (leicht gespreizt)
for x0, x1 in [(xl, xl - 22), (xr, xr + 22)]:
    d.polygon([(x0 - 13, legTop), (x0 + 13, legTop), (x1 + 13, ground), (x1 - 13, ground)], fill=ink)
    d.polygon([(x0 - 8, legTop), (x0 + 8, legTop), (x1 + 8, ground - 4), (x1 - 8, ground - 4)], fill=wood)
    d.line([(x0 - 3, legTop + 4), (x1 - 3, ground - 8)], fill=woodHi, width=3)
# Kanzel: Bretterwand mit Verstrebung
cab = Image.open("deco_12.png").convert("RGBA")
cw = 250; ch = int(cab.size[1] * cw / cab.size[0])
cab = cab.resize((cw, ch), Image.LANCZOS)
cabTop = legTop - ch + 40
img.alpha_composite(cab, ((W - cw) // 2, cabTop))
# Dach: oberer Teil von building_08 (Dach mit Giebel)
b8 = Image.open("building_08.png").convert("RGBA")
roof = b8.crop((0, 0, b8.size[0], 268))
rw = W; rh = int(roof.size[1] * rw / roof.size[0])
roof = roof.resize((rw, rh), Image.LANCZOS)
img.alpha_composite(roof, (0, cabTop - rh + 46))
bb = img.getbbox(); img = img.crop((0, bb[1], W, H))
img.save(out)
print(img.size)

# Dach in das Blau des Wohnhauses, alles etwas dunkler (wie das Haus)
import colorsys
px = img.load()
for y in range(img.size[1]):
    for x in range(img.size[0]):
        r, g, b, a = px[x, y]
        if a == 0: continue
        h, s, v = colorsys.rgb_to_hsv(r/255, g/255, b/255); deg = h * 360
        if 70 <= deg < 190 and s > 0.08:            # gruenes Blechdach -> Hausblau
            h, s, v = 0.6, min(1, 0.55 + s * 0.3), v * 0.82
        elif y < 178 and 5 <= deg < 60 and s > 0.15 and v > 0.25:   # Rostflecken auf dem Dach -> dunkleres Blau
            h, s, v = 0.6, 0.6, v * 0.62
        nr = colorsys.hsv_to_rgb(h, min(1, s * 1.1), v * 0.68)
        px[x, y] = (int(nr[0]*255), int(nr[1]*255), int(nr[2]*255), a)
img.save(out)
