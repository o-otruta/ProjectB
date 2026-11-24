using UnityEngine;

namespace ProjectB.EditorScripts.UiKit
{
    /// <summary>
    /// Tiny pixel-art rasterizer for procedural UI icons: shapes are tested at pixel centres on a small grid
    /// (16×16 / 24×24, y-down), then an automatic 1px dark outline and a voxel-style bevel (lighter top edge,
    /// darker bottom edge) are added, and the result is upscaled with nearest-neighbour sampling.
    /// </summary>
    public sealed class PixelCanvas
    {
        public static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        public static readonly Color32 OutlineColor = new Color32(22, 19, 42, 255);

        private readonly Color32[,] px;
        public int Size { get; }

        public PixelCanvas(int size)
        {
            Size = size;
            px = new Color32[size, size];
        }

        public Color32 Get(int x, int y) => px[x, y];

        public void Set(int x, int y, Color32 c)
        {
            if (x < 0 || y < 0 || x >= Size || y >= Size) return;
            px[x, y] = c;
        }

        public void Circle(float cx, float cy, float r, Color32 c)
        {
            float r2 = r * r;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                if (dx * dx + dy * dy <= r2) px[x, y] = c;
            }
        }

        public void Ring(float cx, float cy, float r0, float r1, Color32 c)
        {
            Arc(cx, cy, r0, r1, -180f, 180f, c);
        }

        /// <summary>Annulus sector; angles in degrees, 0 = +x (right), positive = clockwise on screen (y-down).</summary>
        public void Arc(float cx, float cy, float r0, float r1, float a0, float a1, Color32 c)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < r0 || d > r1) continue;
                float a = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                if (InRange(a, a0, a1)) px[x, y] = c;
            }
        }

        private static bool InRange(float a, float a0, float a1)
        {
            while (a < a0) a += 360f;
            while (a > a0 + 360f) a -= 360f;
            return a <= a1;
        }

        /// <summary>Inclusive pixel rectangle.</summary>
        public void Rect(int x0, int y0, int x1, int y1, Color32 c)
        {
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(Size - 1, y1); y++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(Size - 1, x1); x++)
                px[x, y] = c;
        }

        public void Poly(Color32 c, params Vector2[] pts)
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (PointInPolygon(new Vector2(x + 0.5f, y + 0.5f), pts)) px[x, y] = c;
            }
        }

        public void Line(float x0, float y0, float x1, float y1, float thickness, Color32 c)
        {
            Vector2 a = new Vector2(x0, y0), b = new Vector2(x1, y1);
            float half = thickness * 0.5f;
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (DistanceToSegment(new Vector2(x + 0.5f, y + 0.5f), a, b) <= half) px[x, y] = c;
            }
        }

        /// <summary>Five-point star.</summary>
        public void Star(float cx, float cy, float outer, float inner, Color32 c)
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float r = i % 2 == 0 ? outer : inner;
                float a = (-90f + i * 36f) * Mathf.Deg2Rad;
                pts[i] = new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r);
            }
            Poly(c, pts);
        }

        /// <summary>Lighter top edge / darker bottom edge on every filled pixel (voxel bevel).</summary>
        public void Bevel(float light = 0.28f, float dark = 0.3f)
        {
            var copy = (Color32[,])px.Clone();
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Color32 c = copy[x, y];
                if (c.a == 0) continue;
                bool topOpen = y == 0 || copy[x, y - 1].a == 0;
                bool bottomOpen = y == Size - 1 || copy[x, y + 1].a == 0;
                if (topOpen) px[x, y] = Lerp(c, new Color32(255, 255, 255, c.a), light);
                else if (bottomOpen) px[x, y] = Lerp(c, new Color32(0, 0, 0, c.a), dark);
            }
        }

        /// <summary>1px outline around the filled shape (8-neighbourhood).</summary>
        public void Outline(Color32 color)
        {
            var copy = (Color32[,])px.Clone();
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (copy[x, y].a != 0) continue;
                bool near = false;
                for (int oy = -1; oy <= 1 && !near; oy++)
                for (int ox = -1; ox <= 1 && !near; ox++)
                {
                    int nx = x + ox, ny = y + oy;
                    if (nx < 0 || ny < 0 || nx >= Size || ny >= Size) continue;
                    if (copy[nx, ny].a != 0 && !Same(copy[nx, ny], color)) near = true;
                }
                if (near) px[x, y] = color;
            }
        }

        /// <summary>Nearest-neighbour upscale into a bottom-up texture buffer.</summary>
        public Color32[] ToPixels(int scale, out int textureSize)
        {
            textureSize = Size * scale;
            var buf = new Color32[textureSize * textureSize];
            for (int y = 0; y < textureSize; y++)
            {
                int gy = Size - 1 - y / scale; // flip: grid is y-down, texture is y-up
                for (int x = 0; x < textureSize; x++)
                {
                    buf[y * textureSize + x] = px[x / scale, gy];
                }
            }
            return buf;
        }

        private static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        private static Color32 Lerp(Color32 a, Color32 b, float t)
        {
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.r, b.r, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.g, b.g, t)),
                (byte)Mathf.RoundToInt(Mathf.Lerp(a.b, b.b, t)),
                a.a);
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            float t = len2 > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        private static bool PointInPolygon(Vector2 p, Vector2[] pts)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                if ((pts[i].y > p.y) != (pts[j].y > p.y) &&
                    p.x < (pts[j].x - pts[i].x) * (p.y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x)
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}
