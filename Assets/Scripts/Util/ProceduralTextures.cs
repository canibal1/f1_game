using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    /// <summary>Runtime-generated textures (no texture assets needed).</summary>
    public static class ProceduralTextures
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();

        static Texture2D Make(string key, int w, int h, System.Func<int, int, Color> f, FilterMode filter = FilterMode.Bilinear, bool mip = true)
        {
            if (cache.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(w, h, TextureFormat.RGBA32, mip) { name = key, wrapMode = TextureWrapMode.Repeat, filterMode = filter, anisoLevel = 4 };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = f(x, y);
            t.SetPixels(px);
            t.Apply(mip, false);
            cache[key] = t;
            return t;
        }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 144269504);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xffffff) / (float)0xffffff;
            }
        }

        /// <summary>Tileable value noise (period = size).</summary>
        static float TileNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int Wrap(int v) => ((v % period) + period) % period;
            float a = Hash(Wrap(x0), Wrap(y0), seed), b = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            float c = Hash(Wrap(x0), Wrap(y0 + 1), seed), d = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        static float Fbm(int x, int y, int size, int seed)
        {
            float v = 0, amp = 0.5f, tot = 0;
            for (int o = 0; o < 4; o++)
            {
                int period = 4 << o;
                float s = period / (float)size;
                v += TileNoise(x * s, y * s, period, seed + o * 31) * amp;
                tot += amp; amp *= 0.5f;
            }
            return v / tot;
        }

        public static Texture2D Asphalt()
        {
            const int n = 256;
            return Make("asphalt", n, n, (x, y) =>
            {
                float g = 0.30f + (Fbm(x, y, n, 7) - 0.5f) * 0.10f + (Hash(x, y, 3) - 0.5f) * 0.10f;
                if (Hash(x, y, 9) > 0.985f) g += 0.12f; // aggregate speckles
                return new Color(g, g, g * 1.03f, 1);
            });
        }

        public static Texture2D Grass(Color baseColor)
        {
            const int n = 256;
            string key = "grass" + ColorUtility.ToHtmlStringRGB(baseColor);
            return Make(key, n, n, (x, y) =>
            {
                float stripe = ((y / 64) % 2 == 0) ? 1.06f : 0.94f; // mowing stripes
                float v = (0.85f + Fbm(x, y, n, 11) * 0.3f) * stripe * (0.92f + Hash(x, y, 5) * 0.16f);
                return new Color(baseColor.r * v, baseColor.g * v, baseColor.b * v, 1);
            });
        }

        public static Texture2D Gravel()
        {
            const int n = 128;
            return Make("gravel", n, n, (x, y) =>
            {
                float v = 0.75f + Hash(x, y, 21) * 0.35f;
                return new Color(0.78f * v, 0.70f * v, 0.55f * v, 1);
            });
        }

        public static Texture2D Concrete()
        {
            const int n = 128;
            return Make("concrete", n, n, (x, y) =>
            {
                float v = 0.78f + (Fbm(x, y, n, 41) - 0.5f) * 0.15f + (Hash(x, y, 2) - 0.5f) * 0.05f;
                return new Color(v, v, v * 0.98f, 1);
            });
        }

        /// <summary>Two-tone stripe texture (kerbs, tyre walls). u axis runs along the track.</summary>
        public static Texture2D Stripes(Color a, Color b)
        {
            string key = "stripes" + ColorUtility.ToHtmlStringRGB(a) + ColorUtility.ToHtmlStringRGB(b);
            return Make(key, 2, 1, (x, y) => x == 0 ? a : b, FilterMode.Point, false);
        }

        public static Texture2D Checker(int cells = 8)
        {
            return Make("checker" + cells, cells, cells, (x, y) => ((x + y) % 2 == 0) ? Color.white : new Color(0.08f, 0.08f, 0.08f), FilterMode.Point, false);
        }

        public static Texture2D White()
        {
            return Make("white", 4, 4, (x, y) => Color.white, FilterMode.Point, false);
        }

        /// <summary>Solid colour texture for IMGUI backgrounds.</summary>
        public static Texture2D Solid(Color c)
        {
            string key = "solid" + ColorUtility.ToHtmlStringRGBA(c);
            return Make(key, 2, 2, (x, y) => c, FilterMode.Point, false);
        }

        /// <summary>Rounded rectangle (9-slice friendly) for UI panels/buttons.</summary>
        public static Texture2D Rounded(Color fill, Color border, int radius = 6)
        {
            string key = "round" + ColorUtility.ToHtmlStringRGBA(fill) + ColorUtility.ToHtmlStringRGBA(border) + radius;
            int n = radius * 2 + 4;
            return Make(key, n, n, (x, y) =>
            {
                float cx = Mathf.Clamp(x + 0.5f, radius, n - radius);
                float cy = Mathf.Clamp(y + 0.5f, radius, n - radius);
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                if (d > radius) return new Color(0, 0, 0, 0);
                if (d > radius - 1.5f || x == 0 || y == 0 || x == n - 1 || y == n - 1) return border;
                return fill;
            }, FilterMode.Bilinear, false);
        }

        /// <summary>Vertical gradient (UI headers, sky panels).</summary>
        public static Texture2D Gradient(Color bottom, Color top)
        {
            string key = "grad" + ColorUtility.ToHtmlStringRGBA(bottom) + ColorUtility.ToHtmlStringRGBA(top);
            return Make(key, 2, 64, (x, y) => Color.Lerp(bottom, top, y / 63f), FilterMode.Bilinear, false);
        }

        /// <summary>Soft round dot (minimap car markers, smoke particles).</summary>
        public static Texture2D Dot()
        {
            const int n = 32;
            return Make("dot", n, n, (x, y) =>
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f)) / (n / 2f);
                float a = Mathf.Clamp01((1f - d) * 3f);
                return new Color(1, 1, 1, a);
            }, FilterMode.Bilinear, false);
        }
    }
}
