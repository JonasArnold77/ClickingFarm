# DECISIONS.md – Entscheidungen und Annahmen

## Technik
- **Alles wird per Code aufgebaut.** `Bootstrap` startet das Spiel automatisch in jeder Szene (`RuntimeInitializeOnLoadMethod`). Einfach `SampleScene` öffnen und Play drücken. Keine Prefabs, keine Szenen-Verkabelung, die kaputtgehen kann.
- **Platzhalter-Grafik wird prozedural als Pixel-Art erzeugt** (`Art/Art.cs`, 16 px = 1 Feld, Point-Filter, automatische dunkle Kontur und Schattierung). Stil: Stardew-artig, aber gesättigtere, bunte Farben.
  Echte Grafiken können später ohne Codeänderung eingesetzt werden: `Resources/Sprites/<schlüssel>` (z. B. `crop_tomato_3.png` für den Schlüssel `crop:tomato:3`) hat Vorrang.
- **Sounds werden synthetisiert** (`Fx/Sfx.cs`), keine Audiodateien nötig.
- **UI mit uGUI + eingebauter Schrift (LegacyRuntime)** statt TextMeshPro, damit kein TMP-Essentials-Import nötig ist. Keine Emojis in Texten (Schrift kann sie nicht), stattdessen Pixel-Icons.
- **Tag/Nacht über das Global Light 2D** der URP-2D-Pipeline; am Abend leuchtet ein Point Light am Haus.
- **Speichern in PlayerPrefs als JSON** – funktioniert gleich auf Android und WebGL.
- Logik (`Core/Game*.cs`) ist von Darstellung (`View/`) und Oberfläche (`UI/`) getrennt; diese hören nur auf Events.

## Balancing und Inhalte
- **Alle Werte stehen in `Assets/Resources/Balance/balance.json`** (Pflanzen, Werkzeugstufen, Hindernisse, Bereiche, Gebäude, Gegenstände, Fähigkeiten, Projekte, Auftragsvorlagen, XP-Kurve, Tageslänge …). Neue Pflanze = ein JSON-Eintrag + Name in `Lang/de.txt`.
- **Alle Texte stehen in `Assets/Resources/Lang/de.txt`** (`schlüssel=Text`). Für Englisch einfach `en.txt` anlegen und `Loc.Load("en")` aufrufen.
- XP-Kurve: `xpBase * Stufe^xpExponent` (8 · L²). Jede Stufe ohne eigene Freischaltung bekommt automatisch ein Bonuspaket (Münzen, Gratis-Samen, Material) – keine leeren Stufen.
- Jeder Stufenaufstieg füllt die Energie auf (Energie soll Takt sein, keine Strafe).

## Spielmechanik
- **Samen werden beim Säen direkt bezahlt** (kein Samen-Inventar). Hält den Klickfluss. Der Laden dient zur Samenauswahl; Gratis-Samen (Werbung, Bonus) werden zuerst verbraucht.
- **Automatische Aktionswahl:** Hindernis → passendes Werkzeug, Wiese → Hacke, leere Erde → Säen, ungegossen → Gießen, wachsend → Wachsen, reif → Ernten.
- **Klick-Wachstum kostet keine Energie.** Dazu:
  - **Combo:** schnelle Tipps hintereinander steigern den Multiplikator (×1,5, ×2 … bis Combo-Maximum, ausbaubar).
  - **Super-Wachstum:** Zufallschance (Glücksstern) auf ×5.
  - **Wischen:** Wischt man über Pflanzen, wird jedes Feld „angetippt“ (Wachsen/Ernten/Gießen/Säen).
  - Wischen auf anderen Feldern verschiebt die Kamera; zwei Finger zoomen.
