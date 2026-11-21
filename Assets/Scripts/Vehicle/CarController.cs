using System;
using UnityEngine;

namespace F1Game
{
    [Serializable]
    public struct DriverAssists
    {
        public bool tractionControl;
        public bool abs;
        public bool autoGearbox;
        public bool steeringAssist;
        public bool brakingAssist;

        public static DriverAssists Full => new DriverAssists { tractionControl = true, abs = true, autoGearbox = true, steeringAssist = true, brakingAssist = true };
        public static DriverAssists Default => new DriverAssists { tractionControl = true, abs = true, autoGearbox = true, steeringAssist = true, brakingAssist = false };
    }

    /// <summary>
    /// Raycast-suspension single-seater physics. Per wheel: spring/damper + anti-roll bar,
    /// load-sensitive tyres (simplified Pacejka with friction circle), compound + wear grip;
    /// aerodynamics from the wing setup (front/rear downforce, drag, DRS, wake), 8-speed gearbox,
    /// brake bias, fuel mass and consumption. Setup changes are applied instantly via ApplySetup().
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        public struct Inputs
        {
            public float steer, throttle, brake;
        }

        public class Wheel
        {
            public string name;
            public bool front, left;
            public Vector3 localAnchor;
            public float compression, prevCompression;
            public bool grounded;
            public Vector3 contactPoint, contactNormal;
            public float load;
            public float slipAngle;
            public float use;
            public bool sliding, locked, spinning;
            public float wear;
            public float spinAngle;
            public SurfaceType surface;
            public float surfaceGrip = 1f, surfaceDrag;
        }

        // ---------- configuration ----------
        public CarSetup setup = new CarSetup();
        public DriverAssists assists = DriverAssists.Default;
        public bool isPlayer;
        public TrackData track;
        public TyreCompound compound = TyreCompound.Medium;
        public float fuel = 60f;
        /// <summary>kg/s at full throttle and max rpm (calibrated by the race to the track length).</summary>
        public float fuelRate = 0.03f;
        /// <summary>Tyre wear per second at nominal intensity for the current compound.</summary>
        public float wearRate = 0.001f;
        public float wearMultiplier = 1f;
        public float fuelMultiplier = 1f;
        public float aiGripScale = 1f; // AI difficulty may slightly scale grip (kept at 1 by default)

        // ---------- runtime state ----------
        public Inputs input;
        public int gear = 1;
        public float rpm = VehicleSpec.IdleRpm;
        public float steerAngleDeg;
        public bool frozen;
        public float throttleApplied, brakeApplied;
        public float wakeDownforceFactor = 1f, slipstreamFactor = 1f;
        public bool drsAvailable, drsOpen;
        public float bottoming;
        public float ForwardSpeed { get; private set; }
        public float Speed { get; private set; }
        public float SpeedKmh => Speed * 3.6f;
        public float LateralG { get; private set; }
        public float LongitudinalG { get; private set; }
        public bool OutOfFuel => fuel <= 0.001f;
        public bool IsKinematic => rb != null && rb.isKinematic;
        public float kinematicSpeed;
        public int trackIndex = -1;
        public float upsideDownTime;
        public float MaxTyreWear { get { float m = 0; foreach (var w in wheels) m = Mathf.Max(m, w.wear); return m; } }
        public float AvgTyreWear { get { float m = 0; foreach (var w in wheels) m += w.wear; return m / 4f; } }
        public bool AnySliding { get { foreach (var w in wheels) if (w.sliding && w.grounded) return true; return false; } }
        public float SlipAmount { get; private set; }
        public int WheelsOffTrack { get; private set; }
        public float shiftTimer;
        /// <summary>Steering lock (deg) currently allowed (speed dependent).</summary>
        public float MaxSteerNow { get; private set; } = VehicleSpec.MaxSteerDeg;

        public Wheel[] wheels;
        public Rigidbody rb;

        public event Action<float, Collision> OnImpact;
        public event Action<int> OnGearChanged;

        struct TyreScratch
        {
            public bool active;
            public Vector3 wf, ws, pv;
            public float vLong, vLat, fMax, fy, avail, mShare;
        }
        readonly TyreScratch[] tyre = new TyreScratch[4];

        int pendingShift;
        float reverseTimer;
        float originHeight;
        Vector3 lastVelocity;
        static int groundMask = -1;

