using UnityEngine;

namespace F1Game
{
    /// <summary>
    /// AI racing driver. Follows the pre-computed racing line with pure-pursuit steering and the
    /// speed profile scaled by the difficulty "pace" setting; the "aggression" setting controls
    /// following distance, overtaking attempts, how much room it leaves and how late it brakes.
    /// It drives the same physics as the player (with assists).
    /// </summary>
    public class AIDriver : MonoBehaviour
    {
        RaceManager race;
        RaceCar me;
        CarController car;
        TrackData track;

        public float pace = 1f;          // multiplier on the speed profile
        public float aggression = 0.5f;  // 0..1
        public bool cooldown;            // after the chequered flag
        float lane;                      // lateral offset relative to the racing line (m)
        float laneTarget;
        int overtakeSide;
        float overtakeTimer;
        RaceCar overtakeTarget;
        float lapNoise = 1f;
        int noiseLap = -1;
        float startDelay;
        float smoothSteer;
        public static float StanleyGain = 0f;

        public void Init(RaceManager r, RaceCar rc, DriverDef def)
        {
            race = r;
            me = rc;
            car = rc.car;
            track = r.track;
            var s = GameSettings.Current;
            float difficulty = s.aiPace / 100f;
            float skill = Mathf.InverseLerp(0.95f, 1.0f, def.skill);
            float teamPerf = Mathf.InverseLerp(0.965f, 1.0f, GameData.Teams[def.team].performance);
            pace = Mathf.Lerp(0.84f, 1.045f, difficulty) * (0.975f + 0.015f * skill + 0.01f * teamPerf);
            aggression = Mathf.Clamp01(s.aiAggression / 100f * 0.75f + def.aggression * 0.35f);
            startDelay = Random.Range(0.12f, 0.45f) * (1.3f - skill * 0.5f);
        }

        /// <summary>After a teleport/pit release: start from where the car actually is and merge gradually.</summary>
        public void ResetLane(float lateral, float d)
        {
            lane = lateral - track.LineOffsetAt(d);
            smoothSteer = 0f;
            overtakeTimer = 0f;
            overtakeTarget = null;
        }

        float LaneLimit(float d, float lateralWanted)
        {
            float lim = track.halfWidth - 1.5f;
            float lineOff = track.LineOffsetAt(d);
            return Mathf.Clamp(lineOff + lateralWanted, -lim, lim) - lineOff;
        }

        void FixedUpdate()
        {
            if (race == null || car == null || car.IsKinematic) return;
            float dt = Time.fixedDeltaTime;
            if (car.frozen) { car.input = new CarController.Inputs { throttle = 0.45f, brake = 1f }; return; }
            if (race.state == RaceState.Running && race.raceTime < startDelay && race.IsRace && race.raceTime < 2f)
            {
                car.input = new CarController.Inputs { throttle = 0.5f, brake = 1f };
                return;
            }

            float v = car.Speed;
            float d = me.lapDistance;
            if (me.lapsCompleted != noiseLap)
            {
                noiseLap = me.lapsCompleted;
                lapNoise = 1f + Random.Range(-0.008f, 0.004f) * (1.2f - aggression * 0.4f);
            }

            float trafficLimit = float.MaxValue;
            laneTarget = 0f;
            Traffic(ref trafficLimit, v, d, dt);
            lane = Mathf.MoveTowards(lane, LaneLimit(d, laneTarget), Mathf.Lerp(2.6f, 1.6f, v / 80f) * dt);

            // ---------------- steering (pure pursuit) ----------------
            float look = Mathf.Clamp(7f + v * 0.42f, 9f, 42f);
            float dl = d + look;
            Vector3 target = track.LinePositionAt(dl) + track.RightAt(dl) * LaneLimit(dl, lane);
            Vector3 local = car.transform.InverseTransformPoint(target);
            float l2 = Mathf.Max(local.x * local.x + local.z * local.z, 1f);
            float curv = 2f * local.x / l2;
            float steerDeg = Mathf.Atan(curv * VehicleSpec.Wheelbase) * Mathf.Rad2Deg;
            // cross-track correction at the car's own position (Stanley-style) so it never drifts wide
            float wantHere = track.LineOffsetAt(d) + LaneLimit(d, lane);
            float cte = wantHere - me.lateral;
            float cteDead = Mathf.Sign(cte) * Mathf.Max(0f, Mathf.Abs(cte) - 0.6f);
            steerDeg += Mathf.Atan(StanleyGain * cteDead / (v + 4f)) * Mathf.Rad2Deg;
            // counter yaw when the rear steps out
            Vector3 lv = car.transform.InverseTransformDirection(car.rb.GetVelocity());
            float slipAngle = v > 5f ? Mathf.Atan2(lv.x, Mathf.Abs(lv.z)) * Mathf.Rad2Deg : 0f;
            steerDeg += slipAngle * 0.55f;
            float steer = Mathf.Clamp(steerDeg / Mathf.Max(car.MaxSteerNow, 1f), -1f, 1f);
            smoothSteer = Mathf.Lerp(smoothSteer, steer, 0.5f);

            // ---------------- speed ----------------
            float grip = Mathf.Sqrt(Tyres.Grip(car.compound) / Tyres.Grip(TyreCompound.Medium) * Tyres.WearGrip(car.MaxTyreWear));
            float massF = Mathf.Sqrt(850f / (VehicleSpec.DryMass + car.fuel));
            float brakeLate = 1f + aggression * 0.03f;
            float vt = track.SpeedAt(d + Mathf.Max(4f, v * 0.12f)) * pace * lapNoise * grip * Mathf.Lerp(1f, massF, 0.5f) * brakeLate;
            if (Mathf.Abs(lane) > 1.5f) vt *= 0.975f;
            vt = Mathf.Min(vt, trafficLimit);
            if (cooldown) vt = Mathf.Min(vt * 0.6f, 45f);

            float err = vt - v;
            float throttle = err > 0f ? Mathf.Clamp01(0.3f + err * 0.35f) : 0f;
            float brake = err < -0.6f ? Mathf.Clamp01(-err * 0.15f) : 0f;
            if (car.SlipAmount > 0.45f) throttle *= 0.65f;
            // catching a slide: ease off the pedals
            if (Mathf.Abs(slipAngle) > 3.5f) { float k = Mathf.Clamp01((Mathf.Abs(slipAngle) - 3.5f) / 6f); brake *= 1f - 0.7f * k; throttle *= 1f - 0.5f * k; }
            if (Mathf.Abs(slipAngle) > 12f) throttle = 0f;
            // wheels on the grass: no full throttle until back on the asphalt
            if (car.WheelsOffTrack > 1) throttle = Mathf.Min(throttle, 0.7f);
            if (car.OutOfFuel) { throttle = 0f; }

            car.input = new CarController.Inputs { steer = smoothSteer, throttle = throttle, brake = brake };
            car.drsOpen = car.drsAvailable && brake < 0.05f && Mathf.Abs(smoothSteer) < 0.35f;
        }

