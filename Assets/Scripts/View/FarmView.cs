using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SeedClicker
{
    /// <summary>Stellt die Farm dar und kuemmert sich um das "Juice"-Feedback in der Welt.</summary>
    public class FarmView : MonoBehaviour
    {
        public static FarmView I { get; private set; }

        class TileView
        {
            public SpriteRenderer ground, soil, overlay, wet, obj, shadow;
            public Transform objT;
            public float punch, shake, squash, popIn = 1f, bobPhase;
            public bool ripe;
            public string objKey = "";
        }

        Game g;
        TileView[] tiles;
        readonly HashSet<int> animating = new HashSet<int>();
        readonly List<int> ripeTiles = new List<int>();
        readonly Dictionary<string, SpriteRenderer> signs = new Dictionary<string, SpriteRenderer>();
        readonly List<GameObject> greenhouseFrame = new List<GameObject>();

        public SpriteRenderer House { get; private set; }
        public SpriteRenderer Bin { get; private set; }
        SpriteRenderer houseScaffold, binScaffold;
        float housePunch, binPunch;
        Light2D globalLight, windowLight;
        float waterAnim;
        int waterFrame;

        public Vector3 HousePos => new Vector3(g.W / 2f - 2.5f, g.H + 0.4f, 0);
        public Vector3 BinPos => new Vector3(g.W / 2f + 2.6f, g.H + 0.3f, 0);

        public static int Order(float y) => Mathf.RoundToInt(1000 - y * 10);
        public static Vector3 TileCenter(int x, int y) => new Vector3(x + 0.5f, y + 0.5f, 0);

        void Awake()
        {
            I = this;
            g = Game.I;
            BuildBackground();
            BuildTiles();
            BuildBuildings();
            SetupLights();
            g.TileChanged += (x, y) =>
            {
                RefreshTile(x, y);
                // Beete verbinden sich mit den Nachbarn
                if (x > 0) RefreshTile(x - 1, y); if (x < g.W - 1) RefreshTile(x + 1, y);
                if (y > 0) RefreshTile(x, y - 1); if (y < g.H - 1) RefreshTile(x, y + 1);
            };
            g.AllTilesChanged += RefreshAll;
            g.FeedbackRaised += OnFeedback;
            g.StateChanged += RefreshBuildings;
            RefreshAll();
        }

        // ------------------------------------------------------------------ Aufbau

        SpriteRenderer MakeSR(string name, Transform parent, Sprite sprite, Vector3 pos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite; sr.sortingOrder = order;
            return sr;
        }

        void BuildBackground()
        {
            var bg = new GameObject("Background").transform;
            bg.SetParent(transform, false);
            // grosse Wiese um die Farm
            var meadow = MakeSR("Meadow", bg, Art.Get("ground:grass:1"), new Vector3(g.W / 2f, g.H / 2f + 2, 0), -2000);
            meadow.drawMode = SpriteDrawMode.Tiled; meadow.size = new Vector2(g.W + 30, g.H + 34);
            meadow.color = new Color(0.82f, 0.92f, 0.82f);
            // Weg vom Haus zur Farm
            for (int y = g.H; y < g.H + 3; y++)
                for (int x = g.W / 2 - 1; x <= g.W / 2; x++)
                    MakeSR("Path", bg, Art.Get("ground:path"), TileCenter(x, y), -1500);
            for (int x = 1; x < g.W - 1; x++) MakeSR("Path", bg, Art.Get("ground:path"), TileCenter(x, g.H), -1500);

            // Waldrand aus Baeumen
            var r = new System.Random(77);
            for (float y = -4; y < g.H + 8; y += 1.3f)
            {
                PlaceTree(bg, r, -1.2f - (float)r.NextDouble() * 2.5f, y);
                PlaceTree(bg, r, g.W + 1.0f + (float)r.NextDouble() * 2.5f, y);
                if (r.NextDouble() < 0.6) PlaceTree(bg, r, -4f - (float)r.NextDouble() * 2f, y);
                if (r.NextDouble() < 0.6) PlaceTree(bg, r, g.W + 4f + (float)r.NextDouble() * 2f, y);
            }
            for (float x = -2; x < g.W + 2; x += 1.2f)
            {
                PlaceTree(bg, r, x, -1.6f - (float)r.NextDouble() * 1.5f);
                PlaceTree(bg, r, x, -4.2f - (float)r.NextDouble() * 1.5f);
                PlaceTree(bg, r, x + 0.6f, g.H + 4.5f + (float)r.NextDouble() * 2f);
            }
            // Blumen und Buesche am Rand
            for (int i = 0; i < 22; i++)
            {
                float x = (float)r.NextDouble() * (g.W + 2) - 1, y = g.H + 0.6f + (float)r.NextDouble() * 3.4f;
                if (Mathf.Abs(x - HousePos.x) < 2.2f || Mathf.Abs(x - BinPos.x) < 1.4f || Mathf.Abs(x - g.W / 2f) < 1.2f) continue;
                if (r.NextDouble() < 0.5) MakeSR("Flowers", bg, Art.Get("decor:flowers:" + r.Next(0, 4)), new Vector3(x, y, 0), -1400);
                else MakeSR("Bush", bg, Art.Get("decor:bush:" + r.Next(0, 3)), new Vector3(x, y, 0), Order(y));
            }
            MakeSR("Well", bg, Art.Get("decor:well"), new Vector3(g.W - 1.6f, g.H + 1.1f, 0), Order(g.H + 1.1f));
        }

        void PlaceTree(Transform parent, System.Random r, float x, float y)
        {
            string key = r.NextDouble() < 0.35 ? "decor:pine:" + r.Next(0, 3) : "decor:tree:" + r.Next(0, 6);
            var sr = MakeSR("Tree", parent, Art.Get(key), new Vector3(x, y, 0), Order(y));
            float s = 0.9f + (float)r.NextDouble() * 0.35f;
            sr.transform.localScale = new Vector3(r.NextDouble() < 0.5 ? s : -s, s, 1);
            Shadow(parent, new Vector3(x + 0.45f, y + 0.05f, 0), 2.6f * s, 0.9f * s, 0.3f);
        }

        /// <summary>Weicher Schlagschatten nach rechts unten (Licht kommt von oben links).</summary>
        SpriteRenderer Shadow(Transform parent, Vector3 pos, float w, float h, float alpha)
        {
            var sh = MakeSR("Shadow", parent, Art.Get("soft"), pos, -995);
            sh.color = new Color(0.05f, 0.1f, 0.12f, alpha);
            sh.transform.localScale = new Vector3(w, h, 1);
            return sh;
        }

        void BuildTiles()
        {
            var root = new GameObject("Tiles").transform;
            root.SetParent(transform, false);
            tiles = new TileView[g.W * g.H];
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    var tv = new TileView();
                    var pos = TileCenter(x, y);
                    tv.ground = MakeSR($"T{x}_{y}", root, null, pos, -1000);
                    tv.soil = MakeSR("soil", tv.ground.transform, null, pos, -999);
                    tv.overlay = MakeSR("ov", tv.ground.transform, null, pos, -998);
                    tv.wet = MakeSR("wet", tv.ground.transform, Art.Get("wet"), pos, -997);
                    tv.shadow = Shadow(tv.ground.transform, pos + new Vector3(0.12f, -0.3f, 0), 0.9f, 0.4f, 0.28f);
                    tv.obj = MakeSR("obj", tv.ground.transform, null, pos + new Vector3(0, -0.42f, 0), Order(y));
                    tv.objT = tv.obj.transform;
                    tv.bobPhase = Random.value * 10f;
                    tiles[y * g.W + x] = tv;
                }
            foreach (var a in g.B.areas)
            {
                var sign = MakeSR("Sign_" + a.id, root, Art.Get("sign"), new Vector3(a.center.x + 0.5f, a.center.y + 0.1f, 0), Order(a.center.y) + 5);
                signs[a.id] = sign;
            }
        }

        void BuildBuildings()
        {
            var root = new GameObject("Buildings").transform;
            root.SetParent(transform, false);
            Shadow(root, HousePos + new Vector3(0.5f, 0.1f, 0), 6f, 1.2f, 0.32f);
            Shadow(root, BinPos + new Vector3(0.2f, 0.05f, 0), 2f, 0.6f, 0.3f);
            House = MakeSR("House", root, null, HousePos, Order(HousePos.y));
            Bin = MakeSR("Bin", root, null, BinPos, Order(BinPos.y));
            houseScaffold = MakeSR("Scaffold", House.transform, Art.Get("scaffold"), HousePos, Order(HousePos.y) + 1);
            binScaffold = MakeSR("Scaffold", Bin.transform, Art.Get("scaffold"), BinPos, Order(BinPos.y) + 1);
            binScaffold.transform.localScale = new Vector3(0.6f, 0.6f, 1);
            RefreshBuildings();
        }

        void SetupLights()
        {
            globalLight = null;
            foreach (var l in FindObjectsOfType<Light2D>()) if (l.lightType == Light2D.LightType.Global) { globalLight = l; break; }
            if (globalLight == null)
            {
                var go = new GameObject("Global Light 2D");
                globalLight = go.AddComponent<Light2D>();
                globalLight.lightType = Light2D.LightType.Global;
            }
            var wgo = new GameObject("WindowLight");
            wgo.transform.SetParent(transform, false);
            wgo.transform.position = HousePos + new Vector3(0, 1.0f, 0);
            windowLight = wgo.AddComponent<Light2D>();
            windowLight.lightType = Light2D.LightType.Point;
            windowLight.pointLightOuterRadius = 5f;
            windowLight.pointLightInnerRadius = 0.5f;
            windowLight.color = new Color(1f, 0.78f, 0.45f);
            windowLight.intensity = 0f;
        }

        // ------------------------------------------------------------------ Aktualisieren

        public void RefreshAll()
        {
            for (int y = 0; y < g.H; y++) for (int x = 0; x < g.W; x++) RefreshTile(x, y);
            foreach (var kv in signs) kv.Value.enabled = !g.IsAreaUnlocked(Balance.Area(kv.Key)) && Balance.Area(kv.Key).rects.Length > 0;
            BuildGreenhouseFrame();
            BuildFences();
            RefreshBuildings();
        }

        void RefreshTile(int x, int y)
        {
            var tv = tiles[y * g.W + x];
            var t = g.Tile(x, y);
            var area = Balance.AreaAt(x, y);
            bool unlocked = area != null && g.IsAreaUnlocked(area);
            bool water = area != null && area.water;

            string groundKey;
            if (water && unlocked) groundKey = "ground:water:" + waterFrame;
            else if (area != null && area.id == "greenhouse" && unlocked) groundKey = "ground:floor";
            else groundKey = "ground:grass:" + ((x * 7 + y * 13) % 4);
            tv.ground.sprite = Art.Get(groundKey);

            Color tint = area != null ? Balance.Hex(area.groundTint) : Color.white;
            if (!unlocked) tint = new Color(tint.r * 0.52f, tint.g * 0.6f, tint.b * 0.62f);
            tv.ground.color = tint;
            if (t.tilled)
            {
                int mask = (Tilled(x, y + 1) ? 1 : 0) | (Tilled(x + 1, y) ? 2 : 0) | (Tilled(x, y - 1) ? 4 : 0) | (Tilled(x - 1, y) ? 8 : 0);
                tv.soil.sprite = Art.Get("soil:" + mask + ":" + (g.IsWatered(x, y) ? "1" : "0"));
                tv.soil.color = tint;
            }
            else tv.soil.sprite = null;

            tv.overlay.sprite = t.fert > 0 ? Art.Get("fert:" + t.fert) : null;
            tv.wet.enabled = t.tilled && g.IsSprinkled(x, y);

            string key = "";
            if (t.HasObstacle) key = "ob:" + t.obstacle;
            else if (t.HasItem) key = "item:" + t.item;
            else if (t.HasCrop) key = "crop:" + t.crop + ":" + g.Stage(t);
            if (key != tv.objKey)
            {
                tv.objKey = key;
                tv.obj.sprite = key.Length > 0 ? Art.Get(key) : null;
                // Schattengroesse je nach Objekt
                float sw = key.Length == 0 ? 0 : key == "ob:boulder" ? 1.25f : key == "ob:stump" ? 1.0f : key.StartsWith("ob:") ? 0.8f : key.Contains(":3") ? 0.95f : 0.65f;
                tv.shadow.enabled = sw > 0;
                tv.shadow.transform.localScale = new Vector3(sw, sw * 0.42f, 1);
                // Hindernisse und Gegenstaende sind zentriert, Pflanzen stehen auf dem Boden
                bool bottom = key.StartsWith("crop:") || key == "item:scarecrow" || key.Contains("beehive") || key.Contains("harvester");
                tv.objT.position = TileCenter(x, y) + new Vector3(0, bottom ? -0.42f : 0f, 0);
            }
            tv.obj.color = unlocked ? Color.white : new Color(0.5f, 0.56f, 0.6f);

            bool ripe = t.HasCrop && g.IsRipe(t);
            int idx = y * g.W + x;
            if (ripe != tv.ripe) { tv.ripe = ripe; if (ripe) ripeTiles.Add(idx); else ripeTiles.Remove(idx); }
            if (!ripe) { tv.objT.localScale = Vector3.one; }
        }

        bool Tilled(int x, int y) { var t = g.Tile(x, y); return t != null && t.tilled; }

        readonly List<GameObject> fences = new List<GameObject>();

        /// <summary>Holzzaun um die gesamte freigeschaltete Flaeche.</summary>
        void BuildFences()
        {
            foreach (var go in fences) Destroy(go);
            fences.Clear();
            bool In(int x, int y) => g.IsUsable(x, y) || (Balance.AreaAt(x, y) is AreaDef a && a.water && g.IsAreaUnlocked(a));
            int gateX = g.W / 2 - 1;
            for (int y = 0; y < g.H; y++)
                for (int x = 0; x < g.W; x++)
                {
                    if (!In(x, y)) continue;
                    if (!In(x, y + 1) && !(x >= gateX && x <= gateX + 1 && y + 1 >= g.H)) AddFence("h", new Vector3(x, y + 1 - 0.1f, 0));
                    if (!In(x, y - 1)) AddFence("h", new Vector3(x, y - 0.2f, 0));
                    if (!In(x - 1, y)) AddFence("v", new Vector3(x + 0.05f, y - 0.2f, 0));
                    if (!In(x + 1, y)) AddFence("v", new Vector3(x + 0.95f, y - 0.2f, 0));
                }
        }

        void AddFence(string kind, Vector3 pos)
        {
            var sr = MakeSR("Fence", transform, Art.Get("fence:" + kind), pos + (kind == "h" ? new Vector3(0.5f, 0, 0) : Vector3.zero), Order(pos.y) + 1);
            if (kind == "v") sr.transform.localScale = new Vector3(1, 1.05f, 1);
            fences.Add(sr.gameObject);
        }

        void BuildGreenhouseFrame()
        {
            foreach (var go in greenhouseFrame) Destroy(go);
            greenhouseFrame.Clear();
            var a = Balance.Area("greenhouse");
            if (a == null || !g.IsAreaUnlocked(a)) return;
            var col = new Color(0.75f, 0.92f, 1f, 0.85f);
            foreach (var r in a.rects)
            {
                void Bar(Vector3 c, Vector2 size)
                {
                    var sr = MakeSR("Glass", transform, Art.Get("white"), c, Order(c.y) + 3);
                    sr.drawMode = SpriteDrawMode.Sliced; sr.size = size; sr.color = col;
                    greenhouseFrame.Add(sr.gameObject);
                }
                Bar(new Vector3(r.x + r.w / 2f, r.y + r.h, 0), new Vector2(r.w + 0.2f, 0.18f));
                Bar(new Vector3(r.x + r.w / 2f, r.y, 0), new Vector2(r.w + 0.2f, 0.12f));
                Bar(new Vector3(r.x, r.y + r.h / 2f, 0), new Vector2(0.15f, r.h));
                Bar(new Vector3(r.x + r.w, r.y + r.h / 2f, 0), new Vector2(0.15f, r.h));
                for (int i = 2; i < r.w; i += 2) Bar(new Vector3(r.x + i, r.y + r.h / 2f, 0), new Vector2(0.06f, r.h));
            }
        }

        void RefreshBuildings()
        {
            if (House == null) return;
            House.sprite = Art.Get("house:" + g.S.houseLevel);
            Bin.sprite = Art.Get("bin:" + g.S.binLevel);
            houseScaffold.enabled = g.S.construction.Active && g.S.construction.building == "house";
            binScaffold.enabled = g.S.construction.Active && g.S.construction.building == "bin";
        }

        /// <summary>Welches Gebaeude liegt unter dem Weltpunkt?</summary>
        public string BuildingAt(Vector3 world)
        {
            if (Hit(House, world)) return "house";
            if (Hit(Bin, world)) return "bin";
            return null;
        }

        static bool Hit(SpriteRenderer sr, Vector3 p)
        {
            if (sr == null || sr.sprite == null) return false;
            var b = sr.bounds; b.Expand(0.3f);
            return b.Contains(new Vector3(p.x, p.y, b.center.z));
        }

        // ------------------------------------------------------------------ Feedback

        public void Kick(int x, int y, float punch = 0, float shake = 0, float squash = 0)
        {
            if (x < 0 || y < 0) return;
            int i = y * g.W + x;
            var tv = tiles[i];
            tv.punch = Mathf.Max(tv.punch, punch); tv.shake = Mathf.Max(tv.shake, shake); tv.squash = Mathf.Max(tv.squash, squash);
            animating.Add(i);
        }

        static Color ObstacleColor(string id) => id switch
        {
            "grass" => new Color(0.5f, 0.9f, 0.3f),
            "twig" or "stump" => new Color(0.75f, 0.5f, 0.28f),
            _ => new Color(0.7f, 0.74f, 0.8f)
        };

        void OnFeedback(Feedback f)
        {
            var P = Particles.I;
            Vector3 pos = f.x >= 0 ? TileCenter(f.x, f.y) : Vector3.zero;
            switch (f.type)
            {
                case FxType.Hit:
                    Kick(f.x, f.y, shake: 1f, squash: 0.5f);
                    P.Burst(pos, "square", ObstacleColor(f.id), 4, 2.5f, 0.4f, 0.8f);
                    Sfx.Play(f.id == "stone" || f.id == "boulder" ? "rock" : "chop", Random.Range(0.95f, 1.1f));
                    Haptics.Tap(8);
                    break;
                case FxType.Break:
                    {
                        var c = ObstacleColor(f.id);
                        P.Burst(pos, f.id == "grass" ? "leaf" : "square", c, 10, 4f, 0.6f, 1.1f);
                        P.Burst(pos, "spark", Color.white, 4, 3f, 0.4f, 1f, 0f, 0f);
                        P.Ring(pos, new Color(1, 1, 1, 0.7f), 0.9f);
                        Sfx.Play(f.id == "grass" ? "pop" : "break", Random.Range(0.95f, 1.1f));
                        Haptics.Tap(18);
                        CameraController.I?.Shake(f.id == "boulder" || f.id == "stump" ? 0.12f : 0.03f);
                        break;
                    }
                case FxType.Till:
                    P.Burst(pos, "square", Art.Soil, 6, 2.2f, 0.45f, 0.9f);
                    Kick(f.x, f.y, punch: 0.3f);
                    Sfx.Play("dig", Random.Range(0.9f, 1.1f));
                    Haptics.Tap(10);
                    break;
                case FxType.Sow:
                    tiles[f.y * g.W + f.x].popIn = 0f; animating.Add(f.y * g.W + f.x);
                    P.Burst(pos, "spark", new Color(1f, 0.9f, 0.6f), 5, 1.8f, 0.4f, 0.8f);
                    Sfx.Play("sow", Random.Range(0.95f, 1.15f));
                    Haptics.Tap(8);
                    break;
                case FxType.Water:
                    for (int i = 0; i < 7; i++)
                        P.Spawn(pos + new Vector3(Random.Range(-0.4f, 0.4f), 0.7f + Random.Range(0, 0.4f), 0), "drop", new Color(0.55f, 0.85f, 1f), new Vector3(Random.Range(-0.6f, 0.6f), -Random.Range(1f, 3f), 0), 0.45f, 1f, 0.7f, 6f);
                    P.Ring(pos + new Vector3(0, -0.1f, 0), new Color(0.7f, 0.9f, 1f, 0.8f), 0.8f, 0.4f);
                    Kick(f.x, f.y, squash: 0.5f);
                    Sfx.Play("water", Random.Range(0.95f, 1.1f), 0.8f);
                    Haptics.Tap(8);
                    break;
                case FxType.Grow:
                    {
                        float combo = g.ComboMult;
                        Kick(f.x, f.y, squash: f.strength >= 1.5f ? 1.4f : 0.9f);
                        var leaf = Balance.Crop(f.id) != null ? Balance.Hex(Balance.Crop(f.id).leafColor) : Color.green;
                        P.Burst(pos + new Vector3(0, 0.1f, 0), "leaf", leaf, f.strength >= 1.5f ? 7 : 2, 2.2f, 0.5f, 0.9f, 4f);
                        if (combo > 1.01f) P.Burst(pos + new Vector3(0, 0.3f, 0), "spark", Color.Lerp(Color.white, new Color(1f, 0.85f, 0.2f), (combo - 1) / 4f), 2, 2.5f, 0.35f, 0.9f, 0f);
                        if (f.strength >= 1.5f) { P.Ring(pos, new Color(0.7f, 1f, 0.6f, 0.9f), 1.1f); Sfx.Play("pop", 1.2f); }
                        Sfx.Play("grow", Mathf.Min(2.2f, 0.9f + (combo - 1f) * 0.18f + Random.Range(-0.03f, 0.03f)), 0.8f);
                        Haptics.Tap(5);
                        break;
                    }
                case FxType.Crit:
                    Kick(f.x, f.y, squash: 1.8f, punch: 0.4f);
                    P.Burst(pos + new Vector3(0, 0.3f, 0), "star", new Color(1f, 0.9f, 0.3f), 10, 4.5f, 0.6f, 1.1f, 2f);
                    P.Ring(pos, new Color(1f, 0.95f, 0.5f), 1.6f, 0.45f);
                    Sfx.Play("crit");
                    CameraController.I?.Shake(0.06f);
                    Haptics.Tap(25);
                    break;
                case FxType.Ripe:
                    Kick(f.x, f.y, punch: 0.6f * f.strength, squash: 1.2f * f.strength);
                    P.Burst(pos + new Vector3(0, 0.4f, 0), "star", new Color(1f, 0.95f, 0.55f), f.strength >= 1 ? 8 : 3, 3f, 0.55f, 1f, 1f);
                    if (f.strength >= 1) { P.Ring(pos, new Color(1, 1, 0.7f, 0.9f), 1.4f, 0.4f); Sfx.Play("pop", 1.5f); Haptics.Tap(15); }
                    break;
                case FxType.Harvest:
                    {
                        var c = Balance.Crop(f.id);
                        var col = c != null ? Balance.Hex(c.fruitColor) : Color.yellow;
                        P.Burst(pos + new Vector3(0, 0.3f, 0), "square", col, 8, 3.5f, 0.6f, 1f);
                        P.Burst(pos + new Vector3(0, 0.3f, 0), "leaf", c != null ? Balance.Hex(c.leafColor) : Color.green, 5, 3f, 0.6f, 1f);
                        P.Ring(pos, new Color(1, 1, 1, 0.8f), 1f);
                        // Das Herausspringen und Wegfliegen zur Kiste uebernimmt die UI (FlyToBin)
                        Kick(f.x, f.y, punch: 0.5f);
                        Sfx.Play("harvest", Random.Range(0.97f, 1.08f), 0.8f);
                        Haptics.Tap(20);
                        break;
                    }
                case FxType.Place:
                    tiles[f.y * g.W + f.x].popIn = 0f; animating.Add(f.y * g.W + f.x);
                    P.Burst(pos, "square", new Color(0.9f, 0.85f, 0.7f), 10, 3f, 0.5f, 1f, 2f, 0.5f);
                    P.Ring(pos, Color.white, 1.4f);
                    Sfx.Play("build"); Sfx.Play("unlock", 1.2f, 0.6f);
                    Haptics.Tap(25);
                    break;
                case FxType.Fertilize:
                    P.Burst(pos, f.id == "deluxe_fertilizer" ? "star" : "spark", f.id == "deluxe_fertilizer" ? new Color(0.8f, 0.6f, 1f) : new Color(0.6f, 1f, 0.4f), 10, 2.5f, 0.6f, 1f, 0f);
                    Kick(f.x, f.y, squash: 0.6f);
                    Sfx.Play("sow", 1.3f);
                    break;
                case FxType.Build:
                    {
                        bool house = f.id != "bin";
                        var sr = house ? House : Bin;
                        if (house) housePunch = 1f; else binPunch = 1f;
                        var bp = sr.bounds.center;
                        P.Burst(bp + new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.5f, 0.5f), 0), "square", new Color(0.9f, 0.8f, 0.6f), 8, 3.5f, 0.5f, 1f);
                        P.Burst(bp, "spark", Color.white, 3, 3f, 0.3f, 1f, 0);
                        Sfx.Play("build", Random.Range(0.9f, 1.15f));
                        CameraController.I?.Shake(0.05f);
                        Haptics.Tap(15);
                        break;
                    }
                case FxType.Error:
                    if (f.x >= 0) Kick(f.x, f.y, shake: 1.5f);
                    Sfx.Play("error");
                    Haptics.Tap(30);
                    break;
                case FxType.Coins:
                    Sfx.Play("coin");
                    break;
                case FxType.Push:
                    Kick(f.x, f.y, punch: 0.35f, squash: f.strength >= 1.5f ? 1.3f : 0.8f);
                    P.Burst(pos + new Vector3(0, 0.4f, 0), "spark", BattleView.ElementColor(Balance.Crop(f.id) != null ? Balance.Crop(f.id).defense.element : ""), 4, 2.5f, 0.35f, 0.9f, 0f);
                    if (!g.BattleActive) Sfx.Play("pop", 1.4f, 0.6f);
                    Haptics.Tap(10);
                    break;
                case FxType.Eaten:
                    Kick(f.x, f.y, shake: f.strength >= 1f ? 1.5f : 0.8f);
                    P.Burst(pos, "leaf", new Color(0.5f, 0.85f, 0.3f), f.strength >= 1f ? 10 : 3, 2.5f, 0.5f, 0.9f);
                    Sfx.Play("chop", 0.7f, 0.5f);
                    break;
                case FxType.Wilt:
                    Kick(f.x, f.y, shake: 1.5f, squash: 1f);
                    P.Burst(pos, "leaf", new Color(0.6f, 0.5f, 0.25f), 12, 3f, 0.7f, 1f);
                    Sfx.Play("error", 0.8f);
                    break;
                case FxType.Honey:
                    P.Burst(pos + new Vector3(0, 0.6f, 0), "spark", new Color(1f, 0.75f, 0.2f), 5, 1.5f, 0.6f, 1f, 0f);
                    Kick(f.x, f.y, squash: 0.5f);
                    break;
            }
        }

        // ------------------------------------------------------------------ Animation pro Frame

        readonly List<int> doneBuf = new List<int>();

        void Update()
        {
            float dt = Time.deltaTime;
            doneBuf.Clear();
            foreach (int i in animating)
            {
                var tv = tiles[i];
                tv.punch = Mathf.MoveTowards(tv.punch, 0, dt * 4f);
                tv.shake = Mathf.MoveTowards(tv.shake, 0, dt * 6f);
                tv.squash = Mathf.MoveTowards(tv.squash, 0, dt * 5f);
                tv.popIn = Mathf.MoveTowards(tv.popIn, 1f, dt * 4f);
                ApplyTransform(i, tv);
                if (tv.punch <= 0 && tv.shake <= 0 && tv.squash <= 0 && tv.popIn >= 1f) doneBuf.Add(i);
            }
            foreach (int i in doneBuf) { animating.Remove(i); ApplyTransform(i, tiles[i]); }

            // Erntereife Pflanzen wippen und glitzern
            float tm = Time.time;
            foreach (int i in ripeTiles)
            {
                var tv = tiles[i];
                if (!animating.Contains(i))
                {
                    float s = Mathf.Sin(tm * 5f + tv.bobPhase);
                    tv.objT.localScale = new Vector3(1f - s * 0.04f, 1f + s * 0.07f, 1);
                    tv.objT.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(tm * 2.5f + tv.bobPhase) * 3f);
                }
                if (Random.value < dt * 1.1f)
                {
                    var p = tv.objT.position + new Vector3(Random.Range(-0.4f, 0.4f), Random.Range(0.3f, 1.0f), 0);
                    Particles.I.Spawn(p, "star", new Color(1f, 1f, 0.75f), new Vector3(0, 0.4f, 0), 0.7f, 0.6f, 0f, 0f, 90f);
                }
            }

            // Gebaeude-Hammerschlag
            housePunch = Mathf.MoveTowards(housePunch, 0, dt * 5f);
            binPunch = Mathf.MoveTowards(binPunch, 0, dt * 5f);
            if (House != null)
            {
                House.transform.localScale = new Vector3(1 + housePunch * 0.06f, 1 - housePunch * 0.08f, 1);
                Bin.transform.localScale = new Vector3(1 + binPunch * 0.1f, 1 - binPunch * 0.12f, 1) * (1 + Mathf.Max(0, Mathf.Sin(tm * 3f)) * (g.S.binValue > 0 ? 0.03f : 0f));
            }

            // Wasser animieren
            waterAnim += dt;
            if (waterAnim > 0.6f)
            {
                waterAnim = 0; waterFrame = 1 - waterFrame;
                var pond = Balance.Area("pond");
                if (pond != null && g.IsAreaUnlocked(pond))
                    foreach (var r in pond.rects)
                        for (int y = r.y; y < r.y + r.h; y++) for (int x = r.x; x < r.x + r.w; x++) if (g.Tile(x, y) != null) tiles[y * g.W + x].ground.sprite = Art.Get("ground:water:" + ((waterFrame + x + y) % 2));
            }

            UpdateLight();
            Ambient(dt);
        }

        float ambientT;

        /// <summary>Schwebende Pollen und Pusteblumen-Samen fuer Atmosphaere.</summary>
        void Ambient(float dt)
        {
            ambientT -= dt;
            if (ambientT > 0) return;
            ambientT = 0.18f;
            var cam = Camera.main;
            if (cam == null) return;
            float h = cam.orthographicSize, w = h * cam.aspect;
            var p = cam.transform.position + new Vector3(Random.Range(-w, w), Random.Range(-h, h), 0);
            p.z = 0;
            bool night = g.Hour >= 20f;
            var col = night ? new Color(0.75f, 1f, 0.55f, 0.9f) : new Color(1f, 0.98f, 0.85f, 0.75f);
            Particles.I.Spawn(p, night ? "spark" : "square", col, new Vector3(Random.Range(0.1f, 0.35f), Random.Range(-0.05f, 0.15f), 0), Random.Range(3f, 5f), night ? 0.9f : 0.45f, 0.1f, -0.02f, 20f);
        }

        void ApplyTransform(int i, TileView tv)
        {
            float sq = Mathf.Sin(tv.squash * Mathf.PI * 1.5f) * tv.squash * 0.25f;
            float pop = EaseOutBack(tv.popIn);
            float sx = (1f + sq * 0.8f + tv.punch * 0.25f) * pop, sy = (1f - sq + tv.punch * 0.35f) * pop;
            tv.objT.localScale = new Vector3(sx, sy, 1);
            float sh = tv.shake > 0 ? Mathf.Sin(Time.time * 70f) * tv.shake * 0.08f : 0f;
            int x = i % g.W, y = i / g.W;
            bool bottom = tv.objKey.StartsWith("crop:") || tv.objKey == "item:scarecrow" || tv.objKey.Contains("beehive") || tv.objKey.Contains("harvester");
            tv.objT.position = TileCenter(x, y) + new Vector3(sh, (bottom ? -0.42f : 0f) + tv.punch * 0.25f, 0);
            tv.objT.localRotation = Quaternion.Euler(0, 0, sh * 40f);
        }

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1;
            return 1 + c3 * Mathf.Pow(t - 1, 3) + c1 * Mathf.Pow(t - 1, 2);
        }

        // ------------------------------------------------------------------ Tag/Nacht

        static readonly (float h, Color c, float i)[] sky =
        {
            (6f,  new Color(1.0f, 0.8f, 0.68f), 0.85f),
            (9f,  new Color(1.0f, 0.97f, 0.88f), 1.0f),
            (16f, new Color(1.0f, 0.95f, 0.84f), 1.0f),
            (19f, new Color(1.0f, 0.72f, 0.5f),  0.9f),
            (21f, new Color(0.55f, 0.55f, 0.95f), 0.62f),
            (24f, new Color(0.38f, 0.42f, 0.85f), 0.48f),
        };

        void UpdateLight()
        {
            if (globalLight == null) return;
            float h = g.Hour;
            Color col = sky[0].c; float inten = sky[0].i;
            for (int k = 0; k < sky.Length - 1; k++)
                if (h >= sky[k].h && h <= sky[k + 1].h)
                {
                    float t = Mathf.InverseLerp(sky[k].h, sky[k + 1].h, h);
                    col = Color.Lerp(sky[k].c, sky[k + 1].c, t); inten = Mathf.Lerp(sky[k].i, sky[k + 1].i, t);
                }
            if (h > sky[sky.Length - 1].h) { col = sky[sky.Length - 1].c; inten = sky[sky.Length - 1].i; }
            globalLight.color = col;
            globalLight.intensity = inten;
            windowLight.intensity = Mathf.Clamp01((h - 18.5f) / 2f) * 1.2f;
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = Color.Lerp(new Color(0.18f, 0.42f, 0.22f), new Color(0.08f, 0.12f, 0.25f), 1 - inten);
        }
    }
}
