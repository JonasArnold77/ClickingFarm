import colorsys
# Regeln fuer Wandelemente (Rahmen, Ecksteine) je Gebaeude, ausgewertet auf dem ORIGINAL-Bild des City-Pakets.
# wall: ab welchem Anteil der Hoehe die Wand beginnt (darueber Dach).
def hsv(r, g, b): return colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
RULES = {
    "haus":     ("building_08.png", 0.6,  lambda h, s, v: 25 <= h < 60 and s < 0.6 and v > 0.62),
    "huette_a": ("building_06.png", 0.52, lambda h, s, v: (h < 20 or h > 330) and 0.12 < s < 0.45 and v > 0.4),
    "huette_b": ("building_07.png", 0.55, lambda h, s, v: s < 0.15 and v > 0.55),
    "muehle":   ("building_05.png", 0.4,  lambda h, s, v: 40 <= h < 170 and s < 0.22 and v > 0.62),
    "laden":    ("building_04.png", 0.33, lambda h, s, v: 40 <= h < 170 and s < 0.3 and v > 0.5),   # Markise: skip in metals.py
}
def glassy(h, s, v): return 120 <= h < 210 and 0.06 < s < 0.45 and 0.45 < v < 0.9

def mask(img, wall, rule, skip=None):
    """Wandelemente: Regel trifft, nicht in einer Glasflaeche (viele glasige Nachbarn) und nicht in skip."""
    W, H = img.size; px = img.load(); m = set()
    gl = [[False] * W for _ in range(H)]
    for y in range(H):
        for x in range(W):
            r, g, b, a = px[x, y]
            if a > 200: gl[y][x] = glassy(*[c * 360 if i == 0 else c for i, c in enumerate(hsv(r, g, b))])
    # Summenflaeche fuer schnelle Nachbar-Zaehlung (7x7)
    S = [[0] * (W + 1) for _ in range(H + 1)]
    for y in range(H):
        row = 0
        for x in range(W):
            row += gl[y][x]; S[y + 1][x + 1] = S[y][x + 1] + row
    def near(x, y, r=4):
        x0, y0, x1, y1 = max(0, x - r), max(0, y - r), min(W, x + r + 1), min(H, y + r + 1)
        return S[y1][x1] - S[y0][x1] - S[y1][x0] + S[y0][x0]
    for y in range(int(H * wall), H):
        for x in range(W):
            r, g, b, a = px[x, y]
            if a < 200 or (skip and skip(x, y)): continue
            h, s, v = hsv(r, g, b)
            if rule(h * 360, s, v) and near(x, y) < 34: m.add((x, y))
    return m
