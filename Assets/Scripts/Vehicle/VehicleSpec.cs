using UnityEngine;

namespace F1Game
{
    /// <summary>
    /// Fixed physical properties of the single-seater and shared physics formulas
    /// (used by the car, the AI speed planner and the garage read-outs).
    /// </summary>
    public static class VehicleSpec
    {
        // --- body ---
        public const float DryMass = 800f;          // kg incl. driver
        public const float FuelTankKg = 110f;
        public const float FrontWeight = 0.46f;     // static weight on front axle
        public const float CogHeight = 0.30f;

        // --- geometry (matches the scaled Kenney model, see CarVisual) ---
        public const float WheelRadius = 0.37f;
        public const float FrontAxleZ = 1.344f;
        public const float RearAxleZ = -1.848f;
        public const float HalfTrack = 0.75f;
        public const float Wheelbase = FrontAxleZ - RearAxleZ;
        public const float SuspensionTravel = 0.12f;

        // --- engine ---
        public const float IdleRpm = 4000f;
        public const float MaxRpm = 12500f;          // shift light / redline
        public const float LimiterRpm = 12700f;
        public const float MaxTorque = 560f;         // N·m at the crank
        public const float DrivelineEfficiency = 0.92f;
        public const float UpshiftRpm = 12150f;

        // --- brakes / tyres ---
        public const float MaxBrakeForce = 36000f;   // N, whole car
        public const float TyreMu = 1.78f;           // peak friction of a fresh soft tyre
        public const float TyreRefLoad = 3000f;      // N, load sensitivity reference
        public const float PeakSlipAngle = 0.10f;    // rad (~5.7°)
        public const float MaxSteerDeg = 20f;

        public const float AirDensity = 1.225f;
        public const float RollingResistance = 0.012f;

        /// <summary>Normalised torque curve, 1.0 at the peak (~10 500 rpm).</summary>
        public static float TorqueCurve(float rpm)
        {
            rpm = Mathf.Clamp(rpm, 1000f, LimiterRpm);
            if (rpm < 10500f)
            {
                float t = (rpm - 1000f) / 9500f;
                return Mathf.Lerp(0.42f, 1.0f, 1f - (1f - t) * (1f - t));
            }
            float u = (rpm - 10500f) / (LimiterRpm - 10500f);
            return Mathf.Lerp(1.0f, 0.84f, u * u);
        }

        public static float EngineTorque(float rpm) => MaxTorque * TorqueCurve(rpm);

        public static float EnginePowerKW(float rpm) => EngineTorque(rpm) * rpm * Mathf.PI / 30f / 1000f;

        /// <summary>Total downforce (N) at speed v for a setup.</summary>
        public static float Downforce(CarSetup s, float v)
        {
            float q = 0.5f * AirDensity * v * v;
            return q * (s.FrontClA + s.RearClA) * s.GroundEffect * (1f + 0.04f * s.Stiffness01);
        }

        public static float Drag(CarSetup s, float v)
        {
            return 0.5f * AirDensity * v * v * s.CdA;
        }

        /// <summary>Best tractive force over all gears at speed v (N, at the wheels, full throttle).</summary>
        public static float MaxTractiveForce(CarSetup s, float v, out int bestGear)
        {
            float best = 0f; bestGear = 1;
            for (int g = 1; g <= CarSetup.Gears; g++)
            {
                float overall = s.GearRatio(g) * s.finalDrive;
                float rpm = v / WheelRadius * overall * 30f / Mathf.PI;
                if (rpm > LimiterRpm) continue;
                rpm = Mathf.Max(rpm, 6000f); // clutch slip at very low speed
                float f = EngineTorque(rpm) * overall * DrivelineEfficiency / WheelRadius;
                if (f > best) { best = f; bestGear = g; }
            }
            return best;
        }

        /// <summary>Estimated top speed (m/s): drag/gearing limited.</summary>
        public static float EstimateTopSpeed(CarSetup s, float massKg)
        {
            float v = 10f;
            for (; v < 125f; v += 0.25f)
            {
                float f = MaxTractiveForce(s, v, out _);
                float resist = Drag(s, v) + RollingResistance * (massKg * 9.81f + Downforce(s, v));
                if (f <= resist) break;
            }
            return v;
        }

        /// <summary>Peak cornering acceleration (m/s²) available at speed v with a given tyre grip multiplier.</summary>
        public static float LateralGrip(CarSetup s, float v, float massKg, float gripMul)
        {
            float load = massKg * 9.81f + Downforce(s, v);
            float perWheel = load / 4f;
            float mu = TyreMu * gripMul * LoadSensitivity(perWheel);
            return mu * load / massKg;
        }

        public static float LoadSensitivity(float fz)
        {
            return Mathf.Clamp(1f - 0.10f * (fz / TyreRefLoad - 1f), 0.70f, 1.12f);
        }

        /// <summary>Simplified Pacejka curve (C = 1.35: progressive, forgiving fall-off past the peak). x = slip angle in rad. Returns -1..1.</summary>
        public static float TyreCurve(float slip)
        {
            const float C = 1.35f;
            float B = Mathf.Tan(Mathf.PI / (2f * C)) / PeakSlipAngle;
            return Mathf.Sin(C * Mathf.Atan(B * slip));
        }
    }
}
