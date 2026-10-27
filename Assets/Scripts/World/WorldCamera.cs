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

        // Zeigerzustand
        bool down, overUI, panning, painting;
        Vector2 downScreen, lastScreen;
        float lastPinch;
        int lastTouchCount;
        Cell lastCell;
        bool hasLastCell;

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
            basePos = new Vector3(WorldBuilder.HousePos.x, WorldBuilder.HousePos.y - 0.3f, -10);
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        void LateUpdate()
        {
            if (Input.touchCount > 0) Touches();
            else Mouse();
            Clamp();
            shake = Mathf.MoveTowards(shake, 0f, Time.deltaTime * 0.5f);
            cam.transform.position = basePos + (shake > 0 ? (Vector3)(Random.insideUnitCircle * shake) : Vector3.zero);
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

        void PointerDown(Vector2 screen, int id)
        {
            down = true; panning = false; painting = false; hasLastCell = false; consumed = false;
            downScreen = lastScreen = screen;
            overUI = OverUI(id);
            if (overUI) return;
            var wb = WorldBuilder.I;
            if (wb == null) return;
            var w = World(screen);
            var c = WorldBuilder.CellAt(w);
            // Wachsende Pflanze: sofort beim Druecken reagieren – das macht das Klicken knackig
            if (wb.IsPlantGrowing(c)) { wb.TapPlant(c); consumed = true; return; }
            // Nachts: Tipp auf einen Zombie = Schuss mit der gewaehlten Waffe
            if (NightWave.I != null && NightWave.I.TryShoot(w)) { consumed = true; return; }
            if (wb.PlantAt(c) != null || wb.PotAt(w) != null) return;   // fertige Pflanze / Topf -> Info beim Loslassen
            if (Tool == WorldTool.Field || Tool == WorldTool.Fence || (Tool == WorldTool.Plant && wb.HasField(c)))
            {
                painting = true; Paint(screen);
            }
        }

        void PointerMove(Vector2 screen)
        {
            if (overUI || consumed) return;
            if (painting) { Paint(screen); return; }
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            if (!panning && Vector2.Distance(screen, downScreen) > dpi * 0.08f) panning = true;
            if (panning) PanBy(screen - lastScreen);
            lastScreen = screen;
        }

        void PointerUp(Vector2 screen)
        {
            down = false;
            if (overUI || panning || painting || consumed) { painting = false; return; }
            var w = World(screen);
            var wb = WorldBuilder.I;
            if (wb == null) return;
            if (wb.HitWorkbench(w)) { wb.TapWorkbench(); return; }
            var cell = WorldBuilder.CellAt(w);
            var pot = wb.PotAt(w);
            if (pot != null && Tool != WorldTool.Axe) { wb.TapPot(pot); return; }
            if (wb.PlantAt(cell) != null && Tool != WorldTool.Axe) { wb.TapPlant(cell); return; }
            if (Tool == WorldTool.Axe)
            {
                if (wb.TryChop(w) || wb.TryRemoveFence(w)) return;
                if (pot != null) wb.TapPot(pot);
                else if (wb.PlantAt(cell) != null) wb.TapPlant(cell);
                return;
            }
            if (Tool == WorldTool.Pot) wb.PlacePotFromStock(w);
        }

        /// <summary>Wischen: Felder anlegen, Zaeune ziehen (Zaun-Werkzeug) bzw. leere Felder bepflanzen (Pflanzen-Werkzeug).</summary>
        void Paint(Vector2 screen)
        {
            var wb = WorldBuilder.I;
            if (wb == null) return;
            var c = Tool == WorldTool.Fence ? WorldBuilder.FenceCellAt(World(screen)) : WorldBuilder.CellAt(World(screen));
            if (hasLastCell && c.x == lastCell.x && c.y == lastCell.y) return;
            hasLastCell = true; lastCell = c;
            if (Tool == WorldTool.Field) { wb.ShowCellCursor(c); wb.PlaceField(c, true); return; }
            if (Tool == WorldTool.Fence) { wb.ShowFenceCursor(c); wb.PlaceFence(c, true); return; }
            if (Tool == WorldTool.Plant && wb.HasField(c) && wb.PlantAt(c) == null)
            {
                var sel = wb.Save.selectedPlant;
                if (string.IsNullOrEmpty(sel) || !wb.IsUnlocked(sel)) { wb.TapEmptyField(c); painting = false; return; }
                wb.PlantInto(c, sel);
            }
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
