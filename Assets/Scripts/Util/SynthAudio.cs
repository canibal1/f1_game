using UnityEngine;

namespace F1Game
{
    /// <summary>
    /// Procedurally synthesised sound loops (engine, tyre squeal, wind, beeps).
    /// All loops contain an integer number of waveform cycles so they repeat seamlessly.
    /// </summary>
    public static class SynthAudio
    {
        public const int Rate = 44100;
        /// <summary>Fundamental of the engine loop. pitch = firingFrequency / EngineBaseHz.</summary>
        public const float EngineBaseHz = 220.5f; // 200 samples per cycle

        static AudioClip engineOn, engineOff, squeal, wind, beepLow, beepHigh, pitBeep;

        static float SoftClip(float x) => (float)System.Math.Tanh(x);

        static AudioClip Build(string name, int samples, System.Func<int, float> f)
        {
            var data = new float[samples];
            float peak = 0.0001f;
            for (int i = 0; i < samples; i++) { data[i] = f(i); peak = Mathf.Max(peak, Mathf.Abs(data[i])); }
            for (int i = 0; i < samples; i++) data[i] = data[i] / peak * 0.9f;
            var clip = AudioClip.Create(name, samples, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public static AudioClip EngineOn
        {
            get
            {
                if (engineOn == null)
                {
                    var rnd = new System.Random(1);
                    float[] phaseJit = new float[24];
                    for (int h = 0; h < phaseJit.Length; h++) phaseJit[h] = (float)rnd.NextDouble() * 6.28f;
                    engineOn = Build("EngineOn", Rate, i =>
                    {
                        double t = i / (double)Rate;
                        float s = 0f;
                        // sub-harmonic rumble (half firing frequency)
                        s += 0.55f * Mathf.Sin((float)(2 * System.Math.PI * EngineBaseHz * 0.5 * t));
                        for (int h = 1; h <= 18; h++)
                        {
                            float amp = 1f / Mathf.Pow(h, 0.85f);
                            if (h % 2 == 1) amp *= 1.25f;
                            s += amp * Mathf.Sin((float)(2 * System.Math.PI * EngineBaseHz * h * t) + phaseJit[h]);
                        }
                        return SoftClip(s * 0.55f);
                    });
                }
                return engineOn;
            }
        }

        public static AudioClip EngineOff
        {
            get
            {
                if (engineOff == null)
                {
                    engineOff = Build("EngineOff", Rate, i =>
                    {
                        double t = i / (double)Rate;
                        float s = 0.8f * Mathf.Sin((float)(2 * System.Math.PI * EngineBaseHz * 0.5 * t));
                        for (int h = 1; h <= 6; h++)
                            s += (0.9f / (h * h)) * Mathf.Sin((float)(2 * System.Math.PI * EngineBaseHz * h * t) + h);
                        // pops/crackle on overrun
                        return SoftClip(s * 0.7f);
                    });
                }
                return engineOff;
            }
        }

        public static AudioClip TyreSqueal
        {
            get
            {
                if (squeal == null)
                {
                    var rnd = new System.Random(7);
                    float lp = 0f, hp = 0f, prev = 0f;
                    squeal = Build("Squeal", Rate, i =>
                    {
                        double t = i / (double)Rate;
                        float n = (float)rnd.NextDouble() * 2f - 1f;
                        lp += (n - lp) * 0.35f;      // low-pass
                        hp = 0.9f * (hp + lp - prev); // high-pass
                        prev = lp;
                        float tone = Mathf.Sin((float)(2 * System.Math.PI * 1100 * t)) * (0.5f + 0.5f * Mathf.Sin((float)(2 * System.Math.PI * 7 * t)));
                        return hp * 0.8f + tone * 0.35f;
                    });
                }
                return squeal;
            }
        }

        public static AudioClip Wind
        {
            get
            {
                if (wind == null)
                {
                    var rnd = new System.Random(3);
                    float lp = 0f, lp2 = 0f;
                    wind = Build("Wind", Rate, i =>
                    {
                        float n = (float)rnd.NextDouble() * 2f - 1f;
                        lp += (n - lp) * 0.05f;
                        lp2 += (lp - lp2) * 0.1f;
                        return lp2;
                    });
                }
                return wind;
            }
        }

        static AudioClip Beep(string name, float hz, float seconds)
        {
            int n = (int)(Rate * seconds);
            return Build(name, n, i =>
            {
                float t = i / (float)Rate;
                float env = Mathf.Min(1f, t * 200f) * Mathf.Clamp01((seconds - t) * 30f);
                return Mathf.Sin(2 * Mathf.PI * hz * t) * env + 0.3f * Mathf.Sin(4 * Mathf.PI * hz * t) * env;
            });
        }

        public static AudioClip BeepLow => beepLow != null ? beepLow : (beepLow = Beep("BeepLow", 660f, 0.25f));
        public static AudioClip BeepHigh => beepHigh != null ? beepHigh : (beepHigh = Beep("BeepHigh", 1320f, 0.6f));
        public static AudioClip PitBeep => pitBeep != null ? pitBeep : (pitBeep = Beep("PitBeep", 990f, 0.12f));
    }
}
