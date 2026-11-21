using UnityEngine;

namespace F1Game
{
    public enum TyreCompound { Soft = 0, Medium = 1, Hard = 2 }

    public static class Tyres
    {
        /// <summary>Peak grip multiplier of each compound.</summary>
        public static float Grip(TyreCompound c)
        {
            switch (c)
            {
                case TyreCompound.Soft: return 1.000f;
                case TyreCompound.Medium: return 0.978f;
                default: return 0.956f;
            }
        }

        /// <summary>
        /// Expected life as a fraction of the race distance (before the 70 % "critical" mark).
        /// Race-length scaled so that strategy matters in short races too.
        /// </summary>
        public static float LifeFraction(TyreCompound c)
        {
            switch (c)
            {
                case TyreCompound.Soft: return 0.38f;
                case TyreCompound.Medium: return 0.58f;
                default: return 0.85f;
            }
        }

        public static string Name(TyreCompound c)
        {
            switch (c)
            {
                case TyreCompound.Soft: return "Yumuşak";
                case TyreCompound.Medium: return "Orta";
                default: return "Sert";
            }
        }

        public static string Letter(TyreCompound c)
        {
            switch (c)
            {
                case TyreCompound.Soft: return "S";
                case TyreCompound.Medium: return "M";
                default: return "H";
            }
        }

        public static Color Color(TyreCompound c)
        {
            switch (c)
            {
                case TyreCompound.Soft: return new Color(0.92f, 0.16f, 0.16f);
                case TyreCompound.Medium: return new Color(1f, 0.82f, 0.1f);
                default: return new Color(0.95f, 0.95f, 0.95f);
            }
        }

        /// <summary>Wear (0..1) -> grip multiplier. Gentle drop, then a "cliff" beyond 80 %.</summary>
        public static float WearGrip(float wear)
        {
            if (wear < 0.6f) return 1f - 0.06f * (wear / 0.6f);
            if (wear < 0.8f) return 0.94f - 0.10f * ((wear - 0.6f) / 0.2f);
            return Mathf.Max(0.5f, 0.84f - 0.30f * ((wear - 0.8f) / 0.2f));
        }

        public const float CriticalWear = 0.70f;
    }
}
