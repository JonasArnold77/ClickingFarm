# CLAUDE.md – Farm-Clicker (Arbeitstitel: Seed Clicker)

Dieses Dokument beschreibt, **was** gebaut werden soll und **wie es sich anfühlen muss**. Es ist bewusst nicht technisch. Technische Entscheidungen triffst du selbst, solange sie zu den Zielen hier passen. Wenn etwas unklar ist, triff eine sinnvolle Annahme, notiere sie kurz in `DECISIONS.md` und arbeite weiter.

---

## 1. Die Vision in einem Satz

Ein gemütliches Farmspiel im Stil von „Stardew Valley“, bei dem **jede Aktion ein Klick bzw. Tipp** ist und der Spieler permanent das Gefühl hat, **voranzukommen**: neue Pflanzen, bessere Werkzeuge, eine Farm, die sichtbar wächst.

Stardew Valley ist nur die Inspiration für Stimmung und Genre. Das Spiel bekommt einen eigenen Stil, eigene Namen und eigene Grafiken. Nichts davon wird kopiert.

## 2. Plattformen und Rahmen

- Engine: **Unity** mit **Universal Render Pipeline (2D)**
- Hauptplattform: **Android (Hochformat)**, später zusätzlich **Browser (WebGL)** über Spieleportale
- Finanzierung: **Werbung**, vor allem freiwillige Belohnungsvideos (siehe Abschnitt 9)
- Bedienung: komplett mit einem Finger spielbar, auch auf kleinen Handys
- Grafik: vorerst **Platzhalter** (einfache farbige Formen oder freie Pixel-Art). Die Spiellogik hat Vorrang vor der Optik.

## 3. Das wichtigste Designziel: das Fortschrittsgefühl

Alles im Spiel dient diesem einen Ziel. Der Spieler soll nie das Gefühl haben, festzustecken, und immer schon das nächste Ziel vor Augen sehen.

**Grundregeln für den Fortschritt:**

1. **Immer ein nächstes Ziel sichtbar.** Auf dem Bildschirm steht jederzeit, was als Nächstes freigeschaltet wird und wie weit der Spieler davon entfernt ist (z. B. „Noch 120 XP bis zur Tomate 🍅“).
2. **Häufige kleine Belohnungen, seltene große.** In den ersten 10 Minuten etwa jede Minute etwas Neues. Danach werden die Abstände langsam größer, aber nie länger als ein paar Minuten ohne irgendeinen Fortschritt.
3. **Jede Freischaltung wird gefeiert.** Kurze Einblendung, Konfetti- oder Glitzereffekt, Sound, die neue Sache wird groß gezeigt. Freischaltungen dürfen sich nie beiläufig anfühlen.
4. **Spürbare Sprünge.** Ein neues Werkzeug oder eine neue Pflanze muss sich deutlich besser anfühlen als das Vorherige, etwa doppelt so schnell oder mehrfacher Ertrag, nicht nur ein paar Prozent.
5. **Die Farm verändert sich sichtbar.** Der Spieler beginnt auf einem verwilderten Stück Land und baut daraus über die Zeit einen blühenden Hof. Ein Vorher-nachher-Gefühl ist ausdrücklich gewünscht.
6. **Zahlen dürfen groß werden.** Erträge und Münzen wachsen im Lauf des Spiels um viele Größenordnungen. Große Zahlen werden lesbar abgekürzt (1,2K, 3,4M usw.).

## 4. Die Kern-Spielschleife

1. Land **freiräumen** (Gras, Steine, Äste, Baumstümpfe wegklicken)
2. Boden **umhacken**
3. **Säen**
4. **Gießen**
5. Pflanze per Klicks **wachsen lassen** (jeder Klick lässt sie ein Stück wachsen)
6. **Ernten** und **verkaufen**
7. Mit dem Geld **Samen, Werkzeuge, Gegenstände und Erweiterungen** kaufen
8. Damit bessere Pflanzen schneller anbauen und von vorn beginnen, nur größer

Jeder einzelne Schritt muss sich gut anfühlen: kurzes Wackeln oder Hüpfen beim Antippen, kleine Partikel, eine schwebende Zahl („+3“), ein Sound. Das Klicken selbst muss Spaß machen.

## 5. Spielsysteme der Basisversion

