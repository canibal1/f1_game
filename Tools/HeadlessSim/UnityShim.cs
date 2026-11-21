// Minimal managed stand-in for UnityEngine math types so pure game logic can run under .NET 8.
using System;
namespace UnityEngine
{
    public static class Mathf
    {
        public const float PI = (float)Math.PI; public const float Deg2Rad = PI / 180f; public const float Rad2Deg = 180f / PI;
        public const float Infinity = float.PositiveInfinity; public const float Epsilon = 1.4e-45f;
        public static float Sin(float f) => (float)Math.Sin(f); public static float Cos(float f) => (float)Math.Cos(f);
        public static float Tan(float f) => (float)Math.Tan(f); public static float Atan(float f) => (float)Math.Atan(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x); public static float Asin(float f) => (float)Math.Asin(f);
        public static float Acos(float f) => (float)Math.Acos(f);
        public static float Sqrt(float f) => (float)Math.Sqrt(f); public static float Abs(float f) => Math.Abs(f); public static int Abs(int f) => Math.Abs(f);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b); public static float Exp(float a) => (float)Math.Exp(a);
        public static float Log(float a) => (float)Math.Log(a);
        public static float Min(float a, float b) => a < b ? a : b; public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b; public static int Max(int a, int b) => a > b ? a : b;
        public static float Min(params float[] v) { float m = v[0]; foreach (var x in v) if (x < m) m = x; return m; }
        public static float Max(params float[] v) { float m = v[0]; foreach (var x in v) if (x > m) m = x; return m; }
        public static float Sign(float f) => f >= 0f ? 1f : -1f;
        public static float Clamp(float v, float a, float b) => v < a ? a : v > b ? b : v;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Math.Sign(t - c) * d;
        public static float Round(float f) => (float)Math.Round(f, MidpointRounding.ToEven);
        public static int RoundToInt(float f) => (int)Math.Round(f, MidpointRounding.ToEven);
        public static int FloorToInt(float f) => (int)Math.Floor(f); public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static float Floor(float f) => (float)Math.Floor(f); public static float Repeat(float t, float l) => Clamp(t - Floor(t / l) * l, 0f, l);
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1e-6f * Math.Max(Math.Abs(a), Math.Abs(b)), 1e-6f);
        public static float SmoothStep(float a, float b, float t) { t = Clamp01(t); t = -2f * t * t * t + 3f * t * t; return b * t + a * (1f - t); }
        public static float DeltaAngle(float c, float t) { float d = Repeat(t - c, 360f); if (d > 180f) d -= 360f; return d; }
    }
    public struct Vector2
    {
        public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0); public static Vector2 one => new Vector2(1, 1);
        public float magnitude => Mathf.Sqrt(x * x + y * y); public float sqrMagnitude => x * x + y * y;
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector2(x / m, y / m) : zero; } }
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator *(float d, Vector2 a) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public override string ToString() => $"({x:0.00}, {y:0.00})";
    }
    public struct Vector3
    {
        public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public static Vector3 zero => new Vector3(0, 0, 0); public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0); public static Vector3 forward => new Vector3(0, 0, 1); public static Vector3 right => new Vector3(1, 0, 0);
        public float magnitude => Mathf.Sqrt(x * x + y * y + z * z); public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector3(x / m, y / m, z / m) : zero; } }
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Math.Min(a.x,b.x), Math.Min(a.y,b.y), Math.Min(a.z,b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Math.Max(a.x,b.x), Math.Max(a.y,b.y), Math.Max(a.z,b.z));
        public static Vector3 Slerp(Vector3 a, Vector3 b, float t) => Lerp(a, b, t).normalized * Mathf.Lerp(a.magnitude, b.magnitude, t);
        public static Vector3 MoveTowards(Vector3 a, Vector3 b, float d) { var v = b - a; float m = v.magnitude; return m <= d || m == 0 ? b : a + v / m * d; }
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 1e-10f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object o) => o is Vector3 v && v == this;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 ProjectOnPlane(Vector3 v, Vector3 n) { float d = Dot(n, n); return d < 1e-9f ? v : v - n * (Dot(v, n) / d); }
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
        public override string ToString() => $"({x:0.00}, {y:0.00}, {z:0.00})";
    }
    public struct Color
    {
        public float r, g, b, a; public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1); public static Color red => new Color(1,0,0); public static Color clear => new Color(0,0,0,0);
        public static bool operator ==(Color a, Color b) => a.r==b.r&&a.g==b.g&&a.b==b.b&&a.a==b.a; public static bool operator !=(Color a, Color b) => !(a==b);
        public override bool Equals(object o) => o is Color c && c == this; public override int GetHashCode() => 0;
        public static Color operator *(Color c, float f) => new Color(c.r*f, c.g*f, c.b*f, c.a*f); public static Color black => new Color(0, 0, 0);
        public static Color Lerp(Color x, Color y, float t) { t = Mathf.Clamp01(t); return new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t); }
    }
    public static class ColorUtility
    {
        public static bool TryParseHtmlString(string s, out Color c)
        {
            s = s.TrimStart('#'); int v = Convert.ToInt32(s.Substring(0, 6), 16);
            c = new Color(((v >> 16) & 255) / 255f, ((v >> 8) & 255) / 255f, (v & 255) / 255f); return true;
        }
    }
}
