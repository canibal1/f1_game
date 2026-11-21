using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    public enum SurfaceType { Asphalt, Kerb, Grass, Gravel, PitLane }

    /// <summary>
    /// Pure-data description of a circuit: closed centripetal Catmull-Rom spline resampled every 2 m,
    /// kerbs, racing line, AI speed profile, pit lane path and surface lookup. No Unity objects,
    /// so it can be unit-tested outside the engine.
    /// </summary>
    public class TrackData
    {
        public TrackDef def;
        public int count;
        public float length;
        public float ds;
        public Vector3[] center, tangent, right;
        public float[] curvature; // signed 1/R, + = left-hander
        public float[] dist;
        public bool[] kerb;
        public float halfWidth;
        public const float KerbWidth = 1.3f;

        // racing line + speed profile
        public float[] lineOffset;
        public Vector3[] linePos;
        public float[] lineSpeed;
        public float estimatedLapTime;

        // pit lane (distances along the main track; entry is negative = before the line)
        public float pitEntry, pitExit;
        public float pitLaneOffset;
        public float pitBlend = 70f;
        public float boxStart, boxSpacing;
        public float pitSpeedLimit = 22.2f; // 80 km/h
        public float drsZoneStart, drsZoneEnd, drsDetection;

        public float barrierOffset;
        public Vector3 boundsMin, boundsMax;

        public static TrackData Build(TrackDef def, CarSetup referenceSetup = null)
        {
            var t = new TrackData { def = def };
            t.halfWidth = def.width * 0.5f;
            t.barrierOffset = t.halfWidth + KerbWidth + def.runoff;
            t.BuildSpline(def.points, 2f);
            t.BuildKerbs();
            t.BuildRacingLine();
            t.BuildSpeedProfile(referenceSetup ?? CarSetup.Preset(def.recommended));
            t.BuildPitLane();
            return t;
        }

        // ------------------------------------------------------------ spline
        static Vector2 CatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float s)
        {
            float T(float ti, Vector2 a, Vector2 b) => ti + Mathf.Sqrt(Mathf.Max((b - a).magnitude, 1e-4f));
            float t0 = 0f, t1 = T(t0, p0, p1), t2 = T(t1, p1, p2), t3 = T(t2, p2, p3);
            float t = Mathf.LerpUnclamped(t1, t2, s);
            Vector2 a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            Vector2 a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            Vector2 a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            Vector2 b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            Vector2 b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }

        void BuildSpline(Vector2[] pts, float step)
        {
            int n = pts.Length;
            const int perSeg = 40;
            var dense = new List<Vector2>(n * perSeg + 1);
            for (int i = 0; i < n; i++)
            {
                Vector2 p0 = pts[(i - 1 + n) % n], p1 = pts[i], p2 = pts[(i + 1) % n], p3 = pts[(i + 2) % n];
                for (int s = 0; s < perSeg; s++) dense.Add(CatmullRom(p0, p1, p2, p3, s / (float)perSeg));
            }
            dense.Add(dense[0]);
            var cum = new float[dense.Count];
            for (int i = 1; i < dense.Count; i++) cum[i] = cum[i - 1] + (dense[i] - dense[i - 1]).magnitude;
            length = cum[cum.Length - 1];
            count = Mathf.RoundToInt(length / step);
            ds = length / count;

            center = new Vector3[count];
            tangent = new Vector3[count];
            right = new Vector3[count];
            dist = new float[count];
            curvature = new float[count];
            int j = 0;
            for (int i = 0; i < count; i++)
            {
                float d = i * ds;
                while (j < cum.Length - 2 && cum[j + 1] < d) j++;
                float segLen = Mathf.Max(cum[j + 1] - cum[j], 1e-5f);
                Vector2 p = Vector2.Lerp(dense[j], dense[j + 1], (d - cum[j]) / segLen);
                center[i] = new Vector3(p.x, 0f, p.y);
                dist[i] = d;
            }
            for (int i = 0; i < count; i++)
            {
                Vector3 tg = (center[(i + 1) % count] - center[(i - 1 + count) % count]).normalized;
                tangent[i] = tg;
                right[i] = new Vector3(tg.z, 0f, -tg.x);
            }
            for (int i = 0; i < count; i++) curvature[i] = Curvature(center, i, 5);

            boundsMin = boundsMax = center[0];
            foreach (var c in center)
            {
                boundsMin = Vector3.Min(boundsMin, c);
                boundsMax = Vector3.Max(boundsMax, c);
            }
        }

        float Curvature(Vector3[] pts, int i, int k)
        {
            Vector3 a = pts[(i - k + count) % count], b = pts[i], c = pts[(i + k) % count];
            float ab = (b - a).magnitude, bc = (c - b).magnitude, ca = (a - c).magnitude;
            float cross = (b.x - a.x) * (c.z - b.z) - (b.z - a.z) * (c.x - b.x);
            float denom = ab * bc * ca;
            return denom > 1e-6f ? 2f * cross / denom : 0f;
        }

        void BuildKerbs()
        {
            kerb = new bool[count];
            for (int i = 0; i < count; i++)
            {
                if (Mathf.Abs(curvature[i]) > 1f / 220f)
                    for (int k = -8; k <= 8; k++) kerb[(i + k + count) % count] = true;
            }
        }

        // ------------------------------------------------------------ racing line
        void BuildRacingLine()
        {
            lineOffset = new float[count];
            float lim = halfWidth - 1.3f;
            var pos = new Vector3[count];
            int[] strides = { 16, 8, 4, 2 };
            int[] iters = { 80, 120, 160, 120 };
            for (int s = 0; s < strides.Length; s++)
            {
                int k = strides[s];
                for (int it = 0; it < iters[s]; it++)
                {
                    for (int i = 0; i < count; i++) pos[i] = center[i] + right[i] * lineOffset[i];
                    for (int i = 0; i < count; i++)
                    {
                        Vector3 mid = (pos[(i - k + count) % count] + pos[(i + k) % count]) * 0.5f;
                        float target = Vector3.Dot(mid - center[i], right[i]);
                        lineOffset[i] = Mathf.Clamp(Mathf.Lerp(lineOffset[i], target, 0.55f), -lim, lim);
                    }
                }
            }
            linePos = new Vector3[count];
            for (int i = 0; i < count; i++) linePos[i] = center[i] + right[i] * lineOffset[i];
        }

        public float LineCurvature(int i) => Curvature(linePos, i, 4);

        void BuildSpeedProfile(CarSetup s)
        {
            lineSpeed = new float[count];
            const float mass = 850f;
            float grip = Tyres.Grip(TyreCompound.Medium) * 0.93f;
            const float vMax = 104f;
            // corner limit (smoothed curvature so a single noisy sample doesn't create a fake corner)
            var kappa = new float[count];
            for (int i = 0; i < count; i++) kappa[i] = Mathf.Abs(LineCurvature(i));
            var smooth = new float[count];
            for (int i = 0; i < count; i++)
            {
                float sum = 0f;
                for (int k = -2; k <= 2; k++) sum += kappa[(i + k + count) % count];
                smooth[i] = Mathf.Max(sum / 5f, kappa[i] * 0.85f);
            }
            for (int i = 0; i < count; i++)
            {
                float r = smooth[i] > 1e-5f ? 1f / smooth[i] : 1e5f;
                float v = 30f;
                for (int it = 0; it < 8; it++) v = Mathf.Min(vMax, Mathf.Sqrt(VehicleSpec.LateralGrip(s, v, mass, grip) * r));
                lineSpeed[i] = v;
            }
            // Braking (backwards) and acceleration (forwards) passes, twice for the wrap-around.
            // Friction ellipse: grip used for cornering is not available for braking/traction,
            // so the plan brakes in a straight line instead of trail-braking deep into a tightening corner.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    int nx = (i + 1) % count;
                    float vn = lineSpeed[nx];
                    float load = mass * 9.81f + VehicleSpec.Downforce(s, vn);
                    float latMax = VehicleSpec.LateralGrip(s, vn, mass, grip);
                    float latUse = Mathf.Clamp01(vn * vn * smooth[nx] / Mathf.Max(latMax, 0.1f));
                    float ellipse = Mathf.Sqrt(Mathf.Max(0.12f, 1f - latUse * latUse));
                    float decel = (VehicleSpec.TyreMu * grip * VehicleSpec.LoadSensitivity(load / 4f) * load * 0.84f * ellipse + VehicleSpec.Drag(s, vn)) / mass;
                    lineSpeed[i] = Mathf.Min(lineSpeed[i], Mathf.Sqrt(vn * vn + 2f * decel * ds));
                }
                for (int i = 0; i < count; i++)
                {
                    int pv = (i - 1 + count) % count;
                    float vp = lineSpeed[pv];
                    float load = mass * 9.81f * (1f - VehicleSpec.FrontWeight) + VehicleSpec.Downforce(s, vp) * 0.55f;
                    float latMax = VehicleSpec.LateralGrip(s, vp, mass, grip);
                    float latUse = Mathf.Clamp01(vp * vp * smooth[pv] / Mathf.Max(latMax, 0.1f));
                    float ellipse = Mathf.Sqrt(Mathf.Max(0.15f, 1f - latUse * latUse));
                    float traction = VehicleSpec.TyreMu * grip * load * ellipse;
                    float f = Mathf.Min(VehicleSpec.MaxTractiveForce(s, Mathf.Max(vp, 1f), out _), traction);
                    float acc = (f - VehicleSpec.Drag(s, vp) - VehicleSpec.RollingResistance * mass * 9.81f) / mass;
                    lineSpeed[i] = Mathf.Min(lineSpeed[i], Mathf.Sqrt(Mathf.Max(0f, vp * vp + 2f * Mathf.Max(acc, 0f) * ds)));
                }
            }
            estimatedLapTime = 0f;
            for (int i = 0; i < count; i++) estimatedLapTime += ds / Mathf.Max(lineSpeed[i], 5f);
        }

        // ------------------------------------------------------------ pit lane
        float StraightExtent(int dir)
        {
            float s = 0f;
            int i = 0;
            while (s < 1500f)
            {
                if (Mathf.Abs(curvature[(i + count) % count]) > 1f / 400f) break;
                i += dir;
                s += ds;
            }
            return s;
        }

        void BuildPitLane()
        {
            float fwd = StraightExtent(1), back = StraightExtent(-1);
            pitExit = Mathf.Clamp(fwd - 25f, 120f, 330f);
            pitEntry = -Mathf.Clamp(back - 25f, 120f, 330f);
            pitLaneOffset = halfWidth + KerbWidth + 9.5f;
            float usable = (pitExit - pitBlend - 20f) - (pitEntry + pitBlend + 20f);
            boxSpacing = Mathf.Clamp(usable / GameData.Teams.Length, 9f, 15f);
            float boxesLen = boxSpacing * (GameData.Teams.Length - 1);
            float mid = ((pitEntry + pitBlend) + (pitExit - pitBlend)) * 0.5f;
            boxStart = mid - boxesLen * 0.5f;

            // DRS on the main straight, detection just before the pit entry
            drsDetection = pitEntry - 40f;
            drsZoneStart = 60f;
            drsZoneEnd = Mathf.Max(drsZoneStart + 80f, fwd - 60f);
        }

        /// <summary>Lateral offset of the pit lane centre at track distance d (d may be negative = before the line).</summary>
        public float PitOffsetAt(float d)
        {
            // the lane blends in/out from a line just inside the track edge, so cars rejoin on the asphalt
            float edge = halfWidth - 1.6f;
            if (d < pitEntry || d > pitExit) return edge;
            float a = Mathf.SmoothStep(0f, 1f, (d - pitEntry) / pitBlend);
            float b = Mathf.SmoothStep(0f, 1f, (pitExit - d) / pitBlend);
            return Mathf.Lerp(edge, pitLaneOffset, Mathf.Min(a, b));
        }

        public float BoxDistance(int teamIndex) => boxStart + boxSpacing * teamIndex;
        public float BoxLaneOffset => pitLaneOffset + 4.5f;

        public Vector3 PitLanePoint(float d, float extraOffset = 0f)
        {
            float dd = Wrap(d);
            return PositionAt(dd) + RightAt(dd) * (PitOffsetAt(d) + extraOffset);
        }

        /// <summary>Signed "pit distance": maps the track distance to the -L/2..L/2 range around the line.</summary>
        public float SignedDistance(float d)
        {
            d = Wrap(d);
            return d > length * 0.5f ? d - length : d;
        }

        public bool InPitZone(float d)
        {
            float sd = SignedDistance(d);
            return sd >= pitEntry && sd <= pitExit;
        }

        // ------------------------------------------------------------ queries
        public float Wrap(float d)
        {
            d %= length;
            if (d < 0f) d += length;
            return d;
        }

        public int IndexAt(float d) => Mathf.FloorToInt(Wrap(d) / ds) % count;

        public Vector3 PositionAt(float d)
        {
            d = Wrap(d);
            int i = Mathf.FloorToInt(d / ds) % count;
            float f = (d - i * ds) / ds;
            return Vector3.Lerp(center[i], center[(i + 1) % count], f);
        }

        public Vector3 LinePositionAt(float d)
        {
            d = Wrap(d);
            int i = Mathf.FloorToInt(d / ds) % count;
            float f = (d - i * ds) / ds;
            return Vector3.Lerp(linePos[i], linePos[(i + 1) % count], f);
        }

        public float LineOffsetAt(float d)
        {
            d = Wrap(d);
            int i = Mathf.FloorToInt(d / ds) % count;
            float f = (d - i * ds) / ds;
            return Mathf.Lerp(lineOffset[i], lineOffset[(i + 1) % count], f);
        }

        public float SpeedAt(float d)
        {
            d = Wrap(d);
            int i = Mathf.FloorToInt(d / ds) % count;
            float f = (d - i * ds) / ds;
            return Mathf.Lerp(lineSpeed[i], lineSpeed[(i + 1) % count], f);
        }

        public Vector3 TangentAt(float d)
        {
            d = Wrap(d);
            int i = Mathf.FloorToInt(d / ds) % count;
            float f = (d - i * ds) / ds;
            return Vector3.Lerp(tangent[i], tangent[(i + 1) % count], f).normalized;
        }

        public Vector3 RightAt(float d)
        {
            Vector3 t = TangentAt(d);
            return new Vector3(t.z, 0f, -t.x);
        }

        /// <summary>Nearest sample index. Uses a local window around <paramref name="hint"/> when possible.</summary>
        public int FindIndex(Vector3 p, int hint)
        {
            int best = -1;
            float bestD = float.MaxValue;
            if (hint >= 0 && hint < count)
            {
                for (int k = -30; k <= 30; k++)
                {
                    int i = (hint + k + count) % count;
                    float dx = center[i].x - p.x, dz = center[i].z - p.z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < bestD) { bestD = d2; best = i; }
                }
                if (bestD < 45f * 45f) return best;
            }
            bestD = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                float dx = center[i].x - p.x, dz = center[i].z - p.z;
                float d2 = dx * dx + dz * dz;
                if (d2 < bestD) { bestD = d2; best = i; }
            }
            return best;
        }

        public float LateralOffset(int i, Vector3 p) => Vector3.Dot(p - center[i], right[i]);

        public float DistanceAlong(int i, Vector3 p) => Wrap(dist[i] + Vector3.Dot(p - center[i], tangent[i]));

        public SurfaceType SurfaceAt(int i, Vector3 p, out float grip, out float drag)
        {
            if (i < 0) { grip = 1f; drag = 0f; return SurfaceType.Asphalt; }
            float off = LateralOffset(i, p);
            float a = Mathf.Abs(off);
            if (a <= halfWidth) { grip = 1f; drag = 0f; return SurfaceType.Asphalt; }
            if (a <= halfWidth + KerbWidth && kerb[i]) { grip = 0.96f; drag = 0.004f; return SurfaceType.Kerb; }
            float sd = SignedDistance(dist[i]);
            if (off > 0f && sd >= pitEntry - 5f && sd <= pitExit + 5f && off <= PitOffsetAt(sd) + 9f)
            { grip = 1f; drag = 0f; return SurfaceType.PitLane; }
            bool pavedRunoff = def.scenery == Scenery.Street || def.scenery == Scenery.Speedway;
            if (pavedRunoff) { grip = 0.9f; drag = 0.01f; return SurfaceType.Asphalt; }
            if (a <= halfWidth + KerbWidth + GrassBand) { grip = 0.7f; drag = 0.05f; return SurfaceType.Grass; }
            if (IsGravelCorner(i)) { grip = 0.45f; drag = 0.30f; return SurfaceType.Gravel; }
            grip = 0.6f; drag = 0.12f;
            return SurfaceType.Grass;
        }

        public const float GrassBand = 3f;

        /// <summary>Gravel traps sit on the run-off of corners tighter than 150 m.</summary>
        public bool IsGravelCorner(int i)
        {
            bool pavedRunoff = def.scenery == Scenery.Street || def.scenery == Scenery.Speedway;
            if (pavedRunoff) return false;
            for (int k = -6; k <= 6; k += 3)
                if (Mathf.Abs(curvature[(i + k + count) % count]) > 1f / 150f) return true;
            return false;
        }

        /// <summary>Barrier distance from the centre line on one side, pulled in on the inside of tight corners.</summary>
        public float SideLimit(int i, bool rightSide)
        {
            float k = curvature[i];
            bool inside = rightSide ? k < 0f : k > 0f;
            float lim = barrierOffset;
            if (inside && Mathf.Abs(k) > 1e-5f) lim = Mathf.Min(lim, 0.8f / Mathf.Abs(k));
            return Mathf.Max(lim, halfWidth + KerbWidth + 1.5f);
        }
    }
}
