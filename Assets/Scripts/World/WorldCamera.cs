using UnityEngine;
using UnityEngine.EventSystems;

namespace SeedClicker
{
    /// <summary>
    /// Kamera und Eingabe der freien Welt.
    /// Axt: Tippen faellt Baeume, Wischen verschiebt die Karte.
    /// Feld: Tippen/Wischen legt Felder im Raster an.
    /// Zwei Finger / Mausrad / rechte Maustaste: verschieben und zoomen.
    /// </summary>
    public class WorldCamera : MonoBehaviour
    {
        public static WorldCamera I { get; private set; }
        public static WorldTool Tool = WorldTool.Axe;

        Camera cam;
        Vector3 basePos;
        float shake;
        float punch, punchApplied;       // kurzer Zoom-Stoss (Klick-Wucht)
        Vector2 kick, kickVel;           // gerichteter Ruck, federt zurueck

        // Zeigerzustand
        bool down, overUI, panning, painting;   // painting: frueher Wischen mit Werkzeugen, bleibt aus
        Vector2 downScreen, lastScreen;
        float lastPinch;
        int lastTouchCount;

        float MinSize => 1.5f / Mathf.Max(0.3f, cam.aspect);
        float MaxSize => Mathf.Min(WorldBuilder.MapH / 2f, WorldBuilder.MapW / 2f / Mathf.Max(0.3f, cam.aspect));

        void Awake()
        {
            I = this;
            cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera") { tag = "MainCamera" }; cam = go.AddComponent<Camera>(); }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            Input.simulateMouseWithTouches = false;
            cam.orthographicSize = Mathf.Clamp(4.4f / Mathf.Max(0.3f, cam.aspect), MinSize, MaxSize);
            basePos = new Vector3(WorldBuilder.HousePos.x - 0.8f, WorldBuilder.HousePos.y - 1.9f, -10);   // Haus und eingezaeuntes Startbeet im Blick
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);
        /// <summary>Kurzer Zoom-Stoss nach innen (Anteil der Bildgroesse, z. B. 0.02).</summary>
        public void Punch(float amount) => punch = Mathf.Max(punch, amount);
        /// <summary>Gerichteter Ruck der Kamera, federt elastisch zurueck.</summary>
        public void Kick(Vector2 dir) => kickVel += dir;

        void LateUpdate()
        {
            if (Input.touchCount > 0) Touches();
            else Mouse();
            Clamp();
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            shake = Mathf.MoveTowards(shake, 0f, dt * 0.5f);
            // Zoom-Stoss: vorherigen Anteil herausrechnen, abklingen lassen, neu anwenden
            cam.orthographicSize /= 1f - punchApplied;
            punch = Mathf.MoveTowards(punch, 0f, dt * 0.25f) * Mathf.Pow(0.0005f, dt);
            punchApplied = punch;
            cam.orthographicSize *= 1f - punchApplied;
            // Ruck als gedaempfte Feder
            kickVel += (-kick * 900f - kickVel * 28f) * dt;
            kick += kickVel * dt;
            cam.transform.position = basePos + (Vector3)kick + (shake > 0 ? (Vector3)(Random.insideUnitCircle * shake) : Vector3.zero);
        }

