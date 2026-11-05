# Gebäudebilder erzeugen

Quelle: `Assets/City Assets Pack - Basic` (nicht im Repo), Ergebnis: `Assets/Resources/Buildings`.
Braucht Python mit Pillow. Reihenfolge (die Schilder kommen zuletzt, Umfärben überschreibt sie):

```
CITY="Assets/City Assets Pack - Basic"; OUT="Assets/Resources/Buildings"
python Tools/Buildings/recolor_all.py "$CITY" "$OUT"                       # Haus, Hütten, Mühle
python Tools/Buildings/recolor_shop.py "$CITY/building_04.png" "$OUT/laden.png" 0.64
(cd "$CITY" && python ../../Tools/Buildings/tower.py "../Resources/Buildings/turm_holz.png")
python Tools/Buildings/stone_tower.py "$OUT"
python Tools/Buildings/signs.py "$OUT" Assets                               # Schilder: HOF, LADEN, KNOLLIS, SAATGUT, WACHE
(cd Tools/Buildings && python metals.py "../../$OUT" ../../Assets)        # Stufen Bronze/Silber/Gold: *_t1/_t2/_t3.png
```
