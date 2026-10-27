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
- **Berg hinter dem Haus** (`WorldBuilder.Mountain.cs`, ersetzt die frühere Klippe mit Abgrund): Am Bergfuß (`Ridge`) steigt eine Felswand aus Skyden Mountain Tiles auf, darüber eine grasige Hochebene mit Bäumen, dahinter eine grünblaue mittlere und eine dunstig-blaue ferne Bergkette mit Schneegipfeln, Dunstschleier dazwischen, Himmel mit Wolken. Die Ebenen bewegen sich unterschiedlich schnell (Parallaxe). Die Kamera darf oben 4,5 Einheiten über die Karte hinaus auf den Berg schauen.
- **Größere Objekte:** Haus Skalierung 2,3 (vorher 1,6), Bäume ca. 2,1/1,75 (vorher 1,55/1,3), Stümpfe passend.
- **Post-Processing im Ghibli-Stil** (`WorldPostFX.cs`, alle Werte an einer Stelle): weiches warmes Bloom, Neutral-Tonemapping, wärmere Weißbalance, Split Toning (kühle blaugrüne Schatten, goldene Lichter), etwas mehr Sättigung/Kontrast, sanfte Vignette, leise atmende Sonnenstrahlen von oben links. HDR ist im URP-Asset eingeschaltet.
- **Angriffspflanzen-Kern (freie Welt):** Jede Angriffspflanze hat fünf Eigenschaften mit Stufe 1–5: **Fläche** (Einzelschuss, Doppelschuss, Fächer, Fläche, Durchschlag, Kette), **Distanz** (Reichweite), **Projektil** (Samenkorn, Dorn, Spore, Blütenblatt, Lichtstrahl), **Wirkung** (Natur, Feuer, Eis, Gift, Blitz) und **Zusatzeffekt** (keiner, Explosion, Vergiftung, Rückstoß, Volltreffer, Mehrfachschuss). Alle Werte in `Resources/Balance/plants.json`. Annahme: Die Anfrage nannte „eines von drei Attributen“, zählte aber fünf auf – jede Pflanze hat alle fünf.
- **Start wie bei Pokémon:** drei feste Start-Pflanzen (Glutmais, Frostweizen, Giftrübe), eine wird gewählt. Die beiden anderen werden später freigeschaltet (nach Welle 7/9/11), damit sie Sammelziele bleiben.
- **Kein Kreuzen mehr.** Fortschritt = neue Pflanzenarten freischalten. Jede Art steht fest im Katalog (`plants.json` → `catalog`) mit ihren fünf Eigenschaften und `unlockWave`; jede überstandene Welle schaltet die nächste(n) frei (Feier im Ergebnisfenster, Ziel-Anzeige „Nächste Pflanze: nach Welle N“ als Silhouette oben links). Pflanzenbuch (Stumpf mit Saatgut neben dem Haus) zeigt alle Arten, gesperrte als Silhouette.
- **Kleinere Felder:** Feldraster 0,6 Welteinheiten (`WorldBuilder.CellSize`), passend zum Haus. Spielstand-Schlüssel auf v2 gewechselt (altes Raster passt nicht mehr).
- **Klick-Wachstum dauert:** 100 Tipps pro Pflanze (`growTaps`), Meilensteine bei 25/50/75 %. Pflanze wächst mit jedem Tipp sichtbar ein Stück, Fortschrittsbalken darüber.
- **„Crunchy“ Klick:** jeder Tipp = Knack-Sound (Rauschen + tiefer Schlag + Klick), Wachs-Ton steigt mit Fortschritt, Quetschen/Kippen, Erdkrümel, Blätter, Funken, leichte Vibration und Kamerawackeln. Schnelles Tippen baut eine Combo auf (Fenster 0,4 s): mehr Partikel, Ringe, Tick-Ton alle 10, „Combo ×N“ alle 25. Fertig: großer Ausbruch, Fanfare.
- **Nachtwellen** (`NightWave.cs`): **Zombies** schlurfen aus dem Waldrand zum Haus. Grafik (aktuell): eigene Pixel-Art-Laufanimation aus `Assets/Characters/Ich_mochte_ein_Zombie_mi-Idle` („Scary Walk“, 8 Richtungen × 8 Bilder, 256 px, 128 px/Einheit, Punktfilter, Fußpunkt bei 14 %), in `WorldArt.asset` → `zombieWalk`/`zombieIdle`; das Bild folgt der Laufrichtung, das Animationstempo der Geschwindigkeit, die Zombie-Arten sind nur leicht eingefärbt. Fallback, falls die Bilder fehlen: die geriggte Happy-Harvest-Figur (`Prefab_character_base`, in `WorldArt.asset` → `zombiePrefab`) mit Lauf-Animation, verlangsamt und schwankend, Teile einzeln eingefärbt (grüne Haut, graues Haar, verblasste Kleidung, glühende Pupillen), Stöhnen. Arten: Schlurfer, Kriecher (klein, schnell), Moderbauer, Grabkoloss (groß, zäh). Fehlt das Prefab, werden die alten Skyden-Sprites benutzt; erreichen sie es, verliert das Haus Leben. Pflanzen schießen automatisch, Monster können auch direkt angetippt werden. Elemente: jedes Monster ist gegen eine Wirkung schwach (×1,5). Niederlage ist sanft (etwas Holz weg).
- **Tag → Abend → Nacht:** Vor jeder Welle kommt ein Abend (`waves.eveningDuration`, Test-Knopf: `testEveningDuration`) mit Glocke, Banner und orange-rosa Licht (globales Licht + Farbfilter/Weißabgleich im Post-Processing), damit man gewarnt ist und sich vorbereiten kann. Die Waffenleiste erscheint schon am Abend.
- **Waffen** (`NightWave.Weapons.cs`, Werte in `plants.json` → `weapons`): **keine eigene Spielfigur** – man zielt mit dem Finger, die Schüsse kommen von oben (vom oberen Bildrand) auf die angetippte Stelle. Fadenkreuz blitzt auf, Leuchtspur, Trefferlicht, Funken, Kamerawackeln/Zoom-Stoß. Pistole (sofortiger Treffer, ab Start), Schrotflinte (7 Kugeln streuen um den Zielpunkt, nach Welle 2), Pfeil & Bogen (fällt vom Himmel, trifft bis zu 4 nah beieinander, bleibt kurz stecken, nach Welle 3), Granatwerfer (Granate fällt mit wachsendem Schatten herab und explodiert, nach Welle 5). Waffen-Icons und Geschosse sind im Code gemalt.
- **Eingezäuntes Startbeet:** Beim ersten Start steht ein Holzzaun um ein kleines Beet links vor dem Haus (`WorldBuilder.StartArea`, ca. 3,6 × 2,7 Einheiten, rund 20 Feldzellen), oben rechts ein Tor. Felder gehen nur innerhalb (`InFarmArea`), außerhalb erscheint ein Hinweis. In und knapp vor dem Bereich wachsen keine Bäume. Der Zaun ist ein normaler Zaun (Zombies können ihn zerschlagen); die Fläche bleibt trotzdem begrenzt. Erweitern kommt später.
- **Pflanzen bleiben in ihrem Feld:** Breite höchstens eine Feldzelle, Fuß am unteren Feldrand; sie ragen nur nach oben hinaus (werden beim Wachsen höher gestreckt).
- **Plastische Pflanzen:** eigener Shader `Resources/Shaders/PlantLit2D.shader` nutzt die Normal-Maps der Happy-Harvest-Pflanzen (Secondary Texture `_NormalMap`) mit fester Sonne oben links: warmes Licht, kühle Schatten, Lichtsaum, unten Bodenschatten; dazu weicher Schlagschatten am Boden. Pflanzen sind größer (reif 1,5 Zellen breit) und ragen über ihr Feld hinaus. Gilt auch für Topfpflanzen.
- **Wachstums-Klick mit Wucht** (Hauptmechanik): Treffer-Stopp (Pflanze hält 45 ms die gestauchte Pose), weißer Treffer-Blitz (Shader `_Flash`), Feder-Stauchen mit Nachschwingen und Kippen, flache Bodenwelle, Erdbrocken in zwei Farben, Blätter und Funken, Knack + tiefer Schlag („thump“), Zoom-Stoß und Kamera-Ruck nach unten, Vibration. Die Größe pumpt bei jedem Tipp sichtbar nach. Combo lässt die Pflanze in ihrer Farbe aufglühen. Meilensteine und Reife: kurzer Stillstand der ganzen Welt (Hit-Stop), große Wellen.
- **Zäune aus Holz** (`WorldBuilder.Defense.cs`): Werkzeug „Zaun“, Wischen zieht Zaunreihen (1 Holz pro Stück, `build.fenceWood`), Skyden-Zaunkacheln verbinden sich automatisch (gerade, Ecken, Einzelpfosten). Eigenes Raster 0,9 Einheiten (Kacheln sind für 1 Einheit gemalt; im 0,6-Feldraster wären sie zu klein). Zombies, die gegen einen Zaun laufen, bleiben stehen und schlagen darauf ein (Schaden = 40 % ihres Hausschadens pro Sekunde), bis er bricht (`build.fenceHp`). Axt auf einen Zaun baut ihn ab und gibt das Holz zurück.
- **Nur Topfpflanzen verteidigen:** Eine reife Pflanze auf dem Feld antippen → „In Topf setzen“ (2 Holz, `build.potWood`). Sie kommt ins Lager (Zahl am Werkzeug „Töpfe“), das Feld ist wieder frei. Mit „Töpfe“ irgendwo auf die Karte tippen stellt sie auf (nicht auf Felder, Zäune, Bäume). Antippen → „Einsammeln“ zurück ins Lager. Feldpflanzen schießen nicht mehr. Der Terrakotta-Topf ist im Code gemalt (kein Topf in den Paketen).
- **Test:** `waves.autoWave` startet automatisch Wellen (erste nach 20 s, dann alle 60 s Tag); Knopf „Welle starten (Test)“ oben rechts.
- Sortierwerte (`sortingOrder`) müssen in −32768…32767 liegen; Hintergrundebenen nutzen −32700…−32400.

## Werbung
- `Core/AdService.cs` ist die einzige Stelle für Werbung. Derzeit zeigt die UI ein 2-Sekunden-Platzhalter-Video und vergibt dann die Belohnung.
- Belohnungsvideos: Energie auffüllen, Offline-Ertrag verdoppeln, alles wachsen lassen, Gratis-Samenpaket.
- Interstitial-Platz: nur beim Tageswechsel, frühestens nach 5 Min. Spielzeit, max. alle 10 Min. (nur Log, nicht angebunden).
