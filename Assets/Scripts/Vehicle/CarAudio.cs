using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    /// <summary>
    /// Engine (synthesised V6 loop pitched by rpm, on/off-throttle layers), tyre squeal,
    /// wind and impact sounds. Only the nearest AI cars play their engine to keep the mix clean.
    /// </summary>
    public class CarAudio : MonoBehaviour
    {
        public CarController car;
        public bool isPlayer;
        AudioSource on, off, squeal, wind, oneShot;
        static readonly List<CarAudio> all = new List<CarAudio>();
        static float rankTimer;
        bool audible = true;
        static AudioClip impactClip;

        public void Init(CarController c, bool player)
        {
            car = c;
            isPlayer = player;
            on = MakeSource(SynthAudio.EngineOn, true);
            off = MakeSource(SynthAudio.EngineOff, true);
            squeal = MakeSource(SynthAudio.TyreSqueal, true);
            if (player) wind = MakeSource(SynthAudio.Wind, true);
            oneShot = MakeSource(null, false);
            if (impactClip == null) impactClip = Resources.Load<AudioClip>("FreeAssets/Audio/impact");
            car.OnImpact += HandleImpact;
            all.Add(this);
        }

        void OnDestroy()
        {
            all.Remove(this);
            if (car != null) car.OnImpact -= HandleImpact;
        }

        AudioSource MakeSource(AudioClip clip, bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = isPlayer ? 0.35f : 1f;
            s.dopplerLevel = isPlayer ? 0f : 0.7f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = isPlayer ? 5f : 7f;
            s.maxDistance = 400f;
            s.volume = 0f;
            if (clip != null && loop)
            {
                s.time = Random.Range(0f, clip.length * 0.9f);
                s.Play();
            }
            return s;
        }

        void HandleImpact(float speed, Collision c)
        {
            if (impactClip == null || oneShot == null) return;
            float v = Mathf.Clamp01(speed / 25f) * GameSettings.SfxVolume;
            oneShot.PlayOneShot(impactClip, v);
        }

        static void RankAudible()
        {
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 p = cam.transform.position;
            all.Sort((a, b) =>
            {
                if (a.isPlayer != b.isPlayer) return a.isPlayer ? -1 : 1;
                return (a.transform.position - p).sqrMagnitude.CompareTo((b.transform.position - p).sqrMagnitude);
            });
            for (int i = 0; i < all.Count; i++) all[i].audible = i < 7;
        }

        void Update()
        {
            if (car == null) return;
            rankTimer -= Time.unscaledDeltaTime;
            if (rankTimer <= 0f) { rankTimer = 0.4f; RankAudible(); }

            bool paused = Time.timeScale == 0f;
            float master = paused ? 0f : GameSettings.SfxVolume;
            float firing = car.rpm / 60f * 3f; // V6: three firing pulses per revolution
            float pitch = Mathf.Clamp(firing / SynthAudio.EngineBaseHz, 0.5f, 2.95f);
            float thr = car.throttleApplied;
            float baseVol = (isPlayer ? 0.42f : 0.85f) * (audible ? 1f : 0f) * master;
            if (car.frozen) thr = Mathf.Clamp01(car.input.throttle);
            on.pitch = pitch;
            off.pitch = pitch * 0.98f;
            on.volume = Mathf.Lerp(on.volume, baseVol * (0.25f + 0.75f * thr), 0.3f);
            off.volume = Mathf.Lerp(off.volume, baseVol * 0.55f * (1f - thr), 0.3f);

            float slip = car.SlipAmount;
            squeal.volume = Mathf.Lerp(squeal.volume, (audible ? 1f : 0f) * master * (isPlayer ? 0.35f : 0.25f) * Mathf.Clamp01(slip * 1.5f) * Mathf.Clamp01(car.Speed / 10f), 0.2f);
            squeal.pitch = 0.9f + Mathf.Clamp01(car.Speed / 60f) * 0.25f;

            if (wind != null)
            {
                wind.volume = master * 0.3f * Mathf.Clamp01(car.Speed / 90f);
                wind.pitch = 0.8f + Mathf.Clamp01(car.Speed / 90f) * 0.6f;
            }
        }
    }
}
