import sys, colorsys
from PIL import Image
src, out = sys.argv[1], sys.argv[2]
im = Image.open(src).convert("RGBA"); W, H = im.size
px = im.load()
def hsv(c): return colorsys.rgb_to_hsv(c[0]/255, c[1]/255, c[2]/255)
def rgb(h, s, v): r, g, b = colorsys.hsv_to_rgb(h % 1, max(0, min(1, s)), max(0, min(1, v))); return (int(r*255), int(g*255), int(b*255))
BLUE = 0.6       # Dachblau des Wohnhauses (~215°)
OCHRE = 0.11     # Ockerholz der Tuerrahmen
DARK = float(sys.argv[3]) if len(sys.argv) > 3 else 0.74
for y in range(H):
    for x in range(W):
        r, g, b, a = px[x, y]
        if a == 0: continue
        h, s, v = hsv((r, g, b))
        deg = h * 360
        awning = 440 <= y <= 535 and x < 470
        aircon = x < 210 and y < 120
        if v < 0.3:                                   # Konturen: dunkles Holzbraun statt Marineblau
            nr = rgb(0.07, 0.55, v * 1.05)
        elif (deg < 18 or deg > 340) and s < 0.55:    # altrosa Dach und Gesimse -> Hausblau
            nr = rgb(BLUE, 0.55 + s * 0.3, v * 0.82)
        elif 5 <= deg < 35 and s >= 0.55:             # kraeftiges Orange: Markise blau, Schildrahmen Ocker
            nr = rgb(BLUE, 0.62, v * 0.85) if awning else rgb(OCHRE, 0.6, v * 0.95)
        elif 18 <= deg < 45 and s < 0.55 and v > 0.75: # Pfirsich (Schildflaeche, Markisenrand) -> Creme
            nr = rgb(0.09, 0.12, min(1, v * 1.02))
        elif 185 <= deg < 235 and s > 0.15 and v < 0.75:  # blaue Ziegelwand -> heller Putz
            nr = rgb(0.08, 0.08, 0.78 + v * 0.22)
        elif 50 <= deg < 150 and s < 0.3 and v > 0.45 and not aircon:   # salbeigruene Rahmen und Ecksteine -> helles Holz
            nr = rgb(0.09, 0.38, v * 0.92)
        else:
            nr = (r, g, b)
        # insgesamt dunkler und etwas satter, wie das Wohnhaus
        hh, ss, vv = hsv(nr)
        nr = rgb(hh, min(1, ss * 1.12), vv * DARK)
        px[x, y] = nr + (a,)
im.save(out)
