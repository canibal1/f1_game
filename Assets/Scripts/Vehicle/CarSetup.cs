using System;
using UnityEngine;

namespace F1Game
{
    /// <summary>
    /// Garage setup. All values are what the player sees on the sliders; the derived physical
    /// quantities are computed by the helper methods so the garage UI, the AI and the physics
    /// all agree on what a setup does.
    /// </summary>
    [Serializable]
    public class CarSetup
    {
        public float frontWing = 6f;       // 1..11
        public float rearWing = 6f;        // 1..11
        public float frontSuspension = 6f; // 1..11 (soft..stiff)
        public float rearSuspension = 6f;  // 1..11
        public float rideHeight = 5f;      // 1..11 (low..high)
        public float brakeBias = 57f;      // % front
        public float finalDrive = 3.10f;   // 2.70..3.60
        public float firstGear = 5.60f;    // 4.60..6.40
        public float topGear = 1.62f;      // 1.30..1.95

        public const float WingMin = 1, WingMax = 11;
        public const float SuspMin = 1, SuspMax = 11;
        public const float RideMin = 1, RideMax = 11;
        public const float BiasMin = 50, BiasMax = 70;
        public const float FinalMin = 2.70f, FinalMax = 3.60f;
        public const float FirstMin = 4.60f, FirstMax = 6.40f;
        public const float TopMin = 1.30f, TopMax = 1.95f;
        public const int Gears = 8;

        public CarSetup Clone() => (CarSetup)MemberwiseClone();

        public void CopyFrom(CarSetup o)
        {
            frontWing = o.frontWing; rearWing = o.rearWing; frontSuspension = o.frontSuspension;
            rearSuspension = o.rearSuspension; rideHeight = o.rideHeight; brakeBias = o.brakeBias;
            finalDrive = o.finalDrive; firstGear = o.firstGear; topGear = o.topGear;
        }

        public void Clamp()
        {
            frontWing = Mathf.Clamp(Mathf.Round(frontWing), WingMin, WingMax);
            rearWing = Mathf.Clamp(Mathf.Round(rearWing), WingMin, WingMax);
            frontSuspension = Mathf.Clamp(Mathf.Round(frontSuspension), SuspMin, SuspMax);
            rearSuspension = Mathf.Clamp(Mathf.Round(rearSuspension), SuspMin, SuspMax);
            rideHeight = Mathf.Clamp(Mathf.Round(rideHeight), RideMin, RideMax);
            brakeBias = Mathf.Clamp(Mathf.Round(brakeBias * 2f) / 2f, BiasMin, BiasMax);
            finalDrive = Mathf.Clamp(Mathf.Round(finalDrive * 100f) / 100f, FinalMin, FinalMax);
            firstGear = Mathf.Clamp(Mathf.Round(firstGear * 100f) / 100f, FirstMin, FirstMax);
            topGear = Mathf.Clamp(Mathf.Round(topGear * 100f) / 100f, TopMin, TopMax);
            if (topGear > firstGear * 0.5f) topGear = firstGear * 0.5f;
        }

        public static CarSetup Preset(CarSetupPreset p)
        {
            var s = new CarSetup();
            switch (p)
            {
                case CarSetupPreset.LowDownforce:
                    s.frontWing = 3; s.rearWing = 2; s.frontSuspension = 7; s.rearSuspension = 6; s.rideHeight = 4;
                    s.brakeBias = 58; s.finalDrive = 2.95f; s.firstGear = 5.50f; s.topGear = 1.55f;
                    break;
                case CarSetupPreset.HighDownforce:
                    s.frontWing = 10; s.rearWing = 10; s.frontSuspension = 6; s.rearSuspension = 5; s.rideHeight = 7;
                    s.brakeBias = 56; s.finalDrive = 3.35f; s.firstGear = 5.80f; s.topGear = 1.72f;
                    break;
            }
            return s;
        }

        static float N(float v, float min, float max) => (v - min) / (max - min);

        // ---------------- aerodynamics ----------------
        /// <summary>Front lift coefficient * area (m²).</summary>
        public float FrontClA => 0.95f + 0.11f * (frontWing - 1f);
        /// <summary>Rear lift coefficient * area (m²).</summary>
        public float RearClA => 1.15f + 0.13f * (rearWing - 1f);
        /// <summary>Ground-effect multiplier: low ride height = more downforce.</summary>
        public float GroundEffect => 1.10f - 0.02f * (rideHeight - 1f);
        /// <summary>Drag coefficient * area (m²).</summary>
        public float CdA => 0.75f + 0.012f * (frontWing - 1f) + 0.045f * (rearWing - 1f);
        /// <summary>Share of downforce on the front axle.</summary>
        public float AeroBalance => FrontClA / (FrontClA + RearClA);

        // ---------------- suspension ----------------
        public float FrontSpring => Mathf.Lerp(95000f, 200000f, N(frontSuspension, SuspMin, SuspMax));
        public float RearSpring => Mathf.Lerp(95000f, 200000f, N(rearSuspension, SuspMin, SuspMax));
        public float FrontAntiRoll => Mathf.Lerp(25000f, 110000f, N(frontSuspension, SuspMin, SuspMax));
        public float RearAntiRoll => Mathf.Lerp(25000f, 110000f, N(rearSuspension, SuspMin, SuspMax));
        /// <summary>0 = soft, 1 = stiff (average of both axles).</summary>
        public float Stiffness01 => (N(frontSuspension, SuspMin, SuspMax) + N(rearSuspension, SuspMin, SuspMax)) * 0.5f;
        /// <summary>Static plank clearance in metres.</summary>
        public float PlankClearance => 0.020f + 0.004f * (rideHeight - 1f);

        // ---------------- brakes ----------------
        public float BrakeFront01 => brakeBias / 100f;

        // ---------------- gearbox ----------------
        public float GearRatio(int gear)
        {
            if (gear <= 0) return gear < 0 ? -firstGear * 1.1f : 0f;
            gear = Mathf.Clamp(gear, 1, Gears);
            // geometric progression between first and top gear
            float t = (gear - 1) / (float)(Gears - 1);
            return firstGear * Mathf.Pow(topGear / firstGear, Mathf.Pow(t, 0.92f));
        }

        /// <summary>Road speed (m/s) at the given engine rpm in the given gear.</summary>
        public float SpeedAtRpm(int gear, float rpm)
        {
            float overall = GearRatio(gear) * finalDrive;
            if (Mathf.Abs(overall) < 0.001f) return 0f;
            return rpm * Mathf.PI / 30f / overall * VehicleSpec.WheelRadius;
        }

        public string Summary()
        {
            return $"Ön Kanat {frontWing:0} | Arka Kanat {rearWing:0} | Süsp. {frontSuspension:0}/{rearSuspension:0} | Yükseklik {rideHeight:0} | Fren {brakeBias:0.#}% | Son Oran {finalDrive:0.00}";
        }
    }
}