        public const int CarLayer = 2; // built-in "Ignore Raycast" layer: suspension rays skip cars

        public static int GroundMask
        {
            get
            {
                if (groundMask == -1) groundMask = Physics.DefaultRaycastLayers & ~(1 << CarLayer);
                return groundMask;
            }
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            BuildWheels();
        }

        void BuildWheels()
        {
            if (wheels != null) return;
            wheels = new Wheel[4];
            wheels[0] = new Wheel { name = "FL", front = true, left = true };
            wheels[1] = new Wheel { name = "FR", front = true, left = false };
            wheels[2] = new Wheel { name = "RL", front = false, left = true };
            wheels[3] = new Wheel { name = "RR", front = false, left = false };
        }

        /// <summary>Configures rigidbody + suspension geometry from the setup. Safe to call any time (garage "Save").</summary>
        public void ApplySetup(CarSetup s)
        {
            if (s != null) setup = s.Clone();
            setup.Clamp();
            if (rb == null) rb = GetComponent<Rigidbody>();
            BuildWheels();
            rb.mass = VehicleSpec.DryMass + fuel;
            rb.centerOfMass = new Vector3(0f, VehicleSpec.CogHeight, Mathf.Lerp(VehicleSpec.RearAxleZ, VehicleSpec.FrontAxleZ, VehicleSpec.FrontWeight));
            rb.inertiaTensor = new Vector3(1150f, 1250f, 320f) * (rb.mass / 850f);
            rb.inertiaTensorRotation = Quaternion.identity;

            originHeight = setup.PlankClearance - 0.036f;
            foreach (var w in wheels)
            {
                float k = w.front ? setup.FrontSpring : setup.RearSpring;
                float share = w.front ? VehicleSpec.FrontWeight : 1f - VehicleSpec.FrontWeight;
                float staticComp = rb.mass * 9.81f * share * 0.5f / k;
                float anchorY = VehicleSpec.SuspensionTravel + VehicleSpec.WheelRadius - staticComp - originHeight;
                w.localAnchor = new Vector3(w.left ? -VehicleSpec.HalfTrack : VehicleSpec.HalfTrack, anchorY,
                    w.front ? VehicleSpec.FrontAxleZ : VehicleSpec.RearAxleZ);
            }
        }

        public float StaticCompression(bool front)
        {
            float k = front ? setup.FrontSpring : setup.RearSpring;
            float share = front ? VehicleSpec.FrontWeight : 1f - VehicleSpec.FrontWeight;
            return (VehicleSpec.DryMass + fuel) * 9.81f * share * 0.5f / k;
        }

        /// <summary>Local-space wheel centre for visuals.</summary>
        public Vector3 WheelCenterLocal(int i)
        {
            var w = wheels[i];
            float comp = (rb != null && rb.isKinematic) ? StaticCompression(w.front) : w.compression;
            return w.localAnchor - Vector3.up * (VehicleSpec.SuspensionTravel - comp);
        }

        public void RequestShift(int dir) { pendingShift = dir; }

        public void FitNewTyres(TyreCompound c)
        {
            compound = c;
            foreach (var w in wheels) w.wear = 0f;
        }

        public void Teleport(Vector3 pos, Quaternion rot, float speed)
        {
            bool kin = rb.isKinematic;
            rb.isKinematic = false;
            rb.position = pos;
            rb.rotation = rot;
            transform.SetPositionAndRotation(pos, rot);
            rb.SetVelocity(rot * Vector3.forward * speed);
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = kin;
            foreach (var w in wheels) { w.compression = w.prevCompression = StaticCompression(w.front); }
            lastVelocity = rot * Vector3.forward * speed;
            upsideDownTime = 0f;
            steerAngleDeg = 0f;
        }

        public void SetKinematic(bool on)
        {
            if (on == rb.isKinematic) return;
            if (on)
            {
                kinematicSpeed = Mathf.Max(0f, ForwardSpeed);
                rb.SetVelocity(Vector3.zero);
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.interpolation = RigidbodyInterpolation.None;
            }
            else
            {
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.SetVelocity(transform.forward * kinematicSpeed);
                rb.angularVelocity = Vector3.zero;
                foreach (var w in wheels) w.compression = w.prevCompression = StaticCompression(w.front);
            }
        }

