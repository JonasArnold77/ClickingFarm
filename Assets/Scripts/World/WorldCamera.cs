using UnityEngine;
using UnityEngine.EventSystems;

namespace SeedClicker
{
    /// <summary>Kamera fuer die freie Welt: Wischen verschiebt, zwei Finger / Mausrad zoomen.</summary>
    public class WorldCamera : MonoBehaviour
    {
        public static WorldCamera I { get; private set; }

        Camera cam;
        Vector3 basePos;
        bool dragging;
        Vector2 lastScreen;
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
            cam.orthographicSize = Mathf.Clamp(3.6f / Mathf.Max(0.3f, cam.aspect), MinSize, MaxSize);
            basePos = new Vector3(WorldBuilder.HousePos.x, WorldBuilder.HousePos.y - 1.2f, -10);
        }

        void LateUpdate()
        {
            if (Input.touchCount > 0) Touches();
            else Mouse();
            Clamp();
            cam.transform.position = basePos;
        }

        void Mouse()
        {
            lastTouchCount = 0;
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) ZoomAt(Input.mousePosition, 1f - wheel * 0.1f);
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                dragging = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
                lastScreen = Input.mousePosition;
            }
            if (dragging && (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2)))
            {
                PanBy((Vector2)Input.mousePosition - lastScreen);
                lastScreen = Input.mousePosition;
            }
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetMouseButton(2)) dragging = false;
        }

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
                lastPinch = dist; lastScreen = mid; lastTouchCount = n;
                return;
            }
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began || lastTouchCount != 1) lastScreen = t.position;
            else PanBy(t.position - lastScreen);
            lastScreen = t.position;
            lastTouchCount = n;
        }

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
            basePos.y = h * 2 >= hy * 2 ? 0 : Mathf.Clamp(basePos.y, -hy + h, hy - h);
            basePos.z = -10;
        }
    }
}
