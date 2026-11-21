using System;
using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace F1Game
{
    /// <summary>
    /// Builds the visible circuit from TrackData: non-overlapping surface strips (road, lines, kerbs,
    /// grass, gravel, pit lane), barriers with colliders, pit wall, pit garages, start gantry with lights,
    /// grid boxes, grandstands and scenery made from the CC0 Kenney models.
    /// </summary>
    public static class TrackBuilder
    {
        const float Y = 0f;

        static float[] leftLim, rightLim, pitOff;

        public static TrackWorld Build(TrackData td, Transform parent)
        {
            var root = new GameObject("Track_" + td.def.id);
            root.transform.SetParent(parent, false);
            var world = root.AddComponent<TrackWorld>();
            world.data = td;

            ComputeLimits(td);
            BuildGround(td, root.transform);
            BuildSurface(td, root.transform);
            BuildBarriers(td, root.transform);
            BuildPitComplex(td, root.transform);
            world.startLights = BuildStartGantry(td, root.transform);
            BuildGridMarks(td, root.transform);
            BuildGrandstands(td, root.transform);
            BuildScenery(td, root.transform);
            SetupEnvironment(td, root.transform, world);
            world.minimap = BuildMinimap(td, out world.mapMin, out world.mapSize);
            return world;
        }

        // ------------------------------------------------------------------ limits
        static void ComputeLimits(TrackData td)
        {
            int n = td.count;
            var rawL = new float[n];
            var rawR = new float[n];
            pitOff = new float[n];
            for (int i = 0; i < n; i++)
            {
                rawL[i] = td.SideLimit(i, false);
                rawR[i] = td.SideLimit(i, true);
                float sd = td.SignedDistance(td.dist[i]);
                pitOff[i] = td.PitOffsetAt(sd);
                if (td.InPitZone(td.dist[i])) rawR[i] = Mathf.Max(rawR[i], pitOff[i] + 9.5f);
            }
            leftLim = SmoothMin(rawL, 10, 8);
            rightLim = SmoothMin(rawR, 10, 8);
        }

        static float[] SmoothMin(float[] v, int minWin, int blurWin)
        {
            int n = v.Length;
            var m = new float[n];
            for (int i = 0; i < n; i++)
            {
                float x = float.MaxValue;
                for (int k = -minWin; k <= minWin; k++) x = Mathf.Min(x, v[(i + k + n) % n]);
                m[i] = x;
            }
            var o = new float[n];
            for (int i = 0; i < n; i++)
            {
                float s = 0f;
                for (int k = -blurWin; k <= blurWin; k++) s += m[(i + k + n) % n];
                o[i] = s / (2 * blurWin + 1);
            }
            return o;
        }

        // ------------------------------------------------------------------ helpers
        static Vector3 P(TrackData td, int i, float off, float y = Y)
        {
            i = (i + td.count) % td.count;
            var c = td.center[i] + td.right[i] * off;
            c.y = y;
            return c;
        }

        /// <summary>Adds a strip between offsets lo(i) and hi(i) for every sample where use(i) is true.</summary>
        static void Strip(MeshBuilder mb, TrackData td, int sub, Func<int, float> lo, Func<int, float> hi, Func<int, bool> use,
            float uvAlong, float uvAcross, bool stripesAlong = false, float y = Y)
        {
            mb.Sub(sub);
            int n = td.count;
            for (int i = 0; i < n; i++)
            {
                if (!use(i)) continue;
                int j = (i + 1) % n;
                float l0 = lo(i), h0 = Mathf.Max(lo(i), hi(i)), l1 = lo(j), h1 = Mathf.Max(lo(j), hi(j));
                if (h0 - l0 < 0.01f && h1 - l1 < 0.01f) continue;
                float a0 = td.dist[i], a1 = td.dist[i] + td.ds;
                Vector3 pa = P(td, i, l0, y), pb = P(td, j, l1, y), pc = P(td, j, h1, y), pd = P(td, i, h0, y);
                Vector2 uvA, uvB, uvC, uvD;
                if (stripesAlong)
                {
                    uvA = new Vector2(a0 / uvAlong, 0); uvB = new Vector2(a1 / uvAlong, 0);
                    uvC = new Vector2(a1 / uvAlong, 1); uvD = new Vector2(a0 / uvAlong, 1);
                }
                else
                {
                    uvA = new Vector2(l0 / uvAcross, a0 / uvAlong); uvB = new Vector2(l1 / uvAcross, a1 / uvAlong);
                    uvC = new Vector2(h1 / uvAcross, a1 / uvAlong); uvD = new Vector2(h0 / uvAcross, a0 / uvAlong);
                }
                int ia = mb.AddVertex(pa, Vector3.up, uvA);
                int ib = mb.AddVertex(pb, Vector3.up, uvB);
                int ic = mb.AddVertex(pc, Vector3.up, uvC);
                int id = mb.AddVertex(pd, Vector3.up, uvD);
                mb.AddTriangle(ia, ib, ic);
                mb.AddTriangle(ia, ic, id);
            }
        }

        static GameObject MeshObject(string name, Transform parent, Mesh mesh, Material[] mats, bool shadows = false, bool collider = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mats;
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        // ------------------------------------------------------------------ ground
        static void BuildGround(TrackData td, Transform parent)
        {
            Vector3 c = (td.boundsMin + td.boundsMax) * 0.5f;
            Vector3 size = td.boundsMax - td.boundsMin + new Vector3(1600f, 0f, 1600f);
            // physics: one flat box, top face at y = 0
            var col = new GameObject("GroundCollider");
            col.transform.SetParent(parent, false);
            col.transform.position = new Vector3(c.x, -1f, c.z);
            var bc = col.AddComponent<BoxCollider>();
            bc.size = new Vector3(size.x, 2f, size.z);

            // visual: lower than the near-track strips so they never z-fight
            var mb = new MeshBuilder();
            float hx = size.x * 0.5f, hz = size.z * 0.5f;
            const float y = -0.3f;
            mb.AddQuad(new Vector3(c.x - hx, y, c.z - hz), new Vector3(c.x - hx, y, c.z + hz), new Vector3(c.x + hx, y, c.z + hz), new Vector3(c.x + hx, y, c.z - hz),
                Vector2.zero, new Vector2(size.x / 12f, size.z / 12f));
            var grass = MaterialFactory.Lit(Color.white, 0.05f, 0f, ProceduralTextures.Grass(td.def.grass * 0.92f));
            MeshObject("Ground", parent, mb.ToMesh("ground"), new[] { grass });
        }

        // ------------------------------------------------------------------ surfaces
        static void BuildSurface(TrackData td, Transform parent)
        {
            float hw = td.halfWidth, kw = TrackData.KerbWidth;
            bool paved = td.def.scenery == Scenery.Street || td.def.scenery == Scenery.Speedway;
            var mb = new MeshBuilder(10);
            const int ROAD = 0, LINE = 1, KERB = 2, GRASS = 3, GRAVEL = 4, RUNOFF = 5, PIT = 6, CONCRETE = 7, CHECKER = 8, EDGE = 9;
            const float line = 0.25f;

            Func<int, bool> all = i => true;
            Func<int, bool> startSeg = i => i == 0;
            Func<int, bool> notStart = i => i != 0;
            // road + painted edge lines (+ chequered start line on segment 0)
            Strip(mb, td, ROAD, i => -hw + line, i => hw - line, notStart, 10f, 10f);
            Strip(mb, td, CHECKER, i => -hw + line, i => hw - line, startSeg, 2f, 2f, true);
            Strip(mb, td, LINE, i => -hw, i => -hw + line, all, 10f, 10f);
            Strip(mb, td, LINE, i => hw - line, i => hw, all, 10f, 10f);

            // kerbs (or a plain painted verge where there is no kerb)
            Strip(mb, td, KERB, i => -hw - kw, i => -hw, i => td.kerb[i], 2f, 1f, true);
            Strip(mb, td, KERB, i => hw, i => hw + kw, i => td.kerb[i], 2f, 1f, true);
            // in the pit entry/exit blends the right verge is paved so the pit lane joins the track
            Func<int, bool> pitBlend = i =>
            {
                if (!td.InPitZone(td.dist[i])) return false;
                float sd = td.SignedDistance(td.dist[i]);
                return sd < td.pitEntry + td.pitBlend || sd > td.pitExit - td.pitBlend;
            };
            Strip(mb, td, EDGE, i => -hw - kw, i => -hw, i => !td.kerb[i], 10f, 10f);
            Strip(mb, td, EDGE, i => hw, i => hw + kw, i => !td.kerb[i] && !pitBlend(i), 10f, 10f);
            Strip(mb, td, PIT, i => hw, i => hw + kw, i => !td.kerb[i] && pitBlend(i), 10f, 10f);

            float near = hw + kw + TrackData.GrassBand;
            // left run-off
            if (paved)
            {
                Strip(mb, td, RUNOFF, i => -leftLim[i], i => -hw - kw, all, 12f, 12f);
            }
            else
            {
                Strip(mb, td, GRASS, i => -Mathf.Min(near, leftLim[i]), i => -hw - kw, all, 12f, 12f);
                Strip(mb, td, GRAVEL, i => -leftLim[i], i => -Mathf.Min(near, leftLim[i]), i => td.IsGravelCorner(i), 6f, 6f);
                Strip(mb, td, GRASS, i => -leftLim[i], i => -Mathf.Min(near, leftLim[i]), i => !td.IsGravelCorner(i), 12f, 12f);
            }

            // right run-off (normal) / pit area
            Func<int, bool> pitZone = i => td.InPitZone(td.dist[i]);
            Func<int, bool> noPit = i => !td.InPitZone(td.dist[i]);
            if (paved)
            {
                Strip(mb, td, RUNOFF, i => hw + kw, i => rightLim[i], noPit, 12f, 12f);
            }
            else
            {
                Strip(mb, td, GRASS, i => hw + kw, i => Mathf.Min(near, rightLim[i]), noPit, 12f, 12f);
                Strip(mb, td, GRAVEL, i => Mathf.Min(near, rightLim[i]), i => rightLim[i], i => !td.InPitZone(td.dist[i]) && td.IsGravelCorner(i), 6f, 6f);
                Strip(mb, td, GRASS, i => Mathf.Min(near, rightLim[i]), i => rightLim[i], i => !td.InPitZone(td.dist[i]) && !td.IsGravelCorner(i), 12f, 12f);
            }
            // pit zone: verge -> fast lane -> working lane -> apron up to the garages
            Func<int, float> laneIn = i => Mathf.Max(hw + kw, pitOff[i] - 3f);
            Func<int, float> laneOut = i => Mathf.Max(hw + kw, pitOff[i] + 3f);
            Func<int, float> boxOut = i => Mathf.Max(laneOut(i), pitOff[i] + 8.5f);
            Strip(mb, td, paved ? RUNOFF : GRASS, i => hw + kw, laneIn, i => pitZone(i) && !pitBlend(i), 12f, 12f);
            Strip(mb, td, PIT, i => hw + kw, laneIn, pitBlend, 10f, 10f);
            Strip(mb, td, PIT, laneIn, laneOut, pitZone, 10f, 10f);
            Strip(mb, td, CONCRETE, laneOut, boxOut, pitZone, 8f, 8f);
            Strip(mb, td, paved ? RUNOFF : GRASS, boxOut, i => rightLim[i], pitZone, 12f, 12f);

            var asphalt = ProceduralTextures.Asphalt();
            var mats = new Material[10];
            mats[ROAD] = MaterialFactory.Lit(new Color(0.78f, 0.78f, 0.8f), 0.12f, 0f, asphalt);
            mats[LINE] = MaterialFactory.Lit(new Color(0.95f, 0.95f, 0.95f), 0.2f);
            mats[KERB] = MaterialFactory.Lit(Color.white, 0.25f, 0f, ProceduralTextures.Stripes(new Color(0.85f, 0.1f, 0.1f), new Color(0.95f, 0.95f, 0.95f)));
            mats[GRASS] = MaterialFactory.Lit(Color.white, 0.05f, 0f, ProceduralTextures.Grass(td.def.grass));
            mats[GRAVEL] = MaterialFactory.Lit(Color.white, 0.05f, 0f, ProceduralTextures.Gravel());
            mats[RUNOFF] = MaterialFactory.Lit(new Color(0.62f, 0.64f, 0.7f), 0.1f, 0f, asphalt);
            mats[PIT] = MaterialFactory.Lit(new Color(0.9f, 0.9f, 0.92f), 0.12f, 0f, asphalt);
            mats[CONCRETE] = MaterialFactory.Lit(new Color(0.85f, 0.85f, 0.85f), 0.1f, 0f, ProceduralTextures.Concrete());
            mats[CHECKER] = MaterialFactory.Lit(Color.white, 0.2f, 0f, ProceduralTextures.Checker(8));
            mats[EDGE] = MaterialFactory.Lit(new Color(0.55f, 0.6f, 0.55f), 0.1f, 0f, asphalt);
            MeshObject("Surface", parent, mb.ToMesh("surface"), mats);
        }

        // ------------------------------------------------------------------ barriers
        static void Wall(MeshBuilder vis, MeshBuilder col, TrackData td, Func<int, float> off, Func<int, bool> use, bool rightSide, float height, float thick)
        {
            int n = td.count;
            for (int i = 0; i < n; i++)
            {
                if (!use(i)) continue;
                int j = (i + 1) % n;
                float o0 = off(i), o1 = off(j);
                float s = rightSide ? 1f : -1f;
                Vector3 f0 = P(td, i, o0), f1 = P(td, j, o1);
                Vector3 b0 = P(td, i, o0 + s * thick), b1 = P(td, j, o1 + s * thick);
                Vector3 up = Vector3.up * height;
                float u0 = td.dist[i] / 6f, u1 = (td.dist[i] + td.ds) / 6f;
                if (rightSide)
                {
                    vis.AddQuad(f1, f1 + up, f0 + up, f0, new Vector2(u1, 0), new Vector2(u0, 1));
                    vis.AddQuad(f0 + up, f1 + up, b1 + up, b0 + up, new Vector2(u0, 0), new Vector2(u1, 0.1f));
                    col.AddQuad(f1, f1 + up, f0 + up, f0);
                    col.AddQuad(b0, b0 + up, b1 + up, b1);
                    col.AddQuad(f0 + up, f1 + up, b1 + up, b0 + up);
                }
                else
                {
                    vis.AddQuad(f0, f0 + up, f1 + up, f1, new Vector2(u0, 0), new Vector2(u1, 1));
                    vis.AddQuad(f1 + up, f0 + up, b0 + up, b1 + up, new Vector2(u1, 0), new Vector2(u0, 0.1f));
                    col.AddQuad(f0, f0 + up, f1 + up, f1);
                    col.AddQuad(b1, b1 + up, b0 + up, b0);
                    col.AddQuad(f1 + up, f0 + up, b0 + up, b1 + up);
                }
            }
        }

        static void BuildBarriers(TrackData td, Transform parent)
        {
            var vis = new MeshBuilder();
            var col = new MeshBuilder();
            Wall(vis, col, td, i => -leftLim[i], i => true, false, 1.1f, 0.5f);
            // no barrier in front of the garages: the building itself is the wall there
            float g0 = td.BoxDistance(0) - td.boxSpacing * 0.5f - 6f;
            float g1 = td.BoxDistance(GameData.Teams.Length - 1) + td.boxSpacing * 0.5f + 6f;
            Wall(vis, col, td, i => rightLim[i], i => { float sd = td.SignedDistance(td.dist[i]); return sd < g0 || sd > g1; }, true, 1.1f, 0.5f);
            Color a, b;
            switch (td.def.scenery)
            {
                case Scenery.Street: a = new Color(0.85f, 0.85f, 0.85f); b = new Color(0.2f, 0.35f, 0.7f); break;
                case Scenery.Desert: a = new Color(0.9f, 0.5f, 0.1f); b = new Color(0.95f, 0.95f, 0.9f); break;
                case Scenery.Blossom: a = new Color(0.95f, 0.6f, 0.75f); b = new Color(0.95f, 0.95f, 0.95f); break;
                default: a = new Color(0.15f, 0.3f, 0.75f); b = new Color(0.95f, 0.95f, 0.95f); break;
            }
            var mat = MaterialFactory.Lit(Color.white, 0.3f, 0f, ProceduralTextures.Stripes(a, b));
            var vm = vis.ToMesh("barrierVis");
            MeshObject("Barriers", parent, vm, new[] { mat }, true);
            var cg = new GameObject("BarrierCollider");
            cg.transform.SetParent(parent, false);
            cg.AddComponent<MeshCollider>().sharedMesh = col.ToMesh("barrierCol");
        }

        // ------------------------------------------------------------------ pit complex
        static void BuildPitComplex(TrackData td, Transform parent)
        {
            float hw = td.halfWidth, kw = TrackData.KerbWidth;
            float fullStart = td.pitEntry + td.pitBlend, fullEnd = td.pitExit - td.pitBlend;
            // pit wall between the track and the fast lane
            var vis = new MeshBuilder();
            var col = new MeshBuilder();
            float wallOff = (hw + kw + td.pitLaneOffset - 3f) * 0.5f;
            Func<int, bool> wallUse = i => { float sd = td.SignedDistance(td.dist[i]); return sd > fullStart - 10f && sd < fullEnd + 10f; };
            Wall(vis, col, td, i => wallOff, wallUse, true, 1.0f, 0.6f);
            MeshObject("PitWall", parent, vis.ToMesh("pitWall"), new[] { MaterialFactory.Lit(new Color(0.9f, 0.9f, 0.9f), 0.2f, 0f, ProceduralTextures.Concrete()) }, true);
            var cg = new GameObject("PitWallCollider");
            cg.transform.SetParent(parent, false);
            cg.AddComponent<MeshCollider>().sharedMesh = col.ToMesh("pitWallCol");

            // garages: one straight building along the chord of the box area
            int teams = GameData.Teams.Length;
            float d0 = td.BoxDistance(0) - td.boxSpacing * 0.5f - 6f;
            float d1 = td.BoxDistance(teams - 1) + td.boxSpacing * 0.5f + 6f;
            float front = td.pitLaneOffset + 9.5f;
            Vector3 a = td.PositionAt(d0) + td.RightAt(d0) * front;
            Vector3 b = td.PositionAt(d1) + td.RightAt(d1) * front;
            Vector3 along = (b - a);
            float len = along.magnitude;
            along /= len;
            Vector3 outDir = new Vector3(along.z, 0f, -along.x); // away from the track (right of travel)
            var g = new GameObject("PitBuilding");
            g.transform.SetParent(parent, false);
            g.transform.position = a;
            g.transform.rotation = Quaternion.LookRotation(along, Vector3.up);
            // local frame: z along the building, +x away from track, y up
            var mb = new MeshBuilder(2 + teams);
            const float depth = 14f, height = 8f, doorH = 4.2f, bandH = 1.2f;
            mb.Sub(0);
            mb.AddBox(new Vector3(depth * 0.5f + 0.3f, height * 0.5f, len * 0.5f), new Vector3(depth, height, len), Quaternion.identity, 0.1f); // main block
            mb.AddBox(new Vector3(depth * 0.5f - 1.5f, height + 0.25f, len * 0.5f), new Vector3(depth + 3f, 0.5f, len + 2f), Quaternion.identity, 0.1f); // roof overhang
            for (int t = 0; t < teams; t++)
            {
                float dz = (td.BoxDistance(t) - d0);
                // dark garage opening (slightly in front of the facade) + team colour band above it
                mb.Sub(1);
                mb.AddBox(new Vector3(0.2f, doorH * 0.5f, dz), new Vector3(0.12f, doorH, td.boxSpacing * 0.78f), Quaternion.identity);
                mb.Sub(2 + t);
                mb.AddBox(new Vector3(0.2f, doorH + 0.3f + bandH * 0.5f, dz), new Vector3(0.12f, bandH, td.boxSpacing * 0.9f), Quaternion.identity);
            }
            var mats = new Material[2 + teams];
            mats[0] = MaterialFactory.Lit(new Color(0.88f, 0.89f, 0.92f), 0.3f, 0f, ProceduralTextures.Concrete());
            mats[1] = MaterialFactory.Lit(new Color(0.06f, 0.06f, 0.07f), 0.2f);
            for (int t = 0; t < teams; t++) mats[2 + t] = MaterialFactory.Lit(GameData.Teams[t].primary, 0.5f);
            var bg = MeshObject("Garages", g.transform, mb.ToMesh("garages"), mats, true);
            bg.AddComponent<MeshCollider>().sharedMesh = bg.GetComponent<MeshFilter>().sharedMesh;

            // painted box markings in team colours
            var marks = new MeshBuilder(teams);
            for (int t = 0; t < teams; t++)
            {
                marks.Sub(t);
                float d = td.BoxDistance(t);
                Vector3 c = td.PositionAt(d) + td.RightAt(d) * td.BoxLaneOffset;
                c.y = 0.015f;
                Vector3 f = td.TangentAt(d), r = td.RightAt(d);
                float hl = 3.2f, hwid = 1.4f, bar = 0.18f;
                // U-shaped frame
                AddFlat(marks, c + f * hl, f, r, bar, hwid * 2f);
                AddFlat(marks, c - f * hl, f, r, bar, hwid * 2f);
                AddFlat(marks, c + r * hwid, f, r, hl * 2f, bar);
                AddFlat(marks, c - r * hwid, f, r, hl * 2f, bar);
            }
            var markMats = new Material[teams];
            for (int t = 0; t < teams; t++) markMats[t] = MaterialFactory.Lit(GameData.Teams[t].primary, 0.3f);
            MeshObject("BoxMarks", parent, marks.ToMesh("boxMarks"), markMats);

            // a few crates / equipment in front of the garages
            var crate = MeshPack.Load("crate");
            if (crate != null)
            {
                for (int t = 0; t < teams; t++)
                {
                    float d = td.BoxDistance(t) + td.boxSpacing * 0.35f;
                    Vector3 c = td.PositionAt(d) + td.RightAt(d) * (td.pitLaneOffset + 8.8f);
                    var go = crate.Instantiate(parent, "Crate");
                    go.transform.position = c;
                    go.transform.rotation = Quaternion.LookRotation(td.TangentAt(d));
                    go.transform.localScale = Vector3.one * 1.2f;
                }
            }
        }

        static void AddFlat(MeshBuilder mb, Vector3 c, Vector3 f, Vector3 r, float lenF, float lenR)
        {
            Vector3 hf = f * lenF * 0.5f, hr = r * lenR * 0.5f;
            mb.AddQuad(c - hf - hr, c + hf - hr, c + hf + hr, c - hf + hr);
        }

        // ------------------------------------------------------------------ start gantry & grid
        static Material[] BuildStartGantry(TrackData td, Transform parent)
        {
            float hw = td.halfWidth;
            Vector3 c = td.PositionAt(0f);
            Vector3 f = td.TangentAt(0f), r = td.RightAt(0f);
            var g = new GameObject("StartGantry");
            g.transform.SetParent(parent, false);
            g.transform.position = c;
            g.transform.rotation = Quaternion.LookRotation(f);
            var mb = new MeshBuilder();
            float span = hw + 2.5f;
            mb.AddBox(new Vector3(-span, 3.5f, 0), new Vector3(0.5f, 7f, 0.5f), Quaternion.identity);
            mb.AddBox(new Vector3(span, 3.5f, 0), new Vector3(0.5f, 7f, 0.5f), Quaternion.identity);
            mb.AddBox(new Vector3(0, 6.6f, 0), new Vector3(span * 2f + 0.5f, 0.8f, 0.6f), Quaternion.identity);
            mb.AddBox(new Vector3(0, 5.7f, -0.05f), new Vector3(4.6f, 1.1f, 0.5f), Quaternion.identity);
            MeshObject("Gantry", g.transform, mb.ToMesh("gantry"), new[] { MaterialFactory.Lit(new Color(0.15f, 0.15f, 0.17f), 0.4f, 0.4f) }, true);
            var lights = new Material[5];
            for (int k = 0; k < 5; k++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.DestroyImmediate(s.GetComponent<Collider>());
                s.transform.SetParent(g.transform, false);
                s.transform.localPosition = new Vector3(-1.8f + k * 0.9f, 5.7f, -0.35f);
                s.transform.localScale = Vector3.one * 0.55f;
                lights[k] = MaterialFactory.Emissive(new Color(0.25f, 0.02f, 0.02f), Color.black);
                s.GetComponent<Renderer>().sharedMaterial = lights[k];
            }
            return lights;
        }

        public static Vector3 GridSlot(TrackData td, int slot, out Quaternion rot)
        {
            float d = -(12f + slot * 8f);
            float side = (slot % 2 == 0) ? -2.7f : 2.7f;
            Vector3 p = td.PositionAt(d) + td.RightAt(d) * side;
            rot = Quaternion.LookRotation(td.TangentAt(d));
            return p;
        }

        static void BuildGridMarks(TrackData td, Transform parent)
        {
            var mb = new MeshBuilder();
            for (int s = 0; s < 20; s++)
            {
                Vector3 p = GridSlot(td, s, out var rot);
                Vector3 f = rot * Vector3.forward, r = rot * Vector3.right;
                Vector3 front = p + f * 3.2f;
                front.y = 0.012f;
                AddFlat(mb, front, f, r, 0.2f, 2.4f);
                AddFlat(mb, front - f * 0.6f + r * 1.1f, f, r, 1.2f, 0.2f);
                AddFlat(mb, front - f * 0.6f - r * 1.1f, f, r, 1.2f, 0.2f);
            }
            MeshObject("GridMarks", parent, mb.ToMesh("grid"), new[] { MaterialFactory.Lit(new Color(0.95f, 0.95f, 0.95f), 0.2f) });
        }

        // ------------------------------------------------------------------ grandstands
        static Texture2D crowdTex;

        static Texture2D CrowdTexture()
        {
            if (crowdTex != null) return crowdTex;
            var rnd = new Random(5);
            crowdTex = new Texture2D(64, 8, TextureFormat.RGBA32, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < 8; y++)
                for (int x = 0; x < 64; x++)
                {
                    Color c;
                    double v = rnd.NextDouble();
                    if (v < 0.15) c = new Color(0.2f, 0.2f, 0.25f);
                    else
                    {
                        var team = GameData.Teams[rnd.Next(GameData.Teams.Length)];
                        c = rnd.NextDouble() < 0.5 ? team.primary : Color.Lerp(team.secondary, new Color(0.9f, 0.75f, 0.6f), 0.4f);
                    }
                    crowdTex.SetPixel(x, y, c);
                }
            crowdTex.Apply();
            return crowdTex;
        }

        static void Grandstand(Transform parent, Vector3 frontCenter, Vector3 along, Vector3 away, float length, int rows)
        {
            var g = new GameObject("Grandstand");
            g.transform.SetParent(parent, false);
            g.transform.position = frontCenter;
            g.transform.rotation = Quaternion.LookRotation(along, Vector3.up);
            // local: +x = away from the track
            if (Vector3.Dot(g.transform.right, away) < 0f) g.transform.rotation = Quaternion.LookRotation(-along, Vector3.up);
            var mb = new MeshBuilder(3);
            const float rowD = 1.1f, rowH = 0.65f;
            for (int k = 0; k < rows; k++)
            {
                mb.Sub(1);
                float x = 1f + k * rowD, y = 1.2f + k * rowH;
                // crowd riser (faces the track: -x)
                mb.AddQuad(new Vector3(x, y - rowH, length * 0.5f), new Vector3(x, y, length * 0.5f), new Vector3(x, y, -length * 0.5f), new Vector3(x, y - rowH, -length * 0.5f),
                    Vector2.zero, new Vector2(length / 6f, 1f));
                mb.Sub(0);
                mb.AddQuad(new Vector3(x, y, -length * 0.5f), new Vector3(x, y, length * 0.5f), new Vector3(x + rowD, y, length * 0.5f), new Vector3(x + rowD, y, -length * 0.5f));
            }
            float topX = 1f + rows * rowD, topY = 1.2f + rows * rowH;
            mb.Sub(0);
            mb.AddBox(new Vector3(topX * 0.5f + 0.5f, 0.6f, 0), new Vector3(topX + 1f, 1.2f, length), Quaternion.identity);
            mb.AddBox(new Vector3(topX + 0.3f, topY * 0.5f + 1f, 0), new Vector3(0.6f, topY + 2f, length), Quaternion.identity);
            mb.Sub(2);
            mb.AddBox(new Vector3(topX * 0.5f + 0.5f, topY + 3f, 0), new Vector3(topX + 2f, 0.3f, length + 1f), Quaternion.Euler(0, 0, -6f));
            for (float z = -length * 0.5f; z <= length * 0.5f + 0.1f; z += 12f)
                mb.Sub(0).AddBox(new Vector3(topX + 0.3f, topY + 1.5f, z), new Vector3(0.3f, 3f, 0.3f), Quaternion.identity);
            var mats = new[]
            {
                MaterialFactory.Lit(new Color(0.75f, 0.76f, 0.8f), 0.2f, 0f, ProceduralTextures.Concrete()),
                MaterialFactory.Lit(Color.white, 0.1f, 0f, CrowdTexture()),
                MaterialFactory.Lit(new Color(0.85f, 0.88f, 0.92f), 0.5f, 0.3f)
            };
            MeshObject("Stand", g.transform, mb.ToMesh("stand"), mats, true);
        }

        static void BuildGrandstands(TrackData td, Transform parent)
        {
            // main straight, opposite the pits
            {
                float d = 0f;
                float off = -leftLim[0] - 2f;
                Vector3 c = td.PositionAt(d) + td.RightAt(d) * off;
                Grandstand(parent, c, td.TangentAt(d), -td.RightAt(d), Mathf.Min(220f, (td.pitExit - td.pitEntry) * 0.6f), 10);
            }
            // outside of the three tightest corners
            var used = new List<float>();
            int placed = 0;
            var order = new List<int>();
            for (int i = 0; i < td.count; i++) order.Add(i);
            order.Sort((x, y) => Mathf.Abs(td.curvature[y]).CompareTo(Mathf.Abs(td.curvature[x])));
            int want = td.def.scenery == Scenery.Speedway ? 5 : 3;
            foreach (int i in order)
            {
                if (placed >= want) break;
                float d = td.dist[i];
                bool near = false;
                foreach (var u in used) if (Mathf.Abs(td.SignedDistance(d - u)) < 300f) near = true;
                if (near || td.InPitZone(d) || Mathf.Abs(td.SignedDistance(d)) < 200f) continue;
                used.Add(d);
                bool outsideRight = td.curvature[i] > 0f; // left-hander -> outside is right
                float off = outsideRight ? rightLim[i] + 3f : -leftLim[i] - 3f;
                Vector3 c = td.PositionAt(d) + td.RightAt(d) * off;
                Vector3 away = td.RightAt(d) * (outsideRight ? 1f : -1f);
                Grandstand(parent, c, td.TangentAt(d), away, 70f, 7);
                placed++;
            }
        }

        // ------------------------------------------------------------------ scenery (CC0 models)
        static float NearestCenterDist(TrackData td, Vector3 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < td.count; i += 4)
            {
                float dx = td.center[i].x - p.x, dz = td.center[i].z - p.z;
                float d2 = dx * dx + dz * dz;
                if (d2 < best) best = d2;
            }
            return Mathf.Sqrt(best);
        }

        static void Place(MeshPack pack, Transform parent, Vector3 pos, float yaw, float scale, Func<string, Color, Color> remap = null, bool shadows = true)
        {
            if (pack == null) return;
            var go = pack.Instantiate(parent, "Prop", remap, shadows);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
        }

        static void BuildScenery(TrackData td, Transform parent)
        {
            var rnd = new Random(td.def.id.GetHashCode());
            var props = new GameObject("Scenery").transform;
            props.SetParent(parent, false);
            float maxLim = td.barrierOffset + 4f;

            // pit-building exclusion rectangle (in track distance / offset terms)
            bool NearPits(Vector3 p)
            {
                int i = td.FindIndex(p, -1);
                float sd = td.SignedDistance(td.dist[i]);
                float off = td.LateralOffset(i, p);
                return sd > td.pitEntry - 40f && sd < td.pitExit + 40f && off > 0f && off < td.pitLaneOffset + 45f;
            }

            Vector3 lo = td.boundsMin - new Vector3(260f, 0, 260f), hi = td.boundsMax + new Vector3(260f, 0, 260f);
            Vector3 RandomPoint() => new Vector3(Mathf.Lerp(lo.x, hi.x, (float)rnd.NextDouble()), 0f, Mathf.Lerp(lo.z, hi.z, (float)rnd.NextDouble()));

            (string id, float height, float weight)[] trees;
            Func<string, Color, Color> remap = null;
            switch (td.def.scenery)
            {
                case Scenery.Forest:
                    trees = new[] { ("tree_pine", 13f, 3f), ("tree_pine_tall", 17f, 3f), ("tree_fat", 8f, 0.5f), ("rock_large", 3f, 0.6f) };
                    break;
                case Scenery.Desert:
                    trees = new[] { ("tree_palm", 11f, 2f), ("rock_large", 4f, 2f), ("bush_large", 1.6f, 1f), ("tent", 4f, 0.4f) };
                    break;
                case Scenery.Blossom:
                    trees = new[] { ("tree_oak", 9f, 3f), ("tree_autumn", 10f, 1.5f), ("bush_large", 1.8f, 1f), ("tree_pine", 11f, 0.5f) };
                    remap = (part, c) => (c.g > c.r + 0.15f) ? new Color(0.98f, 0.62f, 0.78f) : c; // cherry blossom leaves
                    break;
                case Scenery.Street:
                    trees = new[] { ("tree_palm", 10f, 1f), ("bush_large", 1.6f, 1f) };
                    break;
                case Scenery.Speedway:
                    trees = new[] { ("tree_oak", 9f, 1f), ("tree_fat", 8f, 1f) };
                    break;
                default:
                    trees = new[] { ("tree_oak", 9f, 2f), ("tree_fat", 8f, 1.5f), ("tree_pine", 12f, 1f), ("bush_large", 1.8f, 1f), ("tent", 4f, 0.2f) };
                    break;
            }
            if (remap == null) remap = (part, c) => (c.g > c.r + 0.15f) ? new Color(c.r * 0.8f, c.g * 0.85f, c.b * 0.45f) : c; // natural foliage green
            float totalW = 0f;
            foreach (var t in trees) totalW += t.weight;
            int treeCount = td.def.scenery == Scenery.Street ? 120 : td.def.scenery == Scenery.Speedway ? 160 : td.def.scenery == Scenery.Forest ? 650 : 420;
            int attempts = 0, placed = 0;
            while (placed < treeCount && attempts < treeCount * 8)
            {
                attempts++;
                Vector3 p = RandomPoint();
                float d = NearestCenterDist(td, p);
                if (d < maxLim + 6f || d > 260f) continue;
                if (NearPits(p)) continue;
                double r = rnd.NextDouble() * totalW;
                var pick = trees[0];
                foreach (var t in trees) { r -= t.weight; if (r <= 0) { pick = t; break; } }
                var pack = MeshPack.Load(pick.id);
                if (pack == null) continue;
                float h = pack.bounds.size.y > 0.01f ? pack.bounds.size.y : 1f;
                float scale = pick.height / h * (0.75f + (float)rnd.NextDouble() * 0.5f);
                Place(pack, props, p, (float)rnd.NextDouble() * 360f, scale, remap, td.def.scenery != Scenery.Forest || placed % 2 == 0);
                placed++;
            }

            // buildings: dense city for the street circuit, distant skyline elsewhere
            string[] blds = { "building_a", "building_c", "building_e" };
            string[] towers = { "skyscraper_a", "skyscraper_b" };
            if (td.def.scenery == Scenery.Street)
            {
                for (int i = 0; i < td.count; i += 12)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float lim = side > 0 ? rightLim[i] : leftLim[i];
                        float off = side * (lim + 14f + (float)rnd.NextDouble() * 6f);
                        Vector3 p = td.center[i] + td.right[i] * off;
                        if (NearestCenterDist(td, p) < lim + 10f || NearPits(p)) continue;
                        bool tower = rnd.NextDouble() < 0.18;
                        var pack = MeshPack.Load(tower ? towers[rnd.Next(2)] : blds[rnd.Next(3)]);
                        if (pack == null) continue;
                        float h = Mathf.Max(0.1f, pack.bounds.size.y);
                        float target = tower ? 45f + (float)rnd.NextDouble() * 30f : 11f + (float)rnd.NextDouble() * 9f;
                        Vector3 toTrack = -td.right[i] * side;
                        Place(pack, props, p, Quaternion.LookRotation(toTrack).eulerAngles.y, target / h);
                    }
                }
            }
            else
            {
                int n = td.def.scenery == Scenery.Speedway ? 30 : 14;
                for (int k = 0; k < n; k++)
                {
                    Vector3 p = RandomPoint();
                    float d = NearestCenterDist(td, p);
                    if (d < 180f) { k--; if (attempts++ > 4000) break; continue; }
                    bool tower = rnd.NextDouble() < 0.35;
                    var pack = MeshPack.Load(tower ? towers[rnd.Next(2)] : blds[rnd.Next(3)]);
                    if (pack == null) continue;
                    float h = Mathf.Max(0.1f, pack.bounds.size.y);
                    Place(pack, props, p, (float)rnd.NextDouble() * 360f, (tower ? 50f : 14f) / h);
                }
            }
        }

        // ------------------------------------------------------------------ lighting / sky
        static void SetupEnvironment(TrackData td, Transform parent, TrackWorld world)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(parent, false);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            Color sky, equator, ground, fog;
            float yaw = 35f, pitch = 48f, intensity = 1.15f;
            switch (td.def.scenery)
            {
                case Scenery.Desert:
                    sun.color = new Color(1f, 0.93f, 0.8f); sky = new Color(0.55f, 0.7f, 0.95f); equator = new Color(0.85f, 0.75f, 0.6f);
                    ground = new Color(0.55f, 0.45f, 0.3f); fog = new Color(0.85f, 0.8f, 0.72f); intensity = 1.3f; pitch = 58f; break;
                case Scenery.Forest:
                    sun.color = new Color(1f, 0.96f, 0.9f); sky = new Color(0.45f, 0.58f, 0.75f); equator = new Color(0.5f, 0.58f, 0.55f);
                    ground = new Color(0.2f, 0.25f, 0.18f); fog = new Color(0.68f, 0.75f, 0.8f); intensity = 1.0f; pitch = 38f; break;
                case Scenery.Blossom:
                    sun.color = new Color(1f, 0.92f, 0.88f); sky = new Color(0.6f, 0.65f, 0.85f); equator = new Color(0.75f, 0.68f, 0.72f);
                    ground = new Color(0.3f, 0.32f, 0.25f); fog = new Color(0.85f, 0.8f, 0.85f); pitch = 34f; yaw = 120f; break;
                case Scenery.Street:
                    sun.color = new Color(1f, 0.9f, 0.78f); sky = new Color(0.5f, 0.62f, 0.85f); equator = new Color(0.7f, 0.68f, 0.65f);
                    ground = new Color(0.35f, 0.33f, 0.3f); fog = new Color(0.8f, 0.82f, 0.85f); pitch = 30f; yaw = 210f; break;
                default:
                    sun.color = new Color(1f, 0.96f, 0.9f); sky = new Color(0.5f, 0.65f, 0.9f); equator = new Color(0.65f, 0.7f, 0.7f);
                    ground = new Color(0.3f, 0.32f, 0.25f); fog = new Color(0.75f, 0.82f, 0.9f); break;
            }
            sun.intensity = intensity;
            sunGo.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = equator;
            RenderSettings.ambientGroundColor = ground;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = fog;
            RenderSettings.fogStartDistance = 450f;
            RenderSettings.fogEndDistance = 3200f;
            var skyMat = Resources.Load<Material>("Materials/F1_Sky");
            if (skyMat != null)
            {
                skyMat = new Material(skyMat);
                if (skyMat.HasProperty("_SkyTint")) skyMat.SetColor("_SkyTint", Color.Lerp(sky, Color.white, 0.3f));
                if (skyMat.HasProperty("_GroundColor")) skyMat.SetColor("_GroundColor", fog);
                if (skyMat.HasProperty("_AtmosphereThickness")) skyMat.SetFloat("_AtmosphereThickness", td.def.scenery == Scenery.Desert ? 0.8f : 1.0f);
                RenderSettings.skybox = skyMat;
            }
            else
            {
                RenderSettings.skybox = null;
            }
            world.skyColor = fog;
        }

        // ------------------------------------------------------------------ minimap
        static Texture2D BuildMinimap(TrackData td, out Vector2 min, out float size)
        {
            const int N = 256;
            Vector3 bmin = td.boundsMin, bmax = td.boundsMax;
            float span = Mathf.Max(bmax.x - bmin.x, bmax.z - bmin.z) * 1.12f;
            Vector2 center = new Vector2((bmin.x + bmax.x) * 0.5f, (bmin.z + bmax.z) * 0.5f);
            min = center - Vector2.one * span * 0.5f;
            size = span;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[N * N];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(0, 0, 0, 0);
            void Disc(float x, float y, float r, Color32 c)
            {
                int x0 = Mathf.FloorToInt(x - r), x1 = Mathf.CeilToInt(x + r), y0 = Mathf.FloorToInt(y - r), y1 = Mathf.CeilToInt(y + r);
                for (int yy = Mathf.Max(0, y0); yy <= Mathf.Min(N - 1, y1); yy++)
                    for (int xx = Mathf.Max(0, x0); xx <= Mathf.Min(N - 1, x1); xx++)
                    {
                        float dx = xx - x, dy = yy - y;
                        if (dx * dx + dy * dy <= r * r) px[yy * N + xx] = c;
                    }
            }
            Vector2 ToPx(Vector3 w, Vector2 mn, float sz) => new Vector2((w.x - mn.x) / sz * N, (w.z - mn.y) / sz * N);
            var mn2 = min; var sz2 = size;
            for (int i = 0; i < td.count; i++) { var p = ToPx(td.center[i], mn2, sz2); Disc(p.x, p.y, 4.2f, new Color32(20, 20, 24, 220)); }
            for (float d = td.pitEntry; d <= td.pitExit; d += 2f) { var p = ToPx(td.PitLanePoint(d), mn2, sz2); Disc(p.x, p.y, 1.5f, new Color32(140, 140, 150, 255)); }
            for (int i = 0; i < td.count; i++) { var p = ToPx(td.center[i], mn2, sz2); Disc(p.x, p.y, 2.4f, new Color32(235, 235, 240, 255)); }
            var s0 = ToPx(td.center[0], mn2, sz2);
            Disc(s0.x, s0.y, 3.5f, new Color32(230, 40, 40, 255));
            tex.SetPixels32(px);
            tex.Apply();
            return tex;
        }
    }
}