### 5.1 Die Farm
- Die Farm besteht aus einem Raster von Feldern (Kacheln), auf das man von oben schaut.
- Am Anfang ist nur ein **kleiner Bereich** nutzbar (etwa 4×4 Felder), der Rest ist verwildert oder noch nicht gekauft.
- Hindernisse brauchen unterschiedlich viele Klicks: Gras 1, Ast 2, Stein 5, Baumstumpf 10. Bessere Werkzeuge verkürzen das.
- Aufräumen bringt Material (Holz, Stein, Fasern), das man für Bauprojekte braucht.

### 5.2 Pflanzen
- Jede Pflanze hat: Samenpreis, benötigtes Wachstum (wie viel Klickarbeit), Verkaufswert, XP beim Ernten und die Spielerstufe, ab der sie freigeschaltet wird.
- Pflanzen zeigen ihren Wachstumsfortschritt in mehreren sichtbaren Stufen (Samen → Keimling → junge Pflanze → erntereif).
- Erntereife Pflanzen fallen sofort auf (Glitzern, leichtes Wippen).
- Ungegossene Pflanzen wachsen deutlich langsamer.
- Mehrjährige Pflanzen (z. B. Beerensträucher, später Obstbäume) wachsen einmal und tragen danach immer wieder. Das ist eine eigene, besondere Freischaltung.
- Beispiel-Abfolge (Namen und Werte dürfen angepasst werden):

| Stufe | Pflanze | Charakter |
|---|---|---|
| 1 | Kresse | sofort verfügbar, sehr schnell, wenig Ertrag |
| 2 | Radieschen | schnell |
| 4 | Salat | solide |
| 6 | Karotte | erste „richtige“ Pflanze |
| 8 | Erdbeere | mehrjährig, trägt mehrmals |
| 10 | Tomate | großer Ertragssprung |
| 13 | Mais | |
| 15 | Sonnenblume | |
| 18 | Kürbis | langsam, sehr wertvoll |
| 21 | Apfelbaum | mehrjährig, Obstwiese nötig |
| 25 | Melone | |
| 30 | Seltene/magische Pflanzen | Endgame-Ziele, z. B. Kristallblume, Goldener Baum |

### 5.3 Werkzeuge
- Hacke, Gießkanne, Axt, Spitzhacke und Sichel.
- Jedes Werkzeug hat Ausbaustufen, z. B. **Holz → Kupfer → Eisen → Gold → Kristall**.
- Eine höhere Stufe bedeutet: weniger Klicks pro Aktion, weniger Energieverbrauch und vor allem **größere Fläche pro Klick** (1 Feld → 3 Felder → 3×3 Felder). Der Moment, in dem man plötzlich ein ganzes Beet auf einmal gießt, ist ein zentrales Belohnungsgefühl.
- Werkzeug-Upgrades kosten Münzen **und** Material.

### 5.4 Gegenstände zum Bauen und Aufstellen
Freischaltbare Objekte, die man auf der Farm platziert:
- **Sprinkler** (gießt benachbarte Felder automatisch, in mehreren Stufen mit wachsender Reichweite)
- **Vogelscheuche** (Pflanzen in der Nähe wachsen schneller)
- **Komposter** (macht aus Ernteresten Dünger)
- **Dünger** (mehr Ertrag oder schnelleres Wachstum pro Feld)
- **Truhe**, **Zaun** und **Wege** (vor allem für Optik und Ordnung)
- **Bienenstock** (erzeugt passiv Honig, wertvoller in der Nähe von Blumen)
- Später: **Gewächshaus**, **Saatgutmaschine**, **Erntehelfer** (erntet automatisch)

Die Reihenfolge der Freischaltungen soll den Spieler langsam vom aktiven Klicken hin zu mehr Automatisierung führen. Klicken bleibt aber immer der schnellste Weg.

### 5.5 Farm-Ausbau
Die Farm wächst in Etappen. Jede Etappe ist ein klar sichtbares, großes Ziel:
1. **Startbeet** – der kleine Anfangsbereich
2. **Hofgelände** – größere Anbaufläche, freiräumen nötig
3. **Ostfeld** – kauft man mit Münzen und Holz
4. **Obstwiese** – Voraussetzung für Obstbäume
5. **Teich** – Deko und später Grundlage für weitere Ideen (z. B. Angeln)
6. **Gewächshaus** – besonders schnelles Wachstum
7. **Hügelland / Bergfeld** – Endgame-Fläche mit seltenen Pflanzen

Dazu kommt das **Farmhaus**, das sich in Stufen ausbauen lässt (Zelt → Hütte → Bauernhaus → Hof). Jede Stufe erhöht zum Beispiel die maximale Energie und sieht sichtbar schöner aus.

