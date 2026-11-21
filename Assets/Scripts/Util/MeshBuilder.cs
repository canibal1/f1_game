using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    /// <summary>Tiny procedural mesh helper (flat-shaded boxes, cylinders, ribbons...).</summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> verts = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<Color> colors = new List<Color>();
        readonly List<List<int>> subMeshes = new List<List<int>>();
        int current;

        public MeshBuilder(int subMeshCount = 1)
        {
            for (int i = 0; i < subMeshCount; i++) subMeshes.Add(new List<int>());
        }

        public int VertexCount => verts.Count;

        public MeshBuilder Sub(int index)
        {
            while (subMeshes.Count <= index) subMeshes.Add(new List<int>());
            current = index;
            return this;
        }

        public int AddVertex(Vector3 p, Vector3 n, Vector2 uv, Color? c = null)
        {
            verts.Add(p); normals.Add(n); uvs.Add(uv); colors.Add(c ?? Color.white);
            return verts.Count - 1;
        }

        public void AddTriangle(int a, int b, int c)
        {
            var l = subMeshes[current];
            l.Add(a); l.Add(b); l.Add(c);
        }

        /// <summary>Quad from 4 corners in clockwise order when seen from the front (Unity winding).</summary>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2? uvMin = null, Vector2? uvMax = null, Color? col = null)
        {
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            Vector2 u0 = uvMin ?? Vector2.zero, u1 = uvMax ?? Vector2.one;
            int i0 = AddVertex(a, n, new Vector2(u0.x, u0.y), col);
            int i1 = AddVertex(b, n, new Vector2(u0.x, u1.y), col);
            int i2 = AddVertex(c, n, new Vector2(u1.x, u1.y), col);
            int i3 = AddVertex(d, n, new Vector2(u1.x, u0.y), col);
            AddTriangle(i0, i1, i2);
            AddTriangle(i0, i2, i3);
        }

        /// <summary>Axis-aligned box in the given local frame (center, size, rotation).</summary>
        public void AddBox(Vector3 center, Vector3 size, Quaternion rot, float uvScale = 1f, Color? col = null)
        {
            Vector3 h = size * 0.5f;
            Vector3 P(float x, float y, float z) => center + rot * new Vector3(x * h.x, y * h.y, z * h.z);
            Vector2 U(float a, float b) => new Vector2(a * uvScale, b * uvScale);
            // +X
            AddQuad(P(1, -1, -1), P(1, 1, -1), P(1, 1, 1), P(1, -1, 1), Vector2.zero, U(size.z, size.y), col);
            // -X
            AddQuad(P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), P(-1, -1, -1), Vector2.zero, U(size.z, size.y), col);
            // +Y
            AddQuad(P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), Vector2.zero, U(size.x, size.z), col);
            // -Y
            AddQuad(P(-1, -1, 1), P(-1, -1, -1), P(1, -1, -1), P(1, -1, 1), Vector2.zero, U(size.x, size.z), col);
            // +Z
            AddQuad(P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), P(-1, -1, 1), Vector2.zero, U(size.x, size.y), col);
            // -Z
            AddQuad(P(-1, -1, -1), P(-1, 1, -1), P(1, 1, -1), P(1, -1, -1), Vector2.zero, U(size.x, size.y), col);
        }

        /// <summary>Cylinder along local X axis (wheels, tubes). Flat shaded caps, smooth sides.</summary>
        public void AddCylinderX(Vector3 center, float radius, float width, int segments, Quaternion rot, bool caps = true)
        {
            float hw = width * 0.5f;
            int baseIdx = verts.Count;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                Vector3 n = new Vector3(0, Mathf.Cos(a), Mathf.Sin(a));
                AddVertex(center + rot * (new Vector3(-hw, 0, 0) + n * radius), rot * n, new Vector2(i / (float)segments, 0));
                AddVertex(center + rot * (new Vector3(hw, 0, 0) + n * radius), rot * n, new Vector2(i / (float)segments, 1));
            }
            for (int i = 0; i < segments; i++)
            {
                int a = baseIdx + i * 2;
                AddTriangle(a, a + 2, a + 1);
                AddTriangle(a + 1, a + 2, a + 3);
            }
            if (!caps) return;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 n = rot * new Vector3(side, 0, 0);
                int c = AddVertex(center + rot * new Vector3(side * hw, 0, 0), n, new Vector2(0.5f, 0.5f));
                int first = verts.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    Vector3 p = new Vector3(side * hw, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
                    AddVertex(center + rot * p, n, new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
                }
                for (int i = 0; i < segments; i++)
                {
                    if (side > 0) AddTriangle(c, first + i, first + i + 1);
                    else AddTriangle(c, first + i + 1, first + i);
                }
            }
        }

        /// <summary>Surface of revolution around local Y (trophy cups). profile: (radius, height).</summary>
        public void AddLathe(Vector3 center, IList<Vector2> profile, int segments)
        {
            int baseIdx = verts.Count;
            for (int j = 0; j < profile.Count; j++)
            {
                Vector2 prev = profile[Mathf.Max(0, j - 1)], next = profile[Mathf.Min(profile.Count - 1, j + 1)];
                Vector2 tangent = (next - prev).normalized;
                Vector2 n2 = new Vector2(tangent.y, -tangent.x);
                for (int i = 0; i <= segments; i++)
                {
                    float a = i / (float)segments * Mathf.PI * 2f;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    AddVertex(center + new Vector3(c * profile[j].x, profile[j].y, s * profile[j].x),
                        new Vector3(c * n2.x, n2.y, s * n2.x).normalized, new Vector2(i / (float)segments, j / (float)(profile.Count - 1)));
                }
            }
            int ring = segments + 1;
            for (int j = 0; j < profile.Count - 1; j++)
                for (int i = 0; i < segments; i++)
                {
                    int a = baseIdx + j * ring + i;
                    AddTriangle(a, a + ring, a + 1);
                    AddTriangle(a + 1, a + ring, a + ring + 1);
                }
        }

        public Mesh ToMesh(string name = "mesh", bool recalcNormals = false)
        {
            var m = new Mesh { name = name };
            if (verts.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.SetVertices(verts);
            m.SetNormals(normals);
            m.SetUVs(0, uvs);
            m.SetColors(colors);
            m.subMeshCount = subMeshes.Count;
            for (int i = 0; i < subMeshes.Count; i++) m.SetTriangles(subMeshes[i], i);
            if (recalcNormals) m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        public int SubMeshCount => subMeshes.Count;
    }
}
