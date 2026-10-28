using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace SeedClicker
{
    /// <summary>
    /// Vergleichs-Modus: Boden als echte Unity-Tilemap aus den Skyden-Bodenkacheln statt des frei gemalten
    /// Boden-Materials. Einschalten in Resources/WorldArt.asset -> "Use Tilemap Ground".
    /// Wege, Pflasterplatz und Schlammstellen kommen aus denselben Daten wie im freien Boden, werden aber
    /// auf 1-Einheiten-Kacheln gerastert und mit Rand-, Ecken- und Innenecken-Kacheln automatisch verbunden.
    /// </summary>
    public partial class WorldBuilder
    {
        const int GrassKind = 0, DirtKind = 1, StoneKind = 2;
        readonly Dictionary<long, Tile> tileCache = new Dictionary<long, Tile>();

        void BuildTileGround()
        {
            // Der gemalte Boden bleibt als Unterlage: an der Bergkante (die nicht im Raster verlaeuft) schaut er durch
            BuildTerrain();
            var atlas = art.groundAtlas;
            if (atlas == null) return;
            var gridGo = new GameObject("TileGround", typeof(Grid));
            gridGo.transform.SetParent(transform, false);
            var tmGo = new GameObject("Ground");
            tmGo.transform.SetParent(gridGo.transform, false);
            var tm = tmGo.AddComponent<Tilemap>();
            var tr = tmGo.AddComponent<TilemapRenderer>();
            tr.sortingOrder = -32499;   // ueber dem gemalten Boden
            tr.mode = TilemapRenderer.Mode.Chunk;

            int x0 = Mathf.FloorToInt(-MapW / 2f) - 1, x1 = Mathf.CeilToInt(MapW / 2f) + 1;
            int y0 = Mathf.FloorToInt(-MapH / 2f) - 1, y1 = Mathf.CeilToInt(MapH / 2f) + 1;
            int w = x1 - x0, h = y1 - y0;
            var kind = new int[w, h];
            var valid = new bool[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    var c = new Vector2(x0 + x + 0.5f, y0 + y + 0.5f);
                    valid[x, y] = Inside(c) > 0.55f;   // nur Kacheln, die ganz vor dem Bergfuss liegen
                    if (!valid[x, y]) continue;
                    var m = MaskAt(c);
                    if (m.a > 0.12f) kind[x, y] = StoneKind;
                    else if (DistToPath(c) < 0.75f || m.g > 0.4f || m.r > 0.45f) kind[x, y] = DirtKind;
                }
            // Die 3x3-Kachelsaetze brauchen mindestens 2 Kacheln Breite: duenne Stellen verbreitern
            for (int pass = 0; pass < 2; pass++)
                for (int x = 1; x < w - 1; x++)
                    for (int y = 1; y < h - 1; y++)
                    {
                        int k = kind[x, y];
                        if (k == GrassKind) continue;
                        if (kind[x - 1, y] != k && kind[x + 1, y] != k) kind[x + 1, y] = k;
                        if (kind[x, y - 1] != k && kind[x, y + 1] != k) kind[x, y + 1] = k;
                    }

            var rng = new System.Random(77);
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                {
                    if (!valid[x, y]) continue;
                    int k = kind[x, y];
                    Vector2Int cell;
                    if (k == GrassKind)
                    {
                        double r = rng.NextDouble();
                        cell = r < 0.72 ? new Vector2Int(4, 0) : r < 0.84 ? new Vector2Int(0, 0) : r < 0.93 ? new Vector2Int(3, 0) : r < 0.97 ? new Vector2Int(2, 0) : new Vector2Int(1, 0);
                    }
                    else cell = AutoTile(kind, x, y, w, h, k == StoneKind ? new Vector2Int(0, 1) : new Vector2Int(0, 4), k == StoneKind ? new Vector2Int(3, 1) : new Vector2Int(3, 5));
                    tm.SetTile(new Vector3Int(x0 + x, y0 + y, 0), TileAt(atlas, cell));
                }
        }

        /// <summary>Waehlt aus einem 3x3-Satz (base = linke obere Ecke im Atlas) und den Innenecken die passende Kachel.</summary>
        static Vector2Int AutoTile(int[,] kind, int x, int y, int w, int h, Vector2Int b, Vector2Int inner)
        {
            int k = kind[x, y];
            bool Same(int dx, int dy)
            {
                int nx = x + dx, ny = y + dy;
                return nx < 0 || ny < 0 || nx >= w || ny >= h || kind[nx, ny] == k;
            }
            bool up = Same(0, 1), down = Same(0, -1), left = Same(-1, 0), right = Same(1, 0);
            int col = !left ? 0 : !right ? 2 : 1;
            int row = !up ? 0 : !down ? 2 : 1;
            if (col != 1 || row != 1) return new Vector2Int(b.x + col, b.y + row);
            // innen: Gras nur diagonal -> Innenecke
            if (!Same(1, -1)) return new Vector2Int(inner.x, inner.y);
            if (!Same(-1, -1)) return new Vector2Int(inner.x + 1, inner.y);
            if (!Same(1, 1)) return new Vector2Int(inner.x, inner.y + 1);
            if (!Same(-1, 1)) return new Vector2Int(inner.x + 1, inner.y + 1);
            return new Vector2Int(b.x + 1, b.y + 1);
        }

        /// <summary>Kachel aus dem Skyden-Atlas (8x8 Felder, Zeile 0 = oben).</summary>
        Tile TileAt(Texture2D atlas, Vector2Int cell)
        {
            long key = cell.x * 100 + cell.y;
            if (tileCache.TryGetValue(key, out var t)) return t;
            int ts = atlas.width / 8;
            var sprite = Sprite.Create(atlas, new Rect(cell.x * ts, atlas.height - (cell.y + 1) * ts, ts, ts), new Vector2(0.5f, 0.5f), ts, 0, SpriteMeshType.FullRect);
            t = ScriptableObject.CreateInstance<Tile>();
            t.sprite = sprite;
            t.colliderType = Tile.ColliderType.None;
            tileCache[key] = t;
            return t;
        }
    }
}