Gesperrte Bereiche sind von Anfang an sichtbar (z. B. hinter einem Zaun, abgedunkelt oder mit Schild und Preis), damit der Spieler weiß, was ihn erwartet.

### 5.6 Spielerstufe und XP
- Jede Aktion (Aufräumen, Ernten, Bauen) gibt XP.
- Stufenaufstiege schalten Pflanzen, Gegenstände und Ausbauten frei.
- Jeder Stufenaufstieg muss **mindestens eine** neue Sache bringen. Eine Stufe ohne Belohnung gibt es nicht.
- Die XP-Leiste ist immer sichtbar.

### 5.7 Energie, Tage und Schlaf
- Werkzeugaktionen kosten Energie. Das Wachsen-lassen per Klick kostet keine Energie, sonst wird das Klicken bestraft.
- Ist die Energie leer, geht der Spieler schlafen. Ein neuer Tag beginnt mit voller Energie.
- Über Nacht wachsen gegossene Pflanzen ein Stück, und die Versandkiste wird ausgezahlt.
- Ein Spieltag dauert in Echtzeit nur wenige Minuten.
- Ein schöner Tag/Nacht-Lichtverlauf unterstützt die Stimmung.
- Energie ist ein Takt, keine Strafe. Sie soll Abwechslung erzeugen, aber nie frustrieren.

### 5.8 Verkaufen und Laden
- **Versandkiste**: Ernte hineinlegen, Auszahlung über Nacht.
- **Sofortverkauf** ist auch möglich, aber etwas schlechter bezahlt.
- **Laden**: Samen, Gegenstände und Werkzeug-Upgrades kaufen. Gesperrte Artikel sind sichtbar, mit dem Hinweis, ab wann sie verfügbar sind.

### 5.9 Sammelbuch (Almanach)
- Eine Übersicht aller Pflanzen und Gegenstände im Spiel.
- Noch nicht entdeckte Einträge sind als Silhouette mit „?“ zu sehen.
- Für jede Pflanze zählt das Buch, wie oft sie geerntet wurde. Meilensteine (10, 100, 1000 Ernten) geben kleine Boni.
- Das Buch soll Sammel- und Komplettierungslust wecken.

### 5.10 Aufträge
- Kleine, wechselnde Aufträge wie „Liefere 10 Karotten“ oder „Räume 20 Steine“, mit Belohnung in Münzen, Material oder XP.
- Ein paar große, langfristige Projekte, z. B. „Repariere die alte Brücke“: mehrere Lieferungen, schaltet am Ende einen neuen Farmbereich frei.

### 5.11 Offline-Fortschritt und Speichern
- Das Spiel speichert automatisch und regelmäßig sowie beim Schließen.
- Wenn der Spieler zurückkommt, haben Sprinkler und Automatisierung weitergearbeitet (begrenzt, z. B. auf einige Stunden).
- Beim Zurückkehren zeigt ein „Willkommen zurück!“-Fenster, was in der Zwischenzeit passiert ist. Das ist auch ein guter Moment für ein freiwilliges Belohnungsvideo („Ertrag verdoppeln?“).

## 6. Bildschirm und Bedienung

- Der Hauptbildschirm zeigt die Farm. Man kann wischen, um sie zu verschieben, und optional zoomen.
- Immer sichtbar: Münzen, Energie, Spielerstufe mit XP-Leiste, nächstes Freischaltziel, Uhrzeit/Tag.
- Unten eine Werkzeugleiste mit dem aktiven Werkzeug bzw. Samen. Ein Tipp aufs Feld führt die passende Aktion aus.
- Wo es eindeutig ist, wählt das Spiel die passende Aktion automatisch (Tipp auf einen Stein → Spitzhacke). Das spart Wechsel und hält den Klickfluss.
- Menüs: Laden, Bauen, Almanach, Aufträge, Einstellungen.
- Wichtige Aktionen müssen mit dem Daumen gut erreichbar sein.

## 7. Spielgefühl und Feedback („Juice“)

Dieser Punkt ist genauso wichtig wie die Spielmechanik:
- Jeder Tipp löst sichtbares Feedback aus (Wackeln, Partikel, Zahl, Sound, auf dem Handy eine leichte Vibration).
- Erntereife Pflanzen springen beim Ernten heraus und fliegen in Richtung Münzanzeige.
- Freischaltungen und Stufenaufstiege bekommen eine kleine Feier.
- Zahlen zählen sichtbar hoch statt einfach umzuspringen.
- Die Stimmung bleibt gemütlich, freundlich und farbenfroh. Keine Hektik, kein Druck.

