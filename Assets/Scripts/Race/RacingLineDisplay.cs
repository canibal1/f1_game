using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    /// <summary>
    /// Dynamic racing line ahead of the player: green = accelerate, yellow = lift, red = brake.
    /// Colours compare the current speed with the racing-line speed profile.
    /// </summary>
    public class RacingLineDisplay : MonoBehaviour
    {
        RaceManager race;
        RaceCar me;
        Mesh mesh;
        GameObject go;
        readonly List<Vector3> verts = new List<Vector3>();
        readonly List<Color> cols = new List<Color>();
        readonly List<int> tris = new List<int>();
        const int Segments = 80;
        const float Step = 2.2f;

        public void Init(RaceManager r, RaceCar rc)
        {
            race = r;
            me = rc;
            go = new GameObject("RacingLine");
            go.transform.SetParent(r.transform, false);
            mesh = new Mesh { name = "racingLine" };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MaterialFactory.VertexColor();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        void OnDisable() { if (go != null) go.SetActive(false); }
        void OnEnable() { if (go != null) go.SetActive(true); }
        void OnDestroy() { if (go != null) Destroy(go); }

        void LateUpdate()
        {
            if (race == null || me == null) return;
            bool show = GameSettings.Current.showRacingLine && !me.InPit && race.state != RaceState.Finished;
            go.SetActive(show);
            if (!show) return;
            var td = race.track;
            float v = me.car.Speed;
            float maxDecel = 35f;
            verts.Clear(); cols.Clear(); tris.Clear();
            float start = me.lapDistance + 6f;
            for (int k = 0; k <= Segments; k++)
            {
                float d = start + k * Step;
                Vector3 p = td.LinePositionAt(d);
                Vector3 r = td.RightAt(d);
                p.y = 0.03f;
                float vt = td.SpeedAt(d);
                float dist = k * Step + 6f;
                float need = (v * v - vt * vt) / (2f * dist);
                Color c;
                if (need > maxDecel * 0.45f) c = new Color(0.95f, 0.15f, 0.1f);
                else if (need > 0f || v > vt) c = new Color(1f, 0.8f, 0.1f);
                else c = new Color(0.2f, 0.9f, 0.3f);
                float fade = Mathf.Clamp01(k / 5f) * Mathf.Clamp01((Segments - k) / 20f);
                c.a = 0.75f * fade;
                float w = 0.35f;
                verts.Add(p - r * w); verts.Add(p + r * w);
                cols.Add(c); cols.Add(c);
                if (k > 0)
                {
                    int i = verts.Count - 4;
                    tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
                    tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 3);
                }
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }
    }
}
