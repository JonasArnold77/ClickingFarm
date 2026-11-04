"""Steinturm aus dem Holzturm: nur Beine und Streben (unter der Kanzel) werden grau.
Aufruf: python stone_tower.py <Buildings-Ordner>"""
import sys, colorsys
from PIL import Image
bdir = sys.argv[1]
im = Image.open(f"{bdir}/turm_holz.png").convert("RGBA"); px = im.load()
W, H = im.size
start = 330 - (660 - H) + 44   # unter der Kanzel
for y in range(start, H):
    for x in range(W):
        r, g, b, a = px[x, y]
        if a == 0: continue
        h, s, v = colorsys.rgb_to_hsv(r/255, g/255, b/255); deg = h*360
        if 10 <= deg < 50 and s > 0.15:
            nr = colorsys.hsv_to_rgb(0.1, 0.08, v * 0.95)
            px[x, y] = (int(nr[0]*255), int(nr[1]*255), int(nr[2]*255), a)
im.save(f"{bdir}/turm_stein.png")