## 8. Balancing-Leitlinien

- **Erste Minute:** Der Spieler räumt, sät, klickt und erntet zum ersten Mal, ganz ohne Erklärungstext. Eine kurze, geführte Einführung zeigt die ersten Schritte.
- **Erste 10 Minuten:** mehrere Pflanzen und das erste Werkzeug-Upgrade freigeschaltet, erster Ausbau in greifbarer Nähe.
- **Erste Stunde:** Sprinkler, mehrere Farmbereiche, deutliche Automatisierung.
- **Langfristig:** Ziele über Tage und Wochen (Endgame-Pflanzen, komplettes Sammelbuch, voll ausgebaute Farm).
- Alle Werte (Preise, Wachstum, XP, Freischaltstufen) müssen **an einer zentralen Stelle** leicht veränderbar sein, ohne den Code anzufassen. Balancing wird viel angepasst werden.
- Neue Pflanzen, Werkzeuge und Gegenstände sollen sich ebenso leicht hinzufügen lassen.

## 9. Werbung (fair und freiwillig)

- Hauptsächlich **freiwillige Belohnungsvideos**, z. B.:
  - Energie sofort auffüllen
  - Ertrag nach dem Offline-Bonus verdoppeln
  - Alle Pflanzen auf der Farm sofort eine Stufe wachsen lassen
  - Einen kleinen Bonus im Laden (z. B. Gratis-Samenpaket)
- Zwangswerbung (Interstitials) höchstens sehr selten und nie mitten in einer Aktion, zum Beispiel beim Tageswechsel und frühestens nach einigen Minuten Spielzeit.
- Das Spiel muss ohne Werbung vollständig spielbar sein. Werbung beschleunigt nur.
- Für die Basisversion reicht es, die Stellen vorzubereiten, an denen später Werbung kommt. Die echte Anbindung folgt später.

## 10. Umfang der Basisversion

**Gehört dazu:**
- Farm mit Freiräumen, Hacken, Säen, Gießen, Klick-Wachstum und Ernten
- Mindestens 8–10 Pflanzen in einer Freischaltkette
- Die fünf Werkzeuge mit mindestens drei Ausbaustufen
- Sprinkler, Vogelscheuche und Dünger als erste Gegenstände
- Mindestens drei Farmbereiche zum Freischalten und zwei Farmhaus-Stufen
- Spielerstufen mit XP und Freischaltungen
- Energie, Tag/Nacht und Schlaf
- Versandkiste und Laden
- Sammelbuch in einfacher Form
- Speichern und Offline-Fortschritt
- Feedback-Effekte und Freischalt-Feiern (auch mit einfachen Platzhaltern)

**Kommt später, nicht jetzt bauen:**
- Jahreszeiten mit saisonalen Pflanzen
- Tiere (Hühner, Kühe)
- Dorf, Figuren, Beziehungen
- Angeln, Bergbau
- Echte Werbeanbindung und Spieleportal-Anbindung
- Endgültige Grafik und Musik

## 11. Arbeitsweise

- Baue in **kleinen, spielbaren Schritten**. Nach jedem Schritt soll das Spiel startbar und testbar sein. Sinnvolle Reihenfolge:
  1. Farm-Raster mit Freiräumen und Hacken
  2. Säen, Klick-Wachstum, Ernten, Münzen
  3. Laden und mehrere Pflanzen
  4. XP, Stufen und Freischaltungen (inklusive Feier)
  5. Werkzeug-Upgrades mit Flächenwirkung
  6. Energie, Tag/Nacht, Versandkiste
  7. Sprinkler und weitere Gegenstände
  8. Farm-Ausbau und Farmhaus
  9. Sammelbuch, Aufträge, Speichern, Offline-Fortschritt
- Frage dich bei jeder Funktion: **Verstärkt das das Fortschrittsgefühl?** Wenn nicht, ist sie wahrscheinlich nicht wichtig für die Basisversion.
- Halte Entscheidungen und Annahmen in `DECISIONS.md` fest und pflege eine kurze Liste offener Punkte in `TODO.md`.
- Die Spielsprache ist zunächst **Deutsch**. Texte sollen sich später leicht übersetzen lassen (Englisch folgt).
