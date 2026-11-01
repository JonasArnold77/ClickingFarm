using System.Collections.Generic;
using UnityEngine;

namespace SeedClicker
{
    /// <summary>
    /// Tore in Mauern: Im Mauer-Modus zweimal auf ein Mauerstueck tippen macht daraus ein Tor (Holz- oder Steinbogen mit
    /// beschlagener Tuer), zweimal auf ein Tor baut es ab. Ein Tor haelt mehr aus als eine Mauer (Zombies schlagen auch
    /// darauf ein), traegt aber keine Pflanze und kein Pult. Knollis laufen nicht mehr durch Mauern – sie suchen sich
    /// ihren Weg und gehen durch Tore. Ist ihr Ziel ganz eingemauert, bleiben sie stehen und rufen nach einem Tor.
    /// </summary>
    public partial class WorldBuilder
    {
        static Sprite woodGateSprite, stoneGateSprite;
        int wallVersion;   // steigt bei jeder Mauer-Aenderung – Knolli-Wege neu berechnen
        const float GateHpMult = 1.5f;

        public bool HasGate(Cell c) => walls.TryGetValue(c, out var v) && v.st.gate;
        bool Blocks(Cell c) => walls.TryGetValue(c, out var v) && !v.st.gate;

        static Sprite WallLook(WallState st) => st.gate ? (st.stone ? StoneGateSprite : WoodGateSprite) : st.stone ? StoneWallSprite : WallSprite;

        /// <summary>Mauer zum Tor machen (nur leer, ohne Pflanze und Pult).</summary>
        void MakeGate(Cell c, WallView v)
        {
            if (PlantAt(c) != null || HasPedestal(c))
            {
                Floater?.Invoke(CellCenter(c) + new Vector2(0, 0.9f), "Erst Pflanze oder Pult herunternehmen", new Color(1f, 0.7f, 0.55f));
                Sfx.Play("error", 1f, 0.5f);
                return;
            }
            v.st.gate = true;
            v.st.hp = MaxHp(v.st);
            v.sr.sprite = WallLook(v.st);
            v.pop = 0.5f;
            wallVersion++;
            Particles.I?.Burst(CellCenter(c) + new Vector2(0, WallH * 0.5f), "square", new Color(0.6f, 0.42f, 0.25f), 12, 2.6f, 0.5f, 0.08f, 7f, 1.6f);
            Sfx.Play("build", 1f, 0.8f);
            Haptics.Tap(20);
            Floater?.Invoke(CellCenter(c) + new Vector2(0, 1f), "Tor gebaut – Knollis kommen hier durch", new Color(1f, 0.92f, 0.6f));
            RefreshGhosts();
        }

        // ------------------------------------------------------------------ Knolli-Wege

        readonly Dictionary<object, KnolliRoute> routes = new Dictionary<object, KnolliRoute>();
        class KnolliRoute { public List<Vector2> pts = new List<Vector2>(); public Vector2 goal; public int version; public float age; public bool blocked; public float askT; }

        /// <summary>Naechster Zwischenpunkt auf dem Weg zum Ziel (Mauern umgehen, durch Tore). blocked = kein Weg.</summary>
        Vector2 RouteAim(object who, Vector2 from, Vector2 to, float dt, out bool blocked)
        {
            blocked = false;
            if (walls.Count == 0 || LineFree(from, to)) { routes.Remove(who); return to; }
            if (!routes.TryGetValue(who, out var r)) routes[who] = r = new KnolliRoute { version = -1 };
            r.age += dt;
            if (r.version != wallVersion || (r.goal - to).sqrMagnitude > 0.09f || r.age > 2f)
            {
                r.pts = FindPath(from, to);
                r.blocked = r.pts == null;
                r.goal = to; r.version = wallVersion; r.age = 0f;
            }
            if (r.blocked) { blocked = true; return from; }
            // Punkte ueberspringen, die man schon direkt sieht (glattere Wege)
            while (r.pts.Count > 1 && LineFree(from, r.pts[1])) r.pts.RemoveAt(0);
            if (r.pts.Count > 0 && (r.pts[0] - from).sqrMagnitude < 0.01f) r.pts.RemoveAt(0);
            return r.pts.Count > 0 ? r.pts[0] : to;
        }

        /// <summary>Kreuzt die Strecke keine (Nicht-Tor-)Mauer?</summary>
        bool LineFree(Vector2 a, Vector2 b)
        {
            float len = (b - a).magnitude;
            int n = Mathf.CeilToInt(len / (CellSize * 0.25f));
            var start = CellAt(a);
            for (int i = 1; i <= n; i++)
            {
                var c = CellAt(Vector2.Lerp(a, b, i / (float)n));
                if (!c.Equals(start) && Blocks(c)) return false;
            }
            return true;
        }

