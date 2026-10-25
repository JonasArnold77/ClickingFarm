using UnityEngine;
using UnityEngine.EventSystems;

namespace SeedClicker
{
    /// <summary>
    /// Kamera und Eingabe: Tippen fuehrt die passende Aktion aus, Wischen ueber Pflanzen
    /// "malt" Aktionen (Wachsen, Ernten, Giessen, Saeen), sonst verschiebt Wischen die Kamera.
    /// Zwei Finger: zoomen. Maus: Rad zoomt, rechte Taste verschiebt.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public static CameraController I { get; private set; }

        Camera cam;
        Game g;
        Vector3 shakeOffset;
        float shakeAmt;
        Vector3 basePos;

        // Zeigerzustand
        bool down, overUI, painting, panning;
        Vector2 downScreen, lastScreen;
        Vector2Int lastTile;
        TileAction pendingAction;
        Vector2Int pendingTile;
        string pendingBuilding;
        float lastPinch;
        int lastTouchCount;

        public float MinSize => 3f / Mathf.Max(0.3f, cam.aspect);
        public float MaxSize => Mathf.Max((g.W + 3) / 2f / Mathf.Max(0.3f, cam.aspect), 6f);

        void Awake()
        {
            I = this;
            g = Game.I;
            cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); }
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            Input.simulateMouseWithTouches = false;
            FocusStart();
        }

        public void FocusStart()
        {
            var start = Balance.Area("start");
            var c = start != null ? start.center + new Vector2(0.5f, 0.5f) : new Vector2(g.W / 2f, g.H / 2f);
            cam.orthographicSize = Mathf.Clamp(3.6f / Mathf.Max(0.3f, cam.aspect), MinSize, MaxSize);
            basePos = new Vector3(c.x, c.y + 1.2f, -10);
            Clamp();
        }

        /// <summary>Zeigt die ganze freigeschaltete Farm (z. B. zum Nachtkampf).</summary>
        public void FocusFarm()
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var id in g.S.unlockedAreas)
            {
                var a = Balance.Area(id); if (a == null) continue;
                foreach (var r in a.rects) { min = Vector2.Min(min, new Vector2(r.x, r.y)); max = Vector2.Max(max, new Vector2(r.x + r.w, r.y + r.h)); }
            }
            if (min.x > max.x) return;
            min -= Vector2.one * 2f; max += Vector2.one * 2f;
            float size = Mathf.Max((max.y - min.y) / 2f, (max.x - min.x) / 2f / Mathf.Max(0.3f, cam.aspect));
            FocusOn((min + max) / 2f + new Vector2(0, 1.2f), Mathf.Max(size * 1.15f, cam.orthographicSize));
        }

        public void FocusOn(Vector2 world, float size = -1)
        {
            basePos = new Vector3(world.x, world.y, -10);
            if (size > 0) cam.orthographicSize = Mathf.Clamp(size, MinSize, MaxSize);
            Clamp();
        }

        public void Shake(float amount) { shakeAmt = Mathf.Max(shakeAmt, amount); }

        void Clamp()
        {
            float h = cam.orthographicSize, w = h * cam.aspect;
            float minX = -1.5f, maxX = g.W + 1.5f, minY = -2f, maxY = g.H + 5f;
            float cx = (maxX - minX) < 2 * w ? (minX + maxX) / 2f : Mathf.Clamp(basePos.x, minX + w, maxX - w);
            float cy = (maxY - minY) < 2 * h ? (minY + maxY) / 2f : Mathf.Clamp(basePos.y, minY + h, maxY - h);
            basePos = new Vector3(cx, cy, -10);
        }

        Vector3 World(Vector2 screen) { var p = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10)); p.z = 0; return p; }

        void LateUpdate()
        {
            HandleInput();
            Clamp();
            shakeAmt = Mathf.MoveTowards(shakeAmt, 0, Time.deltaTime * 0.6f);
            shakeOffset = shakeAmt > 0 ? (Vector3)(Random.insideUnitCircle * shakeAmt) : Vector3.zero;
            cam.transform.position = basePos + shakeOffset;
        }

        // ------------------------------------------------------------------ Eingabe

        void HandleInput()
        {
            if (Input.touchSupported && Input.touchCount > 0) { HandleTouches(); return; }
            lastTouchCount = 0;

            // Maus
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && !PointerOverUI(-1)) ZoomAt(Input.mousePosition, 1f - wheel * 0.1f);
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) lastScreen = Input.mousePosition;
                PanBy((Vector2)Input.mousePosition - lastScreen);
                lastScreen = Input.mousePosition;
            }
            if (Input.GetMouseButtonDown(0)) PointerDown(Input.mousePosition, -1);
            else if (Input.GetMouseButton(0) && down) PointerMove(Input.mousePosition);
            if (Input.GetMouseButtonUp(0) && down) PointerUp(Input.mousePosition);
        }

        void HandleTouches()
        {
            int n = Input.touchCount;
            if (n >= 2)
            {
                var t0 = Input.GetTouch(0); var t1 = Input.GetTouch(1);
                float dist = Vector2.Distance(t0.position, t1.position);
                Vector2 mid = (t0.position + t1.position) / 2f;
                if (lastTouchCount < 2) { lastPinch = dist; lastScreen = mid; CancelPointer(); }
                else
                {
                    if (lastPinch > 1f) ZoomAt(mid, lastPinch / Mathf.Max(1f, dist));
                    PanBy(mid - lastScreen);
                }
                lastPinch = dist; lastScreen = mid;
                lastTouchCount = n;
                return;
            }
            var t = Input.GetTouch(0);
            if (lastTouchCount >= 2) { lastTouchCount = n; lastScreen = t.position; return; } // nach Pinch nicht sofort tippen
            lastTouchCount = n;
            switch (t.phase)
            {
                case TouchPhase.Began: PointerDown(t.position, t.fingerId); break;
                case TouchPhase.Moved: case TouchPhase.Stationary: if (down) PointerMove(t.position); break;
                case TouchPhase.Ended: if (down) PointerUp(t.position); break;
                case TouchPhase.Canceled: CancelPointer(); break;
            }
        }

        static bool PointerOverUI(int id)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return id < 0 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(id);
        }

        void CancelPointer() { down = false; painting = false; panning = false; pendingAction = TileAction.None; pendingBuilding = null; }

        bool TileAt(Vector2 screen, out Vector2Int tile)
        {
            var w = World(screen);
            tile = new Vector2Int(Mathf.FloorToInt(w.x), Mathf.FloorToInt(w.y));
            return g.Tile(tile.x, tile.y) != null;
        }

        void PointerDown(Vector2 screen, int id)
        {
            down = true; painting = false; panning = false;
            overUI = PointerOverUI(id);
            downScreen = lastScreen = screen;
            pendingAction = TileAction.None; pendingBuilding = null;
            if (overUI || g.Sleeping || g.ModalCount > 0) return;

            var world = World(screen);
            if (LuckyBug.I != null && LuckyBug.I.TryCatch(world)) { down = false; return; }
            if (g.TapMonster(world)) { painting = true; lastTile = new Vector2Int(-99, -99); return; }

            pendingBuilding = FarmView.I.BuildingAt(world);
            if (pendingBuilding != null) return;

            if (!TileAt(screen, out var tile)) return;
            var act = g.Resolve(tile.x, tile.y);
            if (Game.IsPaintable(act) && string.IsNullOrEmpty(g.PlacingItem))
            {
                // Sofort ausfuehren: das Klicken soll sich direkt anfuehlen
                g.Perform(tile.x, tile.y, act);
                painting = true; lastTile = tile;
                return;
            }
            pendingAction = act; pendingTile = tile;
        }

        void PointerMove(Vector2 screen)
        {
            if (overUI) return;
            if (painting)
            {
                if (TileAt(screen, out var tile) && tile != lastTile)
                {
                    lastTile = tile;
                    var act = g.Resolve(tile.x, tile.y);
                    if (Game.IsPaintable(act)) g.Perform(tile.x, tile.y, act);
                }
                return;
            }
            float dpi = Screen.dpi > 0 ? Screen.dpi : 160f;
            if (!panning && Vector2.Distance(screen, downScreen) > dpi * 0.08f) { panning = true; pendingAction = TileAction.None; pendingBuilding = null; }
            if (panning) PanBy(screen - lastScreen);
            lastScreen = screen;
        }

        void PointerUp(Vector2 screen)
        {
            down = false;
            if (overUI || panning || painting) { painting = false; panning = false; return; }
            if (pendingBuilding != null) { g.TapBuilding(pendingBuilding); pendingBuilding = null; return; }
            if (pendingAction != TileAction.None) g.Perform(pendingTile.x, pendingTile.y, pendingAction);
            pendingAction = TileAction.None;
        }

        void PanBy(Vector2 screenDelta)
        {
            float unitsPerPixel = cam.orthographicSize * 2f / Screen.height;
            basePos -= new Vector3(screenDelta.x, screenDelta.y, 0) * unitsPerPixel;
        }

        void ZoomAt(Vector2 screen, float factor)
        {
            var before = World(screen);
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, MinSize, MaxSize);
            cam.transform.position = basePos;
            var after = World(screen);
            basePos += before - after;
        }

        public Vector2 WorldToScreen(Vector3 world) => cam.WorldToScreenPoint(world);
    }
}