        Vector2 World(Vector2 screen) => cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));

        static bool OverUI(int id)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return id < 0 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(id);
        }

        // ------------------------------------------------------------------ Maus

        void Mouse()
        {
            lastTouchCount = 0;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && !OverUI(-1)) ZoomAt(Input.mousePosition, 1f - wheel * 0.1f);

            // Rechte/mittlere Taste: immer verschieben
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) lastScreen = Input.mousePosition;
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2)) { PanBy((Vector2)Input.mousePosition - lastScreen); lastScreen = Input.mousePosition; return; }

            // Maus ohne Druck: geplante Mauer folgt dem Zeiger
            var wbh = WorldBuilder.I;
            if (!down && wbh != null && wbh.PlanFollows && !OverUI(-1)) wbh.UpdateWallPlan(World(Input.mousePosition));
            // Maus ueber einem Feld: zeigen, woher es geboostet wird
            if (wbh != null) wbh.HoverCell = !down && !OverUI(-1) && !wbh.BuildMode && !wbh.Placing && wbh.PlacingStructure == null && !wbh.FieldPlanActive
                ? wbh.TapCell(World(Input.mousePosition)) : (Cell?)null;
            if (Input.GetMouseButtonDown(0)) PointerDown(Input.mousePosition, -1);
            else if (Input.GetMouseButton(0) && down) PointerMove(Input.mousePosition);
            if (Input.GetMouseButtonUp(0) && down) PointerUp(Input.mousePosition);
        }

        // ------------------------------------------------------------------ Touch

        void Touches()
        {
            int n = Input.touchCount;
            if (n >= 2)
            {
                var t0 = Input.GetTouch(0); var t1 = Input.GetTouch(1);
                float dist = Vector2.Distance(t0.position, t1.position);
                Vector2 mid = (t0.position + t1.position) / 2f;
                if (lastTouchCount >= 2)
                {
                    if (lastPinch > 1f) ZoomAt(mid, lastPinch / Mathf.Max(1f, dist));
                    PanBy(mid - lastScreen);
                }
                down = false; painting = false;
                lastPinch = dist; lastScreen = mid; lastTouchCount = n;
                return;
            }
            var t = Input.GetTouch(0);
            if (lastTouchCount >= 2) { lastTouchCount = n; lastScreen = t.position; return; } // nach dem Zoomen nicht sofort tippen
            lastTouchCount = n;
            switch (t.phase)
            {
                case TouchPhase.Began: PointerDown(t.position, t.fingerId); break;
                case TouchPhase.Moved: case TouchPhase.Stationary: if (down) PointerMove(t.position); break;
                case TouchPhase.Ended: if (down) PointerUp(t.position); break;
                case TouchPhase.Canceled: down = false; painting = false; break;
            }
        }

        // ------------------------------------------------------------------ Werkzeuglogik

        bool consumed;   // Druck wurde schon verarbeitet (z. B. Wachstums-Tipp)
        PlantedState dragCand;   // reife Pflanze unter dem Finger: kann gezogen werden
        bool draggingPlant;
        bool tappedReady;   // dieser Druck hat die Pflanze schon wachsen lassen (Info-Fenster offen)

        void PointerDown(Vector2 screen, int id)
        {
            down = true; panning = false; painting = false; consumed = false; dragCand = null; draggingPlant = false; tappedReady = false;
            downScreen = lastScreen = screen;
            overUI = OverUI(id);
            if (overUI) return;
            var wb = WorldBuilder.I;
            if (wb == null) return;
            var w = World(screen);
            var c = wb.TapCell(w);
            if (wb.TryCatchButterfly(w)) { consumed = true; return; }
            // Pflanze: laesst sich ziehen (umtopfen); ein kurzer Tipp oeffnet beim Loslassen ihr Info-Fenster
            var under = wb.PlantAt(c);
            // Bau-Modus: gedrueckt halten und ziehen malt Mauern bzw. Pulte
            if (wb.PedestalMode && !wb.Placing && wb.BeginBuildStroke(c)) { painting = true; return; }
            if (under != null && !wb.Placing && !wb.BuildMode)
            {
                dragCand = under;
                // Info-Fenster dieser Pflanze ist offen: Tipp laesst sie sofort wachsen/leveln (wie der Knopf)
                if (under == wb.Inspected) { wb.TapPlant(c); tappedReady = true; }
                return;
            }
            // Nachts: Tipp = Schuss mit der gewaehlten Waffe (geht genau auf die Tipp-Stelle)
            if (NightWave.I != null && NightWave.I.TryShoot(w)) { consumed = true; return; }
            if (wb.Placing) return;   // Ziel waehlen: erst beim Loslassen (so kann man dabei auch die Karte schieben)
            if (wb.PlantAt(c) != null) return;   // fertige Pflanze -> beim Loslassen
        }

        void PointerMove(Vector2 screen)
        {
            if (overUI || consumed) return;
            if (painting) { var wbp = WorldBuilder.I; wbp?.ContinueBuildStroke(wbp.TapCell(World(screen))); lastScreen = screen; return; }
            // Mauer wird geplant: Finger zieht die gruene Vorschau mit (statt die Karte zu schieben)
            if (WorldBuilder.I != null && WorldBuilder.I.PlanFollows) { WorldBuilder.I.UpdateWallPlan(World(screen)); lastScreen = screen; return; }
            if (dragCand != null)
            {
                var wbd = WorldBuilder.I;
                float dpiD = Screen.dpi > 0 ? Screen.dpi : 160f;
                if (!draggingPlant && Vector2.Distance(screen, downScreen) > dpiD * 0.06f)
                    draggingPlant = wbd != null && wbd.BeginDrag(dragCand, World(screen));
                if (draggingPlant) { wbd.DragTo(World(screen)); lastScreen = screen; return; }
            }
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            if (!panning && Vector2.Distance(screen, downScreen) > dpi * 0.08f) panning = true;
            if (panning) PanBy(screen - lastScreen);
            lastScreen = screen;
        }

        void PointerUp(Vector2 screen)
        {
            down = false;
            if (draggingPlant) { draggingPlant = false; dragCand = null; WorldBuilder.I?.Drop(World(screen)); return; }
            dragCand = null;
            if (tappedReady) { tappedReady = false; return; }
            if (painting) { painting = false; WorldBuilder.I?.EndBuildStroke(); return; }
            if (overUI || panning || consumed) return;
            var w = World(screen);
            var wb = WorldBuilder.I;
            if (wb == null) return;
            // Knolli antippen (auswaehlen), dann ein Feld antippen (zuordnen)
            if (wb.HitKnolli(w)) { wb.TapKnolli(); return; }
            if (wb.KnolliSelected) { wb.AssignKnolli(wb.TapCell(w)); return; }
            if (wb.HitWorkbench(w)) { wb.TapWorkbench(); return; }
            var cell = wb.TapCell(w);
            // Felder werden gerade geplant: dieser Tipp legt sie an
            if (wb.FieldPlanActive) { wb.FieldClick(w); return; }
            if (wb.PlacingStructure != null) { if (WorldBuilder.IsTotem(wb.PlacingStructure)) wb.TotemClick(w); else wb.TryPlaceStructure(cell); return; }
            if (wb.HitBench(w)) { wb.TapBench(); return; }
            if (wb.HitAltar(w)) { wb.TapAltar(); return; }
            if (wb.HitCrate(w)) { wb.TapCrate(); return; }
            var hs = wb.HitStructure(w);
            if (hs != null && !wb.Placing && !wb.BuildMode) { wb.TapStructure(hs); return; }
            if (wb.Placing)
            {
                if (wb.PlantAt(cell) == wb.Carrying) wb.CancelPlacing();
                else wb.TryPlaceAt(w);
                return;
            }

            // ein Tipp macht das Naheliegende: Mauer bauen (Mauer-Modus), Pflanzen-Info oeffnen, leeren Platz bepflanzen,
            // Feld anlegen, Baum faellen, Fels abbauen
            if (wb.WallMode) { if (!wb.WallClick(w)) { if (!wb.TryChop(w)) wb.TryMine(w); } return; }
            if (wb.RiverMode) { if (!wb.RiverClick(w)) { if (!wb.TryChop(w)) wb.TryMine(w); } return; }
            if (wb.PedestalMode) { if (!wb.TapPedestalMode(cell)) { if (!wb.TryChop(w)) wb.TryMine(w); } return; }
            if (wb.PlantAt(cell) != null) { wb.InspectPlant(cell); return; }
            if (wb.IsSlot(cell)) { wb.TapEmptyField(cell); return; }
            if (wb.TryChop(w) || wb.TryMine(w)) return;
            if (wb.CanPlaceField(cell)) wb.FieldClick(w);   // Anfang setzen, gruene Vorschau folgt

        }

        // ------------------------------------------------------------------ Verschieben / Zoomen

        void PanBy(Vector2 d) => basePos -= new Vector3(d.x, d.y, 0) * (cam.orthographicSize * 2f / Screen.height);

        void ZoomAt(Vector2 screen, float factor)
        {
            var before = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, MinSize, MaxSize);
            cam.transform.position = basePos;
            var after = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10));
            basePos += before - after;
        }

        void Clamp()
        {
            float h = cam.orthographicSize, w = h * cam.aspect;
            float hx = WorldBuilder.MapW / 2f, hy = WorldBuilder.MapH / 2f;
            basePos.x = w * 2 >= hx * 2 ? 0 : Mathf.Clamp(basePos.x, -hx + w, hx - w);
            const float topExtra = 4.5f; // oben darf man ein Stueck ueber die Karte hinaus auf den Berg schauen
            basePos.y = h * 2 >= hy * 2 + topExtra ? topExtra / 2f : Mathf.Clamp(basePos.y, -hy + h, hy + topExtra - h);
            basePos.z = -10;
        }
    }
}
