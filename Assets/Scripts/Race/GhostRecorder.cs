using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    /// <summary>Time trial ghost: records the player's lap and replays the best valid lap as a translucent car.</summary>
    public class GhostRecorder : MonoBehaviour
    {
        struct Sample { public float t; public Vector3 p; public Quaternion r; }

        RaceManager race;
        RaceCar me;
        readonly List<Sample> current = new List<Sample>();
        List<Sample> best;
        readonly List<float> curMarks = new List<float>(); // lap time at every 10 m of the lap
        List<float> bestMarks;
        float lapStart = -1f;
        float sampleTimer;
        GameObject ghostCar;
        CarVisual ghostVisual;
        public float BestGhostTime { get; private set; } = float.MaxValue;
        public bool HasGhost => best != null;

        public void Init(RaceManager r, RaceCar rc)
        {
            race = r;
            me = rc;
        }

        public void StartLap(float t)
        {
            lapStart = t;
            current.Clear();
            curMarks.Clear();
        }

        /// <summary>Live delta to the best lap at the same point of the lap (negative = faster).</summary>
        public bool TryDelta(out float delta)
        {
            delta = 0f;
            if (bestMarks == null || curMarks.Count == 0 || lapStart < 0f) return false;
            int k = curMarks.Count - 1;
            if (k >= bestMarks.Count) return false;
            delta = curMarks[k] - bestMarks[k];
            return true;
        }

        public void LapCompleted(float lapTime, bool valid)
        {
            if (valid && lapTime < BestGhostTime && current.Count > 10)
            {
                BestGhostTime = lapTime;
                best = new List<Sample>(current);
                bestMarks = new List<float>(curMarks);
                EnsureGhostCar();
            }
            StartLap(race.raceTime);
        }

        void EnsureGhostCar()
        {
            if (ghostCar != null) return;
            ghostCar = new GameObject("GhostCar");
            ghostCar.transform.SetParent(race.transform, false);
            // a visual-only car: dummy controller provides wheel positions for the visual
            var rb = ghostCar.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            var cc = ghostCar.AddComponent<CarController>();
            cc.enabled = false;
            cc.ApplySetup(me.car.setup);
            ghostVisual = ghostCar.AddComponent<CarVisual>();
            ghostVisual.Build(cc, new Color(0.4f, 0.8f, 1f), Color.white, true);
        }

        void FixedUpdate()
        {
            if (race == null || lapStart < 0f) return;
            float t = race.raceTime - lapStart;
            float lapDist = me.totalDistance - Mathf.Floor(me.totalDistance / race.track.length) * race.track.length;
            int mk = Mathf.FloorToInt(lapDist / 10f);
            while (curMarks.Count <= mk && curMarks.Count < 2000) curMarks.Add(t);
            sampleTimer -= Time.fixedDeltaTime;
            if (sampleTimer <= 0f)
            {
                sampleTimer = 0.05f;
                current.Add(new Sample { t = t, p = me.car.transform.position, r = me.car.transform.rotation });
            }
            if (best != null && ghostCar != null)
            {
                bool show = t <= BestGhostTime;
                ghostCar.SetActive(show);
                if (!show) return;
                int i = 0;
                // binary search the sample bracket
                int lo = 0, hi = best.Count - 1;
                while (lo < hi - 1) { int mid = (lo + hi) / 2; if (best[mid].t <= t) lo = mid; else hi = mid; }
                i = lo;
                var a = best[i];
                var b = best[Mathf.Min(i + 1, best.Count - 1)];
                float f = b.t > a.t ? Mathf.Clamp01((t - a.t) / (b.t - a.t)) : 0f;
                ghostCar.transform.SetPositionAndRotation(Vector3.Lerp(a.p, b.p, f), Quaternion.Slerp(a.r, b.r, f));
            }
        }
    }
}