        /// <summary>Breitensuche auf dem Raster (8 Richtungen, keine Ecken abschneiden). null = kein Weg.</summary>
        List<Vector2> FindPath(Vector2 from, Vector2 to)
        {
            var s = CellAt(from); var g = CellAt(to);
            if (s.Equals(g)) return new List<Vector2> { to };
            int pad = 14;
            int x0 = Mathf.Min(s.x, g.x) - pad, x1 = Mathf.Max(s.x, g.x) + pad, y0 = Mathf.Min(s.y, g.y) - pad, y1 = Mathf.Max(s.y, g.y) + pad;
            var prev = new Dictionary<Cell, Cell>();
            var q = new Queue<Cell>();
            q.Enqueue(s); prev[s] = s;
            bool found = false;
            int guard = 0;
            while (q.Count > 0 && guard++ < 6000)
            {
                var c = q.Dequeue();
                if (c.Equals(g)) { found = true; break; }
                for (int dx = -1; dx <= 1; dx++)
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        var n = new Cell(c.x + dx, c.y + dy);
                        if (n.x < x0 || n.x > x1 || n.y < y0 || n.y > y1 || prev.ContainsKey(n)) continue;
                        if (Blocks(n) && !n.Equals(g)) continue;
                        if (dx != 0 && dy != 0 && (Blocks(new Cell(c.x + dx, c.y)) || Blocks(new Cell(c.x, c.y + dy)))) continue;   // nicht durch Mauerecken
                        prev[n] = c;
                        q.Enqueue(n);
                    }
            }
            if (!found) return null;
            var path = new List<Vector2>();
            for (var c = g; !c.Equals(s); c = prev[c]) path.Add(CellCenter(c));
            path.Reverse();
            if (path.Count > 0) path[path.Count - 1] = to;
            return path;
        }

        /// <summary>Kein Weg: ab und zu nach einem Tor rufen.</summary>
        void KnolliWalledIn(object who, float dt)
        {
            if (!routes.TryGetValue(who, out var r)) return;
            r.askT -= dt;
            if (r.askT > 0f) return;
            r.askT = 6f;
            var k = (Knolli)who;
            Floater?.Invoke(k.pos + new Vector2(0, 0.8f), "Ich komme nicht durch – bau ein Tor!", new Color(1f, 0.8f, 0.55f));
        }

        // ------------------------------------------------------------------ Bilder

        static Sprite WoodGateSprite => woodGateSprite != null ? woodGateSprite : woodGateSprite = PaintGate(WallSprite, new Color(0.5f, 0.33f, 0.19f), new Color(0.36f, 0.23f, 0.13f));
        static Sprite StoneGateSprite => stoneGateSprite != null ? stoneGateSprite : stoneGateSprite = PaintGate(StoneWallSprite, new Color(0.62f, 0.62f, 0.64f), new Color(0.4f, 0.4f, 0.43f));

        /// <summary>Mauerbild mit Torbogen: Bogenrahmen, Tuer aus senkrechten Bohlen, zwei Eisenbaender, Ring als Griff.</summary>
        static Sprite PaintGate(Sprite wall, Color frame, Color frameDark)
        {
            var src = wall.texture;
            int w = src.width, h = src.height;
            int front = Mathf.RoundToInt(WallH / CellSize * w);
            var px = src.GetPixels();
            float cx = w * 0.5f, halfW = w * 0.3f, archY = front * 0.62f;
            var plank = new Color(0.55f, 0.35f, 0.18f); var plankHi = new Color(0.72f, 0.5f, 0.28f); var iron = new Color(0.22f, 0.22f, 0.25f);
            for (int y = 0; y < front; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx;
                    // Innenform: Rechteck bis archY, darueber Halbkreis
                    bool inner = Mathf.Abs(dx) < halfW && (y < archY || dx * dx + (y - archY) * (y - archY) < halfW * halfW);
                    float fw = halfW + 6f;
                    bool outer = Mathf.Abs(dx) < fw && (y < archY || dx * dx + (y - archY) * (y - archY) < fw * fw);
                    if (!outer) continue;
                    Color c;
                    if (!inner)
                    {
                        // Bogenrahmen mit Fugen
                        float ang = Mathf.Atan2(y - archY, dx);
                        bool joint = y >= archY ? Mathf.Repeat(ang * 7f / Mathf.PI, 1f) < 0.12f : y % 14 < 2;
                        c = Color.Lerp(frameDark, frame, 0.5f + 0.5f * Mathf.Sin(y * 0.2f));
                        if (joint) c = frameDark * 0.8f;
                    }
                    else
                    {
                        // Tuer: senkrechte Bohlen, unten etwas dunkler
                        float bx = Mathf.Repeat(dx + halfW, 11f);
                        c = Color.Lerp(plank, plankHi, Mathf.PerlinNoise(x * 0.15f, y * 0.05f) * 0.7f) * Mathf.Lerp(0.75f, 1f, y / (float)front);
                        if (bx < 1.5f) c = plank * 0.55f;
                        if (Mathf.Abs(y - front * 0.18f) < 2.5f || Mathf.Abs(y - front * 0.5f) < 2.5f) c = iron;           // Eisenbaender
                        float rx = dx - halfW * 0.45f, ry = y - front * 0.33f;
                        float rr = Mathf.Sqrt(rx * rx + ry * ry);
                        if (rr > 3f && rr < 5f) c = iron * 1.6f;                                                      // Ring
                        if (Mathf.Abs(dx) < 0.8f) c = plank * 0.5f;                                                   // Mittelfuge (zwei Fluegel)
                    }
                    c.a = 1f;
                    px[y * w + x] = c;
                }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), w);
        }
    }
}