- Ernte geht in die **Versandkiste** (fliegt sichtbar dorthin). Auszahlung über Nacht zum vollen Preis, Sofortverkauf an der Kiste mit Abschlag (70 %, später 80/90 %).
- **Gebäude-Ausbau ist eine Klick-Mechanik:** Nach dem Bezahlen steht eine Baustelle, die man per Hammerschlägen (Tippen) fertig baut.
- Werkzeuge haben 5 Stufen (Holz → Kupfer → Eisen → Gold → Kristall). Fläche: 1 → 3 → 3×3 → 3×3 → 5×5. Die Sichel bestimmt die Ernte-Fläche, die Hacke auch die Säe-Fläche.
- Felsbrocken (Hügelland) brauchen mindestens eine Eisen-Spitzhacke.
- Passive Wachstum: gegossene Pflanzen wachsen langsam von selbst (Grundlage für Automatisierung); Klicken bleibt immer der schnellste Weg.
- Automatisierung: Sprinkler (3 Stufen), Vogelscheuche, Dünger (dauerhaft pro Feld), Bienenstock (Honig, Blumen-Bonus), Erntehelfer/Erntemeister (ernten und säen neu), Teich (wässert Ufer), Gewächshaus (immer gegossen, ×2 Wachstum).
- Über Nacht kann auf freien Wiesenfeldern neues Gras/Äste/Steine wachsen → Materialnachschub.
- Offline-Fortschritt: bis 4 Std., simuliert passives Wachstum, Bienen und Erntehelfer. Die Spieluhr läuft offline nicht weiter.
- Die Uhr pausiert, solange ein Fenster offen ist.
- Glückskäfer: fliegt ab und zu über die Farm, Antippen gibt Bonus-Münzen und XP.
- Farmbereiche: Startbeet, Hofgelände, Ostfeld, Obstwiese, Teich, Gewächshaus (Großprojekt nötig), Hügelland (Großprojekt „Brücke“ nötig).
- Farmhaus: Zelt → Hütte → Bauernhaus → Gutshof. Versandkiste: Kiste → Marktkarren → Hofladen.

## Start-Samen und Nachtverteidigung (Erweiterung nach Rückmeldung)
- **Start wie bei Pokémon:** Beim ersten Start wählt man einen von drei Start-Samen: Glutschote (Feuer), Tauglocke (Wasser), Dornenranke (Pflanze). Die anderen beiden gibt es ab Stufe 5 (`otherStartersLevel`).
- **Startfarm:** ein fertig umgegrabenes, leeres 3×3-Beet im Startbereich, keine Hindernisse. Wildes Land gibt es erst beim Ausbau.
- **Beschützer-Pflanzen** (`category: "defender"` in `balance.json`, Werte unter `defense`) wachsen wie alle Pflanzen per Klick (~100 Tipps). Reif werden sie **nicht geerntet**, sondern bleiben als Wächter auf ihrem Feld.
- **Tag = Farmen, Nacht = Verteidigen:** Ab 20 Uhr (`night.startHour`) kommen Monster in Wellen aus dem Wald (links, rechts, unten). Die Uhr pausiert während des Kampfs. Danach folgt Schlafen.
- **Platzierung zählt:** Jede Abwehrpflanze hat eine Reichweite (Ring wird abends, im Kampf und bei Auswahl eines Beschützer-Samens angezeigt). Manche machen Flächenschaden (Tauglocke: Wasserblase mit Verlangsamung; Kanonenkürbis: Mörser; Dornenranke: Puls auf alle in Reichweite).
- **Anfeuern (Push):** Antippen bzw. Wischen über einen Beschützer im Kampf löst einen Power-Schuss aus und stapelt Angriffstempo (max. 10 Stapel, klingen ab). Monster kann man auch direkt antippen.
- **Elemente:** Feuer > Pflanze > Wasser > Feuer (×1,5 / ×0,7). Monster: Schleim (Wasser), Glutkäfer (Feuer), Unkrautgnom (Pflanze), Krähe (neutral, schnell).
- **Kein Frust:** Monster knabbern Pflanzen an (normale Pflanzen verlieren Wachstum, im schlimmsten Fall gefressen), Beschützer werden nur **welk** (müssen etwas nachwachsen), am Haus wird ein kleiner Münzbetrag gestohlen. Nach `maxSeconds` fliehen Monster im Morgengrauen. Über Nacht heilen Beschützer vollständig.
- Abweichung von CLAUDE.md („keine Hektik“): Der Nachtkampf ist bewusst kurz und verzeihend, damit die gemütliche Grundstimmung bleibt.

