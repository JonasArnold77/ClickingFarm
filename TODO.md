# TODO.md – offene Punkte

## Als Nächstes
- [ ] **Playtest und Balancing**: Werte in `balance.json` sind geschätzt. Besonders prüfen: Tempo der ersten 10 Minuten, Kosten Kupfer-Werkzeuge, XP-Kurve ab Stufe 15, Wert von Erdbeere vs. Tomate.
- [ ] Auf echtem Android-Gerät testen: Touch-Gefühl, Wischen vs. Kamera verschieben, Vibration, Safe Area, Performance.
- [ ] Spieler-Einstellungen: Android-Build auf Hochformat fixieren (Player Settings → Resolution and Presentation → Default Orientation: Portrait). Zur Laufzeit wird Portrait bereits gesetzt.
- [ ] Zaun-Grafik an den Grenzen gesperrter Bereiche (aktuell: abgedunkelt + Schild + Preis-Label).

## Später (laut CLAUDE.md nicht Teil der Basisversion)
- [ ] Echte Werbe-SDK-Anbindung in `AdService` (Rewarded + seltene Interstitials).
- [ ] WebGL-Build und Spieleportal-Anbindung.
- [ ] Englische Sprachdatei `Lang/en.txt` + Sprachwahl in den Einstellungen.
- [ ] Echte Grafik (über `Resources/Sprites/…` austauschbar) und Musik.
- [ ] Komposter, Truhe, Zaun, Wege, Saatgutmaschine (Gegenstände aus CLAUDE.md 5.4, noch nicht gebaut).
- [ ] Jahreszeiten, Tiere, Dorf, Angeln (am Teich), Bergbau.

## Bekannte Einschränkungen
- Der Spielstand hat noch keine Versions-Migration (Feld `version` ist vorbereitet).
- Fenster werden bei jeder Aktion neu aufgebaut; bei sehr langen Listen evtl. optimieren.