        // ------------------------------------------------------------------
        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (rb.isKinematic)
            {
                KinematicTick(dt);
                return;
            }

            float targetMass = VehicleSpec.DryMass + fuel;
            if (Mathf.Abs(rb.mass - targetMass) > 0.25f) rb.mass = targetMass;

            Transform t = transform;
            Vector3 vel = rb.GetVelocity();
            Vector3 fwd = t.forward, up = t.up, right = t.right;
            float vF = Vector3.Dot(vel, fwd);
            ForwardSpeed = vF;
            Speed = vel.magnitude;
            Vector3 accel = (vel - lastVelocity) / dt;
            lastVelocity = vel;
            LateralG = Mathf.Lerp(LateralG, Vector3.Dot(accel, right) / 9.81f, 0.1f);
            LongitudinalG = Mathf.Lerp(LongitudinalG, Vector3.Dot(accel, fwd) / 9.81f, 0.1f);

            upsideDownTime = Vector3.Dot(up, Vector3.up) < 0.3f ? upsideDownTime + dt : 0f;

            // ---- controls ----
            float thr = frozen ? 0f : Mathf.Clamp01(input.throttle);
            float brk = frozen ? 1f : Mathf.Clamp01(input.brake);
            UpdateGearbox(dt, vF, ref thr, ref brk);
            UpdateSteering(dt);
            if (shiftTimer > 0f) thr = 0f;
            if (OutOfFuel) thr = 0f;
            throttleApplied = thr;
            brakeApplied = brk;

            // ---- surface detection ----
            UpdateSurfaces();

            // ---- aerodynamics ----
            float vAir = Mathf.Max(0f, vF);
            float q = 0.5f * VehicleSpec.AirDensity * vAir * vAir;
            float ge = setup.GroundEffect * (1f + 0.04f * setup.Stiffness01);
            bool drs = drsOpen && drsAvailable;
            float frontDF = q * setup.FrontClA * ge * wakeDownforceFactor;
            float rearDF = q * setup.RearClA * (drs ? 0.70f : 1f) * ge;
            float cdA = Mathf.Max(0.5f, setup.CdA - (drs ? 0.17f : 0f));
            rb.AddForceAtPosition(-up * frontDF, t.TransformPoint(new Vector3(0f, 0.2f, VehicleSpec.FrontAxleZ)));
            rb.AddForceAtPosition(-up * rearDF, t.TransformPoint(new Vector3(0f, 0.3f, VehicleSpec.RearAxleZ)));
            if (Speed > 0.5f)
                rb.AddForceAtPosition(-vel.normalized * (0.5f * VehicleSpec.AirDensity * Speed * Speed * cdA * slipstreamFactor),
                    t.TransformPoint(new Vector3(0f, 0.45f, -0.2f)));

            // ---- suspension ----
            float travel = VehicleSpec.SuspensionTravel;
            float rayLen = travel + VehicleSpec.WheelRadius;
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                Vector3 anchor = t.TransformPoint(w.localAnchor);
                w.prevCompression = w.compression;
                if (Physics.Raycast(anchor, -up, out RaycastHit hit, rayLen + 0.1f, GroundMask, QueryTriggerInteraction.Ignore))
                {
                    w.grounded = hit.distance <= rayLen + 0.005f;
                    float comp = travel - (hit.distance - VehicleSpec.WheelRadius);
                    w.compression = Mathf.Clamp(comp, 0f, travel + VehicleSpec.WheelRadius * 0.5f);
                    w.contactPoint = hit.point;
                    w.contactNormal = hit.normal;
                }
                else
                {
                    w.grounded = false;
                    w.compression = 0f;
                }
                if (!w.grounded) { w.load = 0f; continue; }