## Grafikstil
- Pixel-Art im Stil gemütlicher 16-px-Farmspiele (Stardew-artige Ästhetik): erdige, warme Palette, dunkelbraune statt schwarzer Konturen, Beete verbinden sich automatisch (Autotiling), Holzzäune um das freigeschaltete Land, buschige Laubbäume und Tannen, Holzhaus mit Schindeldach, Menüs als Holzrahmen mit Pergament. Alles selbst gezeichnet, nichts aus Stardew Valley übernommen.
- Beschützer-Pflanzen und Monster haben Gesichter, damit sie Charakter bekommen.

## Frei gezeichnete Welt ohne Tile-Raster (aktueller Stand)
- `Bootstrap.LegacyFarm = false` startet die neue Welt (`Scripts/World/`). Das bisherige Raster-Farmspiel (Beete, Nachtkampf, UI) bleibt im Code und lässt sich mit `LegacyFarm = true` wieder einschalten; es wird später auf die freie Welt umgezogen.
- **Grafik kommt aus gekauften/importierten Paketen, nicht mehr aus Code:**
  - **Skyden Games „Cozy Farm“** ist der Hauptstil (weich gemalt): Boden-Atlas, Bäume, Haus, Büsche, Steine, Blumen, Pilze.
  - **Happy Harvest** nur für passende Props (Fass, Wegweiser, Baumstamm, Felsen, Grasbüschel, Blumen). Dessen Bäume/Büsche sind flacher Vektorstil und werden bewusst nicht benutzt.
- `Resources/WorldArt.asset` (`WorldArtSet`) listet alle benutzten Sprites mit Namen (`sk_…`, `hh_…`) und den Boden-Atlas. Neue Grafik = Eintrag im Inspector hinzufügen.
- **Boden = ein Material** (`Resources/Shaders/TerrainSplat2D.shader`, URP-2D-beleuchtet) mit Kacheln aus dem Skyden-Atlas: Gras, dunkles Gras, Erdweg, Kopfsteinpflaster, Schlamm (abgedunkelte Erde mit Pfützen). Splat-Maske: R Schlamm, G Weg, B dunkles Gras, A Pflaster. Kanten mit Rauschen ausgefranst.
- Maßstab: 1 Welteinheit = 1 Skyden-Kachel (256 px). Karte 22 × 30, Haus (Skyden House_0, Skalierung 1,6) oben mittig mit Pflaster-Vorplatz, Erdweg nach Süden mit Abzweig, Schlammlöcher. Objekte frei platziert mit Abstandsprüfung, Sortierung nach Unterkante (Y).
- Die prozedurale Grafik (`Scripts/Art`) wird nur noch vom alten Raster-Spiel benutzt.
- **Klippe hinter dem Haus** (`WorldBuilder.Cliff.cs`, nach Skizze): Das Plateau endet an einer Kammlinie (`Ridge`), Spitze direkt hinter dem Haus, links steil, rechts flacher mit Knick. Der Boden-Shader macht alles jenseits der Kante durchsichtig (`_Plateau`-Maske). Entlang der Kante liegt ein Mesh-Band mit der Skyden-Klippenwand (Grasrand → Fels → Nebel).
- **Klippenkante** ist nur ein schmaler Fels-/Grasrand. **Abgrund:** keine Bäume; dichter, treibender Nebel über einem tiefen, wasserartigen Blau mit sanft pulsierendem Lichtschimmer. Die Tiefe bewegt sich mit halber Kamerageschwindigkeit (Parallaxe); Nebel-Sprites sind bilinear gefiltert, damit sie weich bleiben.
- Sortierwerte (`sortingOrder`) müssen in −32768…32767 liegen; Hintergrundebenen nutzen −32700…−32400.

## Werbung
- `Core/AdService.cs` ist die einzige Stelle für Werbung. Derzeit zeigt die UI ein 2-Sekunden-Platzhalter-Video und vergibt dann die Belohnung.
- Belohnungsvideos: Energie auffüllen, Offline-Ertrag verdoppeln, alles wachsen lassen, Gratis-Samenpaket.
- Interstitial-Platz: nur beim Tageswechsel, frühestens nach 5 Min. Spielzeit, max. alle 10 Min. (nur Log, nicht angebunden).