        void Traffic(ref float limit, float v, float d, float dt)
        {
            float L = track.length;
            if (overtakeTimer > 0f) overtakeTimer -= dt;
            float lim = track.halfWidth - 1.5f;
            float myLat = me.lateral;
            float lineHere = track.LineOffsetAt(d);

            foreach (var o in race.cars)
            {
                if (o == me || o.InPit || o.retired) continue;
                // physical gap along the track (handles lapped cars)
                float gap = Mathf.Repeat(o.totalDistance - me.totalDistance + L * 0.5f, L) - L * 0.5f;
                if (gap > 70f || gap < -15f) continue;
                float latDiff = o.lateral - myLat;
                float closing = v - o.car.Speed;

                if (gap > 0f)
                {
                    bool inPath = Mathf.Abs(latDiff) < 2.5f;
                    if (!inPath) continue;
                    float follow = 6f + v * 0.22f * (1.25f - aggression * 0.6f);
                    bool blueFlag = o.totalDistance + L * 0.5f < me.totalDistance; // backmarker ahead: we are lapping it
                    if (gap < follow + Mathf.Max(0f, closing) * 1.5f)
                    {
                        if (overtakeTimer <= 0f && gap < 35f && (closing > 0.3f || aggression > 0.55f || blueFlag))
                        {
                            float roomLeft = o.lateral - (-lim);
                            float roomRight = lim - o.lateral;
                            int side = roomRight > roomLeft ? 1 : -1;
                            // prefer the inside of the next corner
                            int k = track.IndexAt(d + 90f);
                            if (Mathf.Abs(track.curvature[k]) > 1f / 300f)
                            {
                                int inside = track.curvature[k] > 0f ? -1 : 1;
                                if ((inside > 0 ? roomRight : roomLeft) > 3.2f) side = inside;
                            }
                            if (Mathf.Max(roomLeft, roomRight) > 3.2f)
                            {
                                overtakeSide = side;
                                overtakeTarget = o;
                                overtakeTimer = 3.5f + aggression * 2f;
                            }
                        }
                        if (overtakeTimer <= 0f || overtakeTarget != o)
                        {
                            // stay behind: match speed and keep the gap
                            float want = o.car.Speed + (gap - follow) * 0.6f;
                            limit = Mathf.Min(limit, Mathf.Max(0f, want));
                        }
                        else if (gap < 6f && Mathf.Abs(latDiff) < 2.0f)
                        {
                            // not alongside yet but about to touch: brake a little
                            limit = Mathf.Min(limit, o.car.Speed + 1f);
                        }
                    }
                }
                else
                {
                    // car behind us or alongside
                    if (gap > -6f && Mathf.Abs(latDiff) < 2.6f)
                    {
                        float push = (2.7f - Mathf.Abs(latDiff)) * (1.1f - aggression * 0.6f);
                        laneTarget += -Mathf.Sign(latDiff == 0f ? 1f : latDiff) * push;
                    }
                    // let faster cars that are lapping us through
                    bool beingLapped = o.totalDistance > me.totalDistance + L * 0.5f;
                    if (beingLapped && gap > -45f)
                    {
                        laneTarget += (o.lateral > myLat ? -2.5f : 2.5f);
                        limit = Mathf.Min(limit, Mathf.Max(20f, v * 0.96f));
                    }
                }
            }

            if (overtakeTimer > 0f && overtakeTarget != null)
            {
                float gapT = Mathf.Repeat(overtakeTarget.totalDistance - me.totalDistance + L * 0.5f, L) - L * 0.5f;
                if (gapT < -7f) { overtakeTimer = 0f; overtakeTarget = null; }
                else
                {
                    float wantAbs = Mathf.Clamp(overtakeTarget.lateral + overtakeSide * 3.4f, -lim, lim);
                    laneTarget = wantAbs - lineHere;
                }
            }
        }
    }
}