                float k = w.front ? setup.FrontSpring : setup.RearSpring;
                float share = w.front ? VehicleSpec.FrontWeight : 1f - VehicleSpec.FrontWeight;
                float mCorner = rb.mass * share * 0.5f;
                float cCrit = 2f * Mathf.Sqrt(k * mCorner);
                float compVel = (w.compression - w.prevCompression) / dt;
                float damper = compVel > 0f ? 0.45f * cCrit : 0.70f * cCrit;
                float f = k * w.compression + damper * compVel;
                float stop = travel * 0.9f;
                if (w.compression > stop) f += k * 6f * (w.compression - stop);
                w.load = Mathf.Max(0f, f);
            }

            // anti-roll bars
            ApplyAntiRoll(wheels[0], wheels[1], setup.FrontAntiRoll);
            ApplyAntiRoll(wheels[2], wheels[3], setup.RearAntiRoll);

            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                if (!w.grounded) continue;
                rb.AddForceAtPosition(up * w.load, t.TransformPoint(w.localAnchor));
            }

            // plank / bottoming out
            bottoming = 0f;
            Plank(true, t, up, vel);
            Plank(false, t, up, vel);

            // ---- engine ----
            float driveForce = EngineTick(dt, vF, thr);

            // ---- tyres ----
            // pass 1: kinematics, grip and pure lateral force of every wheel
            float mech = 1f + 0.035f * (1f - setup.Stiffness01) * Mathf.Clamp01(1f - (Speed - 25f) / 50f);
            float bias = setup.BrakeFront01;
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                var k = tyre[i];
                w.sliding = w.locked = w.spinning = false;
                w.use = 0f;
                k.active = w.grounded;
                if (!w.grounded) { k.fMax = 0f; k.avail = 0f; tyre[i] = k; continue; }
                Vector3 n = w.contactNormal;
                Vector3 wf = w.front ? Quaternion.AngleAxis(steerAngleDeg, up) * fwd : fwd;
                wf = Vector3.ProjectOnPlane(wf, n).normalized;
                k.wf = wf;
                k.ws = Vector3.Cross(n, wf);
                k.pv = rb.GetPointVelocity(w.contactPoint);
                k.vLong = Vector3.Dot(k.pv, wf);
                k.vLat = Vector3.Dot(k.pv, k.ws);
                float mu = VehicleSpec.TyreMu * Tyres.Grip(compound) * Tyres.WearGrip(w.wear) * VehicleSpec.LoadSensitivity(w.load)
                           * w.surfaceGrip * mech * aiGripScale;
                k.fMax = mu * w.load;
                float alpha = Mathf.Atan2(k.vLat, Mathf.Max(Mathf.Abs(k.vLong), 2.0f));
                w.slipAngle = alpha;
                float fy = -k.fMax * VehicleSpec.TyreCurve(alpha);
                float share = w.front ? VehicleSpec.FrontWeight : 1f - VehicleSpec.FrontWeight;
                k.mShare = rb.mass * share * 0.5f;
                float fyCap = k.mShare * Mathf.Abs(k.vLat) / dt * 0.6f;
                k.fy = Mathf.Clamp(fy, -fyCap, fyCap);
                k.avail = Mathf.Sqrt(Mathf.Max(0f, k.fMax * k.fMax - k.fy * k.fy));
                tyre[i] = k;
            }

            // pass 2: longitudinal forces with traction control / ABS. The assists work "select-low"
            // per axle (like real ABS on the rear axle) so a wheel on the grass doesn't yaw the car.
            bool isAI = !isPlayer;
            bool tc = assists.tractionControl || isAI;
            bool abs = assists.abs || isAI;
            float slipSum = 0f;
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                var k = tyre[i];
                if (!k.active)
                {
                    w.spinAngle += vF / VehicleSpec.WheelRadius * Mathf.Rad2Deg * dt;
                    continue;
                }
                // Select-low only on a split-mu surface (one wheel on grass/gravel): otherwise each wheel
                // is controlled on its own so cornering load transfer doesn't rob braking/traction.
                var other = tyre[i ^ 1]; // same axle, other side
                bool split = other.active && Mathf.Abs(w.surfaceGrip - wheels[i ^ 1].surfaceGrip) > 0.05f;
                float axleAvail = split ? Mathf.Min(k.avail, other.avail) : k.avail;
                float axleFMax = split ? Mathf.Min(k.fMax, other.fMax) : k.fMax;
                float fMax = k.fMax, fy = k.fy, vLong = k.vLong;

                float drive = w.front ? 0f : driveForce * 0.5f;
                float brakeF = brk * VehicleSpec.MaxBrakeForce * (w.front ? bias : 1f - bias) * 0.5f;

                float fx = drive;
                bool braking = false;
                if (brakeF > 0f)
                {
                    if (Mathf.Abs(vLong) > 0.4f) { fx -= Mathf.Sign(vLong) * brakeF; braking = true; }
                    else fx += Mathf.Clamp(-vLong * k.mShare / dt, -brakeF, brakeF);
                }
                // Assists limit the TOTAL longitudinal force (brake + engine braking, or drive) to the grip
                // left over after cornering, so combined demands never overload the tyre.
                bool retarding = Mathf.Abs(vLong) > 0.4f && Mathf.Sign(fx) != Mathf.Sign(vLong);
                if ((retarding && abs) || (!retarding && tc && drive != 0f))
                {
                    float lim = Mathf.Max(axleAvail * 0.97f, axleFMax * (retarding ? 0.25f : 0.2f));
                    fx = Mathf.Clamp(fx, -lim, lim);
                }
                if (Mathf.Abs(vLong) > 0.1f)
                    fx -= Mathf.Sign(vLong) * (VehicleSpec.RollingResistance + w.surfaceDrag) * w.load;

                Vector3 n = w.contactNormal;
                Vector3 force;
                if (Mathf.Abs(fx) > fMax)
                {
                    if (braking && Mathf.Abs(vLong) > 1f)
                    {
                        // locked wheel: friction opposes the sliding velocity
                        w.locked = true;
                        Vector3 slide = Vector3.ProjectOnPlane(k.pv, n);
                        force = -slide.normalized * fMax * 0.85f;
                    }
                    else
                    {
                        w.spinning = drive != 0f;
                        fx = Mathf.Sign(fx) * fMax * 0.86f;
                        force = k.wf * fx + k.ws * fy * 0.4f;
                    }
                    w.sliding = true;
                    w.use = 1.2f;
                }
                else
                {
                    float fyMax = Mathf.Sqrt(fMax * fMax - fx * fx);
                    fy = Mathf.Clamp(fy, -fyMax, fyMax);
                    force = k.wf * fx + k.ws * fy;
                    w.use = fMax > 1f ? Mathf.Sqrt(fx * fx + fy * fy) / fMax : 0f;
                    w.sliding = Mathf.Abs(w.slipAngle) > VehicleSpec.PeakSlipAngle * 1.35f && Speed > 4f;
                }

                rb.AddForceAtPosition(force, w.contactPoint + up * 0.12f);

                // wheel spin (visual)
                float spinV = w.locked ? 0f : (w.spinning ? vLong + 8f * Mathf.Sign(drive) : vLong);
                w.spinAngle += spinV / VehicleSpec.WheelRadius * Mathf.Rad2Deg * dt;

                // tyre wear
                float intensity = (0.30f + 0.9f * w.use * w.use) * Mathf.Clamp(Speed / 55f, 0.1f, 1.6f)
                                  + (w.locked || w.spinning ? 2.5f : 0f) + (w.sliding ? 0.8f : 0f);
                if (w.surface == SurfaceType.Grass || w.surface == SurfaceType.Gravel) intensity += 0.3f;
                w.wear = Mathf.Min(1f, w.wear + wearRate * wearMultiplier * intensity * dt);
                slipSum += (w.sliding ? 1f : 0f) + (w.locked ? 1f : 0f);
            }
            SlipAmount = Mathf.Lerp(SlipAmount, Mathf.Clamp01(slipSum / 3f), 0.15f);

            // fuel
            float flow = fuelRate * (0.12f + 0.88f * thr * Mathf.Clamp01(rpm / VehicleSpec.MaxRpm));
            fuel = Mathf.Max(0f, fuel - flow * fuelMultiplier * dt);
        }

        void ApplyAntiRoll(Wheel l, Wheel r, float k)
        {
            if (!l.grounded || !r.grounded) return;
            float f = (l.compression - r.compression) * k;
            l.load = Mathf.Max(0f, l.load + f);
            r.load = Mathf.Max(0f, r.load - f);
        }

        void Plank(bool front, Transform t, Vector3 up, Vector3 vel)
        {
            var a = wheels[front ? 0 : 2];
            var b = wheels[front ? 1 : 3];
            if (!a.grounded && !b.grounded) return;
            float comp = (a.compression + b.compression) * 0.5f;
            float clearance = setup.PlankClearance - (comp - StaticCompression(front));
            if (clearance >= 0f) return;
            float pen = Mathf.Min(-clearance, 0.05f);
            float f = 350000f * pen;
            Vector3 pos = t.TransformPoint(new Vector3(0f, 0.05f, front ? VehicleSpec.FrontAxleZ * 0.5f : VehicleSpec.RearAxleZ * 0.5f));
            rb.AddForceAtPosition(up * f, pos);
            Vector3 hv = Vector3.ProjectOnPlane(vel, up);
            if (hv.sqrMagnitude > 1f) rb.AddForce(-hv.normalized * f * 0.35f);
            bottoming = Mathf.Max(bottoming, Mathf.Clamp01(pen / 0.01f));
        }

        void UpdateSurfaces()
        {
            WheelsOffTrack = 0;
            if (track == null)
            {
                foreach (var w in wheels) { w.surface = SurfaceType.Asphalt; w.surfaceGrip = 1f; w.surfaceDrag = 0f; }
                return;
            }
            trackIndex = track.FindIndex(transform.position, trackIndex);
            for (int i = 0; i < 4; i++)
            {
                var w = wheels[i];
                Vector3 p = w.grounded ? w.contactPoint : transform.TransformPoint(w.localAnchor);
                w.surface = track.SurfaceAt(trackIndex, p, out w.surfaceGrip, out w.surfaceDrag);
                if (w.surface == SurfaceType.Grass || w.surface == SurfaceType.Gravel) WheelsOffTrack++;
            }
        }

        void UpdateSteering(float dt)
        {
            float v = Speed;
            float maxSteer = VehicleSpec.MaxSteerDeg;
            if (assists.steeringAssist || !isPlayer)
            {
                float aMax = VehicleSpec.LateralGrip(setup, v, rb.mass, Tyres.Grip(compound));
                float limitRad = VehicleSpec.Wheelbase * aMax / Mathf.Max(v * v, 1f) + VehicleSpec.PeakSlipAngle * 1.15f;
                maxSteer = Mathf.Min(maxSteer, limitRad * Mathf.Rad2Deg);
            }
            else
            {
                maxSteer = Mathf.Lerp(VehicleSpec.MaxSteerDeg, 9f, v / 80f);
            }
            MaxSteerNow = maxSteer;
            float target = Mathf.Clamp(input.steer, -1f, 1f) * maxSteer;
            float rate;
            if (!isPlayer || GameInput.UsingAnalogSteering) rate = 160f;
            else rate = (Mathf.Abs(target) < Mathf.Abs(steerAngleDeg) || Mathf.Sign(target) != Mathf.Sign(steerAngleDeg)) ? 150f : 75f;
            steerAngleDeg = Mathf.MoveTowards(steerAngleDeg, target, rate * dt);
        }

        void UpdateGearbox(float dt, float vF, ref float thr, ref float brk)
        {
            if (shiftTimer > 0f) shiftTimer -= dt;
            float wheelRpm = Mathf.Abs(vF) / VehicleSpec.WheelRadius * 30f / Mathf.PI;
            bool auto = assists.autoGearbox || !isPlayer;

            if (pendingShift != 0)
            {
                if (pendingShift > 0)
                {
                    if (gear == -1) SetGear(1);
                    else if (gear < CarSetup.Gears) SetGear(gear + 1);
                }
                else
                {
                    if (gear > 1)
                    {
                        float after = wheelRpm * setup.GearRatio(gear - 1) * setup.finalDrive;
                        if (after < VehicleSpec.LimiterRpm) SetGear(gear - 1);
                    }
                    else if (gear == 1 && Mathf.Abs(vF) < 2f) SetGear(-1);
                }
                pendingShift = 0;
            }

            if (auto && !frozen)
            {
                if (gear >= 1)
                {
                    float cur = wheelRpm * setup.GearRatio(gear) * setup.finalDrive;
                    if (shiftTimer <= 0f)
                    {
                        if (cur > VehicleSpec.UpshiftRpm && gear < CarSetup.Gears && vF > 0f) SetGear(gear + 1);
                        else if (gear > 1)
                        {
                            float lower = wheelRpm * setup.GearRatio(gear - 1) * setup.finalDrive;
                            if (cur < 8300f && lower < 10900f) SetGear(gear - 1);
                        }
                    }
                    if (isPlayer && brk > 0.5f && thr < 0.1f && vF < 0.3f && vF > -0.5f) reverseTimer += dt;
                    else reverseTimer = 0f;
                    if (reverseTimer > 0.6f) { SetGear(-1); reverseTimer = 0f; }
                }
                else if (gear == -1)
                {
                    if (thr > 0.3f && vF > -1.0f) SetGear(1);
                }
            }

            // auto box in reverse: brake pedal drives backwards, throttle brakes
            if (gear == -1 && auto)
            {
                float tmp = thr; thr = brk; brk = tmp;
            }
        }

        void SetGear(int g)
        {
            if (g == gear) return;
            gear = g;
            shiftTimer = g > 0 ? 0.045f : 0.25f;
            OnGearChanged?.Invoke(g);
        }

        /// <summary>Updates rpm and returns the total tractive force at the rear wheels.</summary>
        float EngineTick(float dt, float vF, float thr)
        {
            float ratio = setup.GearRatio(gear) * setup.finalDrive;
            float wheelOmega = vF / VehicleSpec.WheelRadius;
            float fromWheels = Mathf.Abs(wheelOmega * ratio) * 30f / Mathf.PI;
            float targetRpm;
            if (frozen)
            {
                targetRpm = VehicleSpec.IdleRpm + thr * 0f + Mathf.Clamp01(input.throttle) * (10500f - VehicleSpec.IdleRpm);
            }
            else
            {
                // clutch slip near standstill lets the engine sit at launch revs
                float launch = VehicleSpec.IdleRpm + thr * (8500f - VehicleSpec.IdleRpm);
                targetRpm = Mathf.Abs(gear) == 1 ? Mathf.Max(fromWheels, launch) : Mathf.Max(fromWheels, VehicleSpec.IdleRpm);
                bool spinning = wheels[2].spinning || wheels[3].spinning;
                if (spinning) targetRpm = Mathf.Min(VehicleSpec.LimiterRpm, targetRpm + 1800f * thr);
            }
            rpm = Mathf.Lerp(rpm, Mathf.Min(targetRpm, VehicleSpec.LimiterRpm + 100f), 1f - Mathf.Exp(-dt * 25f));
            if (frozen || OutOfFuel) return 0f;

            float torque = 0f;
            if (fromWheels < VehicleSpec.LimiterRpm)
                torque = VehicleSpec.EngineTorque(rpm) * thr;
            if (thr < 0.05f && gear != 0 && Mathf.Abs(vF) > 1f)
            {
                float eb = 40f + 50f * (rpm / VehicleSpec.MaxRpm);
                return -Mathf.Sign(vF) * eb * Mathf.Abs(ratio) / VehicleSpec.WheelRadius;
            }
            return torque * ratio * VehicleSpec.DrivelineEfficiency / VehicleSpec.WheelRadius;
        }

        void KinematicTick(float dt)
        {
            ForwardSpeed = kinematicSpeed;
            Speed = Mathf.Abs(kinematicSpeed);
            LateralG = LongitudinalG = 0f;
            SlipAmount = 0f;
            if (gear < 1) gear = 1;
            // pick a sensible gear for the pit-lane speed so the engine note is right
            while (gear < CarSetup.Gears && setup.SpeedAtRpm(gear, 10500f) < Speed) gear++;
            while (gear > 1 && setup.SpeedAtRpm(gear - 1, 9000f) > Speed) gear--;
            float r = Speed / VehicleSpec.WheelRadius * setup.GearRatio(gear) * setup.finalDrive * 30f / Mathf.PI;
            rpm = Mathf.Lerp(rpm, Mathf.Max(VehicleSpec.IdleRpm, r), 0.2f);
            throttleApplied = Speed > 1f ? 0.3f : 0f;
            brakeApplied = 0f;
            steerAngleDeg = Mathf.MoveTowards(steerAngleDeg, 0f, 60f * dt);
            foreach (var w in wheels)
            {
                w.compression = StaticCompression(w.front);
                w.grounded = true;
                w.sliding = w.locked = w.spinning = false;
                w.spinAngle += Speed / VehicleSpec.WheelRadius * Mathf.Rad2Deg * dt;
            }
            fuel = Mathf.Max(0f, fuel - fuelRate * 0.1f * fuelMultiplier * dt);
        }

        void OnCollisionEnter(Collision c)
        {
            float v = c.relativeVelocity.magnitude;
            if (v > 2f) OnImpact?.Invoke(v, c);
        }
    }
}
