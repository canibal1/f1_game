using System;
using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    public enum SessionType { Race, TimeTrial }
    public enum RaceState { Countdown, Running, Finished }

    public class RaceConfig
    {
        public SessionType type = SessionType.Race;
        public int trackIndex;
        public int laps = 5;
        public List<int> seats = new List<int>();   // AI + player seats taking part, grid order
        public int playerSeat;
        public string playerName = "Oyuncu";
        public CarSetup playerSetup = new CarSetup();
        public TyreCompound playerCompound = TyreCompound.Medium;
        public float playerFuel = 60f;
        public bool championship;
        public int round;
    }

    /// <summary>
    /// Runs a session: spawns the cars, start lights, lap/sector timing, positions and gaps,
    /// slipstream/dirty air, DRS, the pit-stop state machine, tyre/fuel warnings, recovery and results.
    /// </summary>
    public class RaceManager : MonoBehaviour
    {
        public static RaceManager Instance { get; private set; }

        public RaceConfig config;
        public TrackData track;
        public TrackWorld world;
        public readonly List<RaceCar> cars = new List<RaceCar>();
        public RaceCar playerCar;
        public RaceState state = RaceState.Countdown;
        public float raceTime;
        public bool checkered;
        public float countdown;
        public int lightsOn;
        public List<ResultRow> results;
        public float fastestLap = float.MaxValue;
        public RaceCar fastestLapCar;
        public float bestSessionLap = float.MaxValue; // time trial personal best
        public float[] bestSectors = { float.MaxValue, float.MaxValue, float.MaxValue };
        public readonly List<string> warnings = new List<string>();
        public string eventMessage = "";
        public float eventTimer;
        public float estimatedLapTime;
        public float fuelRateFull;
        public CameraRig cameraRig;
        public GhostRecorder ghost;
        public event Action OnRaceFinished;

        float lightsOutDelay;
        float warningBeepTimer;
        AudioSource uiAudio;
        const float MarkerSpacing = 20f;
        float finishedTimer = -1f;

        public bool IsRace => config.type == SessionType.Race;
        public int TotalLaps => config.laps;

        // ------------------------------------------------------------------ setup
        public void Begin(RaceConfig cfg, CameraRig rig)
        {
            Instance = this;
            config = cfg;
            cameraRig = rig;
            var def = GameData.Tracks[cfg.trackIndex];
            track = TrackData.Build(def);
            world = TrackBuilder.Build(track, transform);
            uiAudio = gameObject.AddComponent<AudioSource>();
            uiAudio.spatialBlend = 0f;

            estimatedLapTime = track.estimatedLapTime * 1.08f;
            var s = GameSettings.Current;
            int n = Mathf.Max(2, cfg.laps);
            // calibrate consumption so a full tank covers ~70 % of the race and tyres last their life fraction
            fuelRateFull = VehicleSpec.FuelTankKg / (TankLaps(n) * estimatedLapTime * 0.62f);

            for (int i = 0; i < cfg.seats.Count; i++) SpawnCar(cfg.seats[i], i);

            if (cfg.type == SessionType.TimeTrial)
            {
                ghost = gameObject.AddComponent<GhostRecorder>();
                ghost.Init(this, playerCar);
            }

            state = RaceState.Countdown;
            countdown = 0f;
            lightsOn = 0;
            lightsOutDelay = UnityEngine.Random.Range(0.4f, 1.6f);
            world.SetStartLights(0);
            rig.Follow(playerCar.car, playerCar.visual);
            foreach (var rc in cars) rc.car.frozen = true;
            if (cfg.type == SessionType.TimeTrial)
            {
                // no start procedure in time trial – go immediately
                state = RaceState.Running;
                foreach (var rc in cars) rc.car.frozen = false;
            }
        }

        void SpawnCar(int seat, int gridSlot)
        {
            var driver = GameData.Drivers[seat];
            bool isPlayer = seat == config.playerSeat;
            var team = GameData.Teams[driver.team];
            var rc = new RaceCar
            {
                id = cars.Count,
                seat = seat,
                team = driver.team,
                isPlayer = isPlayer,
                name = isPlayer ? config.playerName : driver.name,
                shortName = isPlayer ? MakeShort(config.playerName) : driver.shortName,
                color = team.primary,
            };

            var go = new GameObject((isPlayer ? "PlayerCar_" : "Car_") + rc.shortName);
            go.transform.SetParent(transform, false);
            go.layer = CarController.CarLayer;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = VehicleSpec.DryMass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.SetDamping(0.0f, 0.15f);
            // colliders: monocoque + one box per axle (so wheel-to-wheel contact works)
            AddBox(go, new Vector3(0f, 0.48f, -0.3f), new Vector3(1.25f, 0.55f, 4.6f));
            AddBox(go, new Vector3(0f, 0.45f, VehicleSpec.FrontAxleZ), new Vector3(1.85f, 0.5f, 0.55f));
            AddBox(go, new Vector3(0f, 0.45f, VehicleSpec.RearAxleZ), new Vector3(1.95f, 0.5f, 0.6f));
            AddBox(go, new Vector3(0f, 0.3f, 2.55f), new Vector3(1.7f, 0.25f, 0.5f)); // front wing

            var car = go.AddComponent<CarController>();
            rc.car = car;
            car.isPlayer = isPlayer;
            car.track = track;
            var s = GameSettings.Current;
            car.assists = isPlayer ? s.assists : DriverAssists.Full;
            car.fuelRate = fuelRateFull;
            bool tt = config.type == SessionType.TimeTrial;
            car.fuelMultiplier = tt ? 0f : s.FuelFactor;
            car.wearMultiplier = tt ? 0f : s.WearFactor;

            CarSetup setup;
            if (isPlayer) setup = config.playerSetup ?? new CarSetup();
            else setup = CarSetup.Preset(track.def.recommended);

            // strategy at the start
            if (isPlayer)
            {
                car.fuel = tt ? 40f : Mathf.Clamp(config.playerFuel, 5f, VehicleSpec.FuelTankKg);
                car.compound = config.playerCompound;
            }
            else
            {
                float r = UnityEngine.Random.value;
                car.compound = r < 0.3f ? TyreCompound.Soft : r < 0.8f ? TyreCompound.Medium : TyreCompound.Hard;
                if (config.laps <= 3 && car.compound == TyreCompound.Hard) car.compound = TyreCompound.Medium;
                float firstStop = Mathf.Clamp(Mathf.Round(Tyres.LifeFraction(car.compound) * config.laps), 1, Mathf.Max(1, config.laps - 1));
                car.fuel = Mathf.Min(VehicleSpec.FuelTankKg, FuelPerLapEstimate(car) * (firstStop + 0.7f));
                if (tt) car.fuel = 40f;
            }
            car.ApplySetup(setup);
            car.wearRate = WearRate(car.compound);
            rc.startCompound = car.compound;
            rc.usedCompounds.Add(car.compound);
            rc.fuelAtLapStart = car.fuel;
            rc.fuelPerLap = FuelPerLapEstimate(car);
            rc.planCompound = car.compound;

            Vector3 pos = TrackBuilder.GridSlot(track, gridSlot, out var rot);
            if (tt) { pos = track.PositionAt(-60f); rot = Quaternion.LookRotation(track.TangentAt(-60f)); }
            car.Teleport(pos + Vector3.up * 0.02f, rot, 0f);

            var vis = go.AddComponent<CarVisual>();
            vis.Build(car, team.primary, team.secondary);
            rc.visual = vis;
            var audio = go.AddComponent<CarAudio>();
            audio.Init(car, isPlayer);

            var ai = go.AddComponent<AIDriver>();
            ai.Init(this, rc, driver);
            rc.ai = ai;
            if (isPlayer)
            {
                ai.enabled = false;
                var pd = go.AddComponent<PlayerDriver>();
                pd.Init(this, rc);
                rc.player = pd;
                playerCar = rc;
                if (GameSettings.Current.showRacingLine) go.AddComponent<RacingLineDisplay>().Init(this, rc);
            }

            int idx = track.FindIndex(pos, -1);
            rc.trackIndex = idx;
            rc.lapDistance = track.DistanceAlong(idx, pos);
            rc.totalDistance = track.SignedDistance(rc.lapDistance);
            rc.lastSignedDist = rc.totalDistance;
            cars.Add(rc);
        }

        static string MakeShort(string n)
        {
            n = (n ?? "OYN").Trim().ToUpperInvariant().Replace(" ", "");
            return n.Length >= 3 ? n.Substring(0, 3) : n.PadRight(3, 'X');
        }

        static void AddBox(GameObject go, Vector3 c, Vector3 size)
        {
            var b = go.AddComponent<BoxCollider>();
            b.center = c;
            b.size = size;
            Compat.SetSlippery(b);
        }

        /// <summary>How many laps a full tank lasts: ~70 % of the race (so at least one refuelling stop), never less than 2.5 laps.</summary>
        public static float TankLaps(int raceLaps) => Mathf.Max(0.70f * raceLaps, 2.5f);

        public float FuelPerLapEstimate(CarController car)
        {
            return fuelRateFull * 0.62f * estimatedLapTime * car.fuelMultiplier;
        }

        public float WearRate(TyreCompound c)
        {
            int n = Mathf.Max(2, config.laps);
            return Tyres.CriticalWear / (Tyres.LifeFraction(c) * n * estimatedLapTime * 0.8f);
        }

        // ------------------------------------------------------------------ main loop
        void Update()
        {
            if (state == RaceState.Countdown) UpdateCountdown(Time.deltaTime);
            if (eventTimer > 0f) eventTimer -= Time.deltaTime;
            UpdateWarnings();
            if (state == RaceState.Finished && finishedTimer >= 0f)
            {
                finishedTimer += Time.deltaTime;
            }
        }

        void UpdateCountdown(float dt)
        {
            countdown += dt;
            int want = Mathf.Clamp(Mathf.FloorToInt(countdown - 1f) + 1, 0, 5);
            if (countdown < 1f) want = 0;
            if (want != lightsOn && countdown < 6f)
            {
                lightsOn = want;
                world.SetStartLights(lightsOn);
                if (lightsOn > 0) Beep(SynthAudio.BeepLow, 0.6f);
            }
            if (countdown >= 6f + lightsOutDelay)
            {
                world.SetStartLights(0);
                lightsOn = 0;
                state = RaceState.Running;
                raceTime = 0f;
                foreach (var rc in cars) { rc.car.frozen = false; rc.lapStartTime = 0f; rc.sectorStart = 0f; }
                Beep(SynthAudio.BeepHigh, 0.7f);
                ShowEvent("IŞIKLAR SÖNDÜ — YARIŞ BAŞLADI!");
            }
        }

        public void Beep(AudioClip c, float vol)
        {
            if (uiAudio != null && c != null) uiAudio.PlayOneShot(c, vol * GameSettings.SfxVolume);
        }

        public void ShowEvent(string msg, float seconds = 3f)
        {
            eventMessage = msg;
            eventTimer = seconds;
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (state == RaceState.Running || state == RaceState.Finished) raceTime += dt;
            foreach (var rc in cars) UpdateProgress(rc, dt);
            if (state != RaceState.Countdown)
            {
                UpdatePositions();
                UpdateAero();
                foreach (var rc in cars)
                {
                    if (rc.InPit) PitTick(rc, dt);
                    else CheckPitEntry(rc);
                    if (rc.ghostTimer > 0f && !rc.InPit)
                    {
                        rc.ghostTimer -= dt;
                        if (rc.ghostTimer <= 0f)
                        {
                            if (ClearOfOthers(rc, 6f)) SetGhost(rc, false);
                            else rc.ghostTimer = 0.3f; // still overlapping someone: stay ghosted a bit longer
                        }
                    }
                    if (!rc.isPlayer || rc.finished) CheckRecovery(rc, dt);
                }
            }
        }

        // ------------------------------------------------------------------ timing
        void UpdateProgress(RaceCar rc, float dt)
        {
            Vector3 pos = rc.car.transform.position;
            int idx = track.FindIndex(pos, rc.trackIndex);
            rc.trackIndex = idx;
            if (!rc.car.IsKinematic) rc.car.trackIndex = idx;
            float d = track.DistanceAlong(idx, pos);
            rc.lateral = track.LateralOffset(idx, pos);
            float delta = d - rc.lapDistance;
            if (delta < -track.length * 0.5f) delta += track.length;
            if (delta > track.length * 0.5f) delta -= track.length;
            rc.lapDistance = d;
            if (state == RaceState.Countdown) return;
            float prevTotal = rc.totalDistance;
            rc.totalDistance += delta;

            // timing markers (every 20 m of race distance)
            if (rc.totalDistance > 0f)
            {
                int marker = Mathf.FloorToInt(rc.totalDistance / MarkerSpacing);
                while (rc.markerTimes.Count <= marker) rc.markerTimes.Add(raceTime);
            }

            if (!rc.finished && raceTime > 2f) rc.avgSpeed = Mathf.Max(5f, rc.totalDistance / raceTime);

            // sectors
            float inLap = Mathf.Repeat(rc.totalDistance, track.length);
            int sec = Mathf.Clamp(Mathf.FloorToInt(inLap / (track.length / 3f)), 0, 2);
            if (rc.totalDistance > 0f && sec != rc.sector && sec == (rc.sector + 1) % 3 && sec != 0)
            {
                rc.sectorTimes[rc.sector] = raceTime - rc.sectorStart;
                rc.sectorStart = raceTime;
                rc.sector = sec;
            }

            if (rc.isPlayer && rc.car.WheelsOffTrack >= 4 && !rc.InPit) rc.lapInvalid = true;

            // line crossings
            int prevCross = Mathf.FloorToInt(prevTotal / track.length);
            int cross = Mathf.FloorToInt(rc.totalDistance / track.length);
            if (cross > prevCross && delta > 0f)
            {
                float over = rc.totalDistance - cross * track.length;
                float tCross = raceTime - dt * Mathf.Clamp01(over / Mathf.Max(delta, 1e-4f));
                OnLineCrossed(rc, cross, tCross);
            }
        }

        void OnLineCrossed(RaceCar rc, int crossing, float t)
        {
            bool tt = config.type == SessionType.TimeTrial;
            if (crossing <= 0)
            {
                // first time over the line: in time trial this starts the first timed lap
                if (tt) { rc.lapStartTime = t; rc.sectorStart = t; rc.sector = 0; rc.lapInvalid = false; ghost?.StartLap(t); }
                return;
            }
            float lapTime = t - rc.lapStartTime;
            rc.sectorTimes[2] = t - rc.sectorStart;
            Array.Copy(rc.sectorTimes, rc.lastSectorTimes, 3);
            rc.sector = 0;
            rc.sectorStart = t;
            rc.lapStartTime = t;
            rc.lastLapTime = lapTime;
            rc.lapTimes.Add(lapTime);
            rc.lapsCompleted = crossing;
            bool valid = !rc.lapInvalid;
            if (valid && lapTime < rc.bestLapTime) rc.bestLapTime = lapTime;
            if (valid && lapTime < fastestLap && IsRace)
            {
                fastestLap = lapTime;
                fastestLapCar = rc;
                if (raceTime > 5f && crossing > 1) ShowEvent($"EN HIZLI TUR: {rc.name} {FormatTime(lapTime)}");
            }
            if (rc.isPlayer && valid)
            {
                for (int k = 0; k < 3; k++) bestSectors[k] = Mathf.Min(bestSectors[k], rc.lastSectorTimes[k]);
                if (tt && lapTime < bestSessionLap)
                {
                    bestSessionLap = lapTime;
                    ShowEvent("YENİ EN İYİ TUR!  " + FormatTime(lapTime));
                    SaveSystem.RecordBestLap(track.def.id, lapTime);
                }
            }
            if (rc.isPlayer && tt)
            {
                if (!valid) ShowEvent("TUR GEÇERSİZ (pist sınırı aşıldı)");
                ghost?.LapCompleted(lapTime, valid);
            }
            rc.lapInvalid = false;

            // per-lap consumption bookkeeping
            float used = rc.fuelAtLapStart - rc.car.fuel;
            if (used > 0.01f) rc.fuelPerLap = Mathf.Lerp(rc.fuelPerLap, used, rc.lapsCompleted <= 1 ? 0.6f : 0.5f);
            rc.fuelAtLapStart = rc.car.fuel;
            float wearUsed = rc.car.MaxTyreWear - rc.wearAtLapStart;
            if (wearUsed > 0f) rc.wearPerLap = rc.wearPerLap <= 0f ? wearUsed : Mathf.Lerp(rc.wearPerLap, wearUsed, 0.5f);
            rc.wearAtLapStart = rc.car.MaxTyreWear;
            rc.pitDecisionMade = false;

            if (!IsRace) return;
            if (!checkered && rc.lapsCompleted >= config.laps)
            {
                checkered = true;
                ShowEvent($"DAMALI BAYRAK! {rc.name} kazandı!", 4f);
            }
            if (checkered && !rc.finished)
            {
                rc.finished = true;
                rc.finishTime = t;
                rc.car.fuelMultiplier = 0f; // cool-down lap: no more consumption or wear
                rc.car.wearMultiplier = 0f;
                rc.boxRequested = false;
                if (rc.isPlayer) OnPlayerFinished();
                else rc.ai.cooldown = true;
            }
        }

        void OnPlayerFinished()
        {
            ShowEvent($"YARIŞ BİTTİ — {playerCar.position}. sıra", 4f);
            playerCar.player.enabled = false;
            playerCar.ai.enabled = true;
            playerCar.ai.cooldown = true;
            var rl = playerCar.car.GetComponent<RacingLineDisplay>();
            if (rl != null) rl.enabled = false;
            Finish();
        }

        /// <summary>Ends the session and classifies everybody (unfinished cars are projected to the line).</summary>
        public void Finish(bool playerRetired = false)
        {
            if (state == RaceState.Finished) return;
            if (playerRetired && playerCar != null) playerCar.retired = true;
            state = RaceState.Finished;
            finishedTimer = 0f;
            results = BuildResults();
            if (IsRace && playerCar != null && !playerCar.retired)
            {
                var me = results.Find(r => r.isPlayer);
                if (me != null && me.position <= 3) Beep(Resources.Load<AudioClip>("FreeAssets/Audio/level_up"), 0.9f);
            }
            OnRaceFinished?.Invoke();
        }

        List<ResultRow> BuildResults()
        {
            var rows = new List<ResultRow>();
            float L = track.length;
            float lead = -1f;
            float tLead = raceTime;
            if (IsRace && !checkered)
            {
                // the projected winner: furthest car that is still running
                RaceCar best = null;
                foreach (var rc in cars) if (!rc.retired && (best == null || rc.totalDistance > best.totalDistance)) best = rc;
                if (best != null)
                {
                    lead = config.laps * L;
                    tLead = raceTime + Mathf.Max(0f, lead - best.totalDistance) / Mathf.Max(best.avgSpeed, 10f);
                }
            }
            foreach (var rc in cars)
            {
                var row = new ResultRow
                {
                    seat = rc.seat, team = rc.team, isPlayer = rc.isPlayer, name = rc.name,
                    bestLap = rc.bestLapTime, pitStops = rc.pitStops, dnf = rc.retired
                };
                if (!IsRace)
                {
                    row.laps = rc.lapsCompleted;
                    row.time = rc.bestLapTime;
                }
                else if (rc.finished)
                {
                    row.laps = rc.lapsCompleted;
                    row.time = rc.finishTime;
                }
                else if (rc.retired)
                {
                    row.laps = rc.lapsCompleted;
                    row.time = float.MaxValue;
                }
                else
                {
                    float v = Mathf.Max(rc.avgSpeed, 10f);
                    float atLead = rc.totalDistance + v * Mathf.Max(0f, tLead - raceTime);
                    int lapsAt = Mathf.Min(config.laps, Mathf.CeilToInt(atLead / L - 1e-4f));
                    lapsAt = Mathf.Max(lapsAt, rc.lapsCompleted + 1);
                    lapsAt = Mathf.Min(lapsAt, config.laps);
                    row.laps = lapsAt;
                    row.time = raceTime + Mathf.Max(0f, lapsAt * L - rc.totalDistance) / v;
                }
                rows.Add(row);
            }
            rows.Sort((a, b) =>
            {
                if (a.dnf != b.dnf) return a.dnf ? 1 : -1;
                if (!IsRace) return a.time.CompareTo(b.time);
                if (a.laps != b.laps) return b.laps.CompareTo(a.laps);
                return a.time.CompareTo(b.time);
            });
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].position = i + 1;
                if (IsRace && !rows[i].dnf && i < GameData.Points.Length) rows[i].points = GameData.Points[i];
            }
            if (IsRace && fastestLapCar != null)
            {
                var fl = rows.Find(r => r.seat == fastestLapCar.seat);
                if (fl != null)
                {
                    fl.fastestLap = true;
                    if (fl.position <= 10 && !fl.dnf) fl.points += GameData.FastestLapPoint;
                }
            }
            return rows;
        }

        // ------------------------------------------------------------------ positions & gaps
        readonly List<RaceCar> order = new List<RaceCar>();

        void UpdatePositions()
        {
            order.Clear();
            order.AddRange(cars);
            order.Sort((a, b) =>
            {
                if (a.retired != b.retired) return a.retired ? 1 : -1;
                if (a.finished != b.finished) return a.finished ? -1 : 1;
                if (a.finished && b.finished)
                {
                    if (a.lapsCompleted != b.lapsCompleted) return b.lapsCompleted.CompareTo(a.lapsCompleted);
                    return a.finishTime.CompareTo(b.finishTime);
                }
                return b.totalDistance.CompareTo(a.totalDistance);
            });
            if (!IsRace)
            {
                order.Sort((a, b) => a.bestLapTime.CompareTo(b.bestLapTime));
            }
            for (int i = 0; i < order.Count; i++)
            {
                var rc = order[i];
                rc.position = i + 1;
                rc.gapAhead = i > 0 ? TimeGap(rc, order[i - 1]) : 0f;
                rc.gapLeader = i > 0 ? TimeGap(rc, order[0]) : 0f;
                rc.lapsDownToLeader = i > 0 ? Mathf.FloorToInt((order[0].totalDistance - rc.totalDistance) / track.length) : 0;
            }
        }

        public List<RaceCar> Order => order;

        /// <summary>Time gap (s) between a car and one ahead of it, using the 20 m timing markers.</summary>
        public float TimeGap(RaceCar behind, RaceCar ahead)
        {
            int k = behind.markerTimes.Count - 1;
            if (k < 0) return 0f;
            if (ahead.markerTimes.Count > k) return Mathf.Max(0f, behind.markerTimes[k] - ahead.markerTimes[k]);
            return (ahead.totalDistance - behind.totalDistance) / Mathf.Max(behind.car.Speed, 10f);
        }

        // ------------------------------------------------------------------ slipstream / dirty air / DRS
        void UpdateAero()
        {
            float L = track.length;
            foreach (var a in cars)
            {
                a.car.slipstreamFactor = 1f;
                a.car.wakeDownforceFactor = 1f;
                if (a.InPit) continue;
                float bestGap = float.MaxValue;
                RaceCar ahead = null;
                foreach (var b in cars)
                {
                    if (b == a || b.InPit) continue;
                    float g = Mathf.Repeat(b.totalDistance - a.totalDistance, L);
                    if (g < 1f || g > 60f) continue;
                    if (Mathf.Abs(b.lateral - a.lateral) > 3.2f) continue;
                    if (g < bestGap) { bestGap = g; ahead = b; }
                }
                if (ahead != null)
                {
                    if (bestGap < 50f) a.car.slipstreamFactor = 1f - 0.28f * (1f - bestGap / 50f);
                    if (bestGap < 25f) a.car.wakeDownforceFactor = 1f - 0.18f * (1f - bestGap / 25f);
                }

                // DRS
                float sd = track.SignedDistance(a.lapDistance);
                bool drsOn = GameSettings.Current.drsEnabled;
                if (IsRace)
                {
                    if (a.lastSignedDist < track.drsDetection && sd >= track.drsDetection)
                    {
                        RaceCar front = null;
                        float fg = float.MaxValue;
                        foreach (var b in cars)
                        {
                            if (b == a || b.InPit) continue;
                            float g = Mathf.Repeat(b.totalDistance - a.totalDistance, L);
                            if (g > 0.5f && g < fg) { fg = g; front = b; }
                        }
                        a.drsEligible = drsOn && front != null && a.lapsCompleted >= 1 && !checkered && fg / Mathf.Max(a.car.Speed, 20f) < 1.0f;
                    }
                }
                else a.drsEligible = drsOn;
                bool inZone = sd >= track.drsZoneStart && sd <= track.drsZoneEnd;
                a.car.drsAvailable = a.drsEligible && inZone && state == RaceState.Running;
                if (!a.car.drsAvailable || a.car.brakeApplied > 0.1f) a.car.drsOpen = false;
                if (!inZone && sd > track.drsZoneEnd) a.drsEligible = IsRace ? false : a.drsEligible;
                a.lastSignedDist = sd;
            }
        }

        // ------------------------------------------------------------------ pit stops
        void CheckPitEntry(RaceCar rc)
        {
            if (!IsRace || state != RaceState.Running || rc.finished) return;
            float sd = track.SignedDistance(rc.lapDistance);
            if (!rc.isPlayer && !rc.pitDecisionMade && sd > track.pitEntry - 320f && sd < track.pitEntry - 200f)
            {
                rc.pitDecisionMade = true;
                AIPitDecision(rc);
            }
            bool inEntry = sd >= track.pitEntry - 5f && sd <= track.pitEntry + 45f;
            if (!inEntry) return;
            bool drivesIn = rc.lateral > track.halfWidth + 1.8f;
            bool lastLap = rc.lapsCompleted >= config.laps - 1 || checkered; // no finishing through the pit lane
            if ((rc.boxRequested || drivesIn) && !lastLap) StartPit(rc, sd);
        }

        /// <summary>Remaining laps after the current one is finished.</summary>
        public int LapsRemaining(RaceCar rc) => Mathf.Max(0, config.laps - rc.lapsCompleted);

        public void SuggestPitPlan(RaceCar rc, out TyreCompound compound, out float fuelAdd)
        {
            int remaining = Mathf.Max(1, LapsRemaining(rc) - 1); // laps after this stop (the stop is at the end of this lap)
            float frac = remaining / (float)Mathf.Max(1, config.laps);
            compound = TyreCompound.Hard;
            if (frac <= Tyres.LifeFraction(TyreCompound.Soft) * 0.95f) compound = TyreCompound.Soft;
            else if (frac <= Tyres.LifeFraction(TyreCompound.Medium) * 0.95f) compound = TyreCompound.Medium;
            float perLap = Mathf.Max(rc.fuelPerLap, 0.01f);
            float fuelAtStop = Mathf.Max(0f, rc.car.fuel - perLap * 0.3f);
            float need = perLap * (remaining + 0.5f) - fuelAtStop;
            fuelAdd = Mathf.Clamp(need, 0f, VehicleSpec.FuelTankKg - fuelAtStop);
            if (rc.car.fuelMultiplier <= 0f) fuelAdd = 0f;
        }

        void AIPitDecision(RaceCar rc)
        {
            int remaining = LapsRemaining(rc);
            if (remaining <= 1) return; // final lap: never stop
            float perLap = Mathf.Max(rc.fuelPerLap, 0.01f);
            float wearNext = rc.car.MaxTyreWear + Mathf.Max(rc.wearPerLap, 0.05f);
            bool fuelShort = rc.car.fuelMultiplier > 0f && rc.car.fuel < perLap * 1.35f;
            bool tyresGone = rc.car.wearMultiplier > 0f && (wearNext > 0.84f || rc.car.MaxTyreWear > Tyres.CriticalWear + 0.05f);
            if (!fuelShort && !tyresGone) return;
            SuggestPitPlan(rc, out var c, out var f);
            rc.planCompound = c;
            rc.planFuelAdd = f;
            rc.boxRequested = true;
        }

        void StartPit(RaceCar rc, float sd)
        {
            rc.pit = PitPhase.Entering;
            rc.pitS = sd;
            rc.pitEntryS = sd;
            rc.pitSpeed = Mathf.Max(5f, rc.car.ForwardSpeed);
            rc.pitOffsetStart = rc.lateral;
            rc.car.SetKinematic(true);
            SetGhost(rc, true);
            if (rc.isPlayer)
            {
                if (!rc.planEdited)
                {
                    SuggestPitPlan(rc, out var c, out var f);
                    rc.planCompound = c;
                    rc.planFuelAdd = f;
                }
                ShowEvent("PİT ŞERİDİ — hız sınırlayıcı aktif (80 km/sa)");
            }
        }

        void PitTick(RaceCar rc, float dt)
        {
            float limit = track.pitSpeedLimit;
            float boxS = track.BoxDistance(rc.team);
            switch (rc.pit)
            {
                case PitPhase.Entering:
                case PitPhase.ToBox:
                {
                    float toBox = boxS - rc.pitS;
                    float vStop = Mathf.Sqrt(2f * 7f * Mathf.Max(0f, toBox));
                    float vt = Mathf.Min(limit, vStop);
                    rc.pitSpeed = Mathf.MoveTowards(rc.pitSpeed, vt, (rc.pitSpeed > vt ? 15f : 6f) * dt);
                    if (rc.pit == PitPhase.Entering && rc.pitS > rc.pitEntryS + 60f) rc.pit = PitPhase.ToBox;
                    if (toBox <= 0.08f && rc.pitSpeed < 3f)
                    {
                        rc.pitS = boxS;
                        rc.pitSpeed = 0f;
                        BeginService(rc);
                    }
                    break;
                }
                case PitPhase.Service:
                    rc.pitSpeed = 0f;
                    rc.serviceTimer += dt;
                    if (rc.serviceTimer >= rc.serviceTotal) CompleteService(rc);
                    break;
                case PitPhase.Leaving:
                    rc.pitSpeed = Mathf.MoveTowards(rc.pitSpeed, limit, 7f * dt);
                    if (rc.pitS > track.pitExit - track.pitBlend) rc.pit = PitPhase.Exiting;
                    break;
                case PitPhase.Exiting:
                    rc.pitSpeed = Mathf.MoveTowards(rc.pitSpeed, 42f, 9f * dt);
                    break;
            }
            rc.pitS += rc.pitSpeed * dt;
            if (rc.pit == PitPhase.Exiting && rc.pitS >= track.pitExit)
            {
                ReleaseFromPit(rc);
                return;
            }
            Vector3 p = PitPathPoint(rc, rc.pitS, boxS);
            Vector3 p2 = PitPathPoint(rc, rc.pitS + 2f, boxS);
            Vector3 dir = p2 - p;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = track.TangentAt(rc.pitS);
            p.y = rc.car.setup.PlankClearance - 0.036f;
            rc.car.rb.MovePosition(p);
            rc.car.rb.MoveRotation(Quaternion.LookRotation(dir.normalized, Vector3.up));
            rc.car.kinematicSpeed = rc.pitSpeed;
        }

        Vector3 PitPathPoint(RaceCar rc, float s, float boxS)
        {
            float off = track.PitOffsetAt(s);
            if (s < rc.pitEntryS + 60f)
                off = Mathf.Lerp(rc.pitOffsetStart, off, Mathf.SmoothStep(0f, 1f, (s - rc.pitEntryS) / 60f));
            float box = 1f - Mathf.Clamp01(Mathf.Abs(s - boxS) / 18f);
            off += 4.5f * Mathf.SmoothStep(0f, 1f, box);
            return track.PositionAt(s) + track.RightAt(s) * off;
        }

        void BeginService(RaceCar rc)
        {
            rc.pit = PitPhase.Service;
            rc.serviceTimer = 0f;
            float fuel = rc.car.fuelMultiplier > 0f ? Mathf.Clamp(rc.planFuelAdd, 0f, VehicleSpec.FuelTankKg - rc.car.fuel) : 0f;
            rc.planFuelAdd = fuel;
            float t = 2.2f + UnityEngine.Random.Range(0f, 0.6f);
            t = Mathf.Max(t, fuel / 6f + 1.2f);           // refuelling at 6 kg/s overlaps the tyre change
            if (rc.planWingDelta != 0) t += 0.8f;
            rc.serviceNote = "";
            if (UnityEngine.Random.value < 0.05f)
            {
                float extra = UnityEngine.Random.Range(2f, 4.5f);
                t += extra;
                rc.serviceNote = "Sorunlu pit stop! Arka sol tekerlek takıldı.";
            }
            rc.serviceTotal = t;
            if (rc.isPlayer) Beep(SynthAudio.PitBeep, 0.8f);
        }

        void CompleteService(RaceCar rc)
        {
            var car = rc.car;
            car.FitNewTyres(rc.planCompound);
            car.wearRate = WearRate(rc.planCompound);
            float before = car.fuel;
            car.fuel = Mathf.Min(VehicleSpec.FuelTankKg, car.fuel + rc.planFuelAdd);
            float added = car.fuel - before;
            if (rc.planWingDelta != 0)
            {
                var s = car.setup.Clone();
                s.frontWing = Mathf.Clamp(s.frontWing + rc.planWingDelta, CarSetup.WingMin, CarSetup.WingMax);
                car.ApplySetup(s);
                if (rc.isPlayer) GameManager.Instance?.OnPlayerSetupChangedInPit(s);
            }
            rc.usedCompounds.Add(rc.planCompound);
            rc.pitStops++;
            rc.boxRequested = false;
            rc.planWingDelta = 0;
            rc.fuelAtLapStart += added; // keeps the per-lap consumption figure meaningful
            rc.planEdited = false;
            rc.wearAtLapStart = 0f;
            rc.pit = PitPhase.Leaving;
            if (rc.isPlayer)
            {
                Beep(SynthAudio.BeepHigh, 0.5f);
                ShowEvent($"Pit stop {rc.serviceTotal:0.0} sn — {Tyres.Name(rc.planCompound)} lastik, +{added:0} kg yakıt");
            }
            rc.planFuelAdd = 0f;
        }

        void ReleaseFromPit(RaceCar rc)
        {
            float s = track.pitExit;
            Vector3 p = track.PositionAt(s) + track.RightAt(s) * track.PitOffsetAt(s);
            p.y = 0.02f;
            rc.car.SetKinematic(false);
            rc.car.Teleport(p, Quaternion.LookRotation(track.TangentAt(s)), rc.pitSpeed);
            rc.pit = PitPhase.None;
            rc.ghostTimer = 1.5f;
            rc.lateral = track.PitOffsetAt(s);
            if (rc.ai != null) rc.ai.ResetLane(rc.lateral, s);
            if (rc.isPlayer) ShowEvent("Pitten çıktın — hız sınırlayıcı kapandı. Bas gaza!");
        }

        public void SetGhost(RaceCar rc, bool ghostOn)
        {
            foreach (var c in rc.car.GetComponents<Collider>()) c.isTrigger = ghostOn;
        }

        bool ClearOfOthers(RaceCar rc, float radius)
        {
            Vector3 p = rc.car.transform.position;
            foreach (var o in cars)
            {
                if (o == rc) continue;
                if ((o.car.transform.position - p).sqrMagnitude < radius * radius) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ recovery
        void CheckRecovery(RaceCar rc, float dt)
        {
            if (rc.InPit || state != RaceState.Running) { rc.stuckTimer = 0f; return; }
            var car = rc.car;
            bool slow = car.Speed < 2.5f && !car.frozen;
            rc.stuckTimer = slow ? rc.stuckTimer + dt : 0f;
            Vector3 tan = track.tangent[Mathf.Max(0, rc.trackIndex)];
            bool backwards = Vector3.Dot(car.transform.forward, tan) < -0.2f;
            rc.wrongWayTimer = backwards ? rc.wrongWayTimer + dt : 0f;
            if (rc.stuckTimer > 4f || car.upsideDownTime > 1.5f || rc.wrongWayTimer > 2.5f)
                Respawn(rc);
            if (car.OutOfFuel && car.Speed < 1f && rc.stuckTimer > 3f && IsRace && !rc.finished)
            {
                rc.retired = true;
                ShowEvent($"{rc.name} yakıtı bitti — yarış dışı!");
            }
        }

        public void Respawn(RaceCar rc)
        {
            if (rc.InPit) return;
            float d = rc.lapDistance - 8f;
            // find a spot that is clear of other cars
            for (int tries = 0; tries < 12; tries++)
            {
                Vector3 test = track.LinePositionAt(d);
                bool clear = true;
                foreach (var o in cars)
                    if (o != rc && (o.car.transform.position - test).sqrMagnitude < 64f) { clear = false; break; }
                if (clear) break;
                d -= 10f;
            }
            Vector3 p = track.LinePositionAt(d);
            p.y = 0.05f;
            rc.car.Teleport(p, Quaternion.LookRotation(track.TangentAt(d)), 0f);
            if (rc.ai != null) rc.ai.ResetLane(track.LineOffsetAt(d), d);
            rc.stuckTimer = 0f;
            rc.wrongWayTimer = 0f;
            rc.ghostTimer = 2f;
            SetGhost(rc, true);
            if (rc.isPlayer)
            {
                rc.lapInvalid = true;
                ShowEvent("Araç piste geri konuldu");
            }
        }

        // ------------------------------------------------------------------ player warnings
        void UpdateWarnings()
        {
            warnings.Clear();
            if (playerCar == null || state != RaceState.Running || !IsRace || playerCar.finished) return;
            var car = playerCar.car;
            float perLap = Mathf.Max(playerCar.fuelPerLap, 0.01f);
            float lapsLeftInRace = config.laps - playerCar.lapsCompleted - Mathf.Repeat(playerCar.totalDistance, track.length) / track.length;
            float fuelLaps = car.fuel / perLap;
            if (car.wearMultiplier > 0f && car.MaxTyreWear >= Tyres.CriticalWear)
                warnings.Add($"LASTİKLER KRİTİK (%{car.MaxTyreWear * 100f:0}) — PİTE GİR! [B]");
            if (car.fuelMultiplier > 0f)
            {
                if (car.OutOfFuel) warnings.Add("YAKIT BİTTİ!");
                else if (fuelLaps < 0.6f && lapsLeftInRace > fuelLaps) warnings.Add($"YAKIT KRİTİK ({car.fuel:0.0} kg) — HEMEN PİTE GİR! [B]");
                else if (fuelLaps < 1.4f && lapsLeftInRace > fuelLaps) warnings.Add($"YAKIT AZALIYOR (~{fuelLaps:0.0} tur) — bu tur pite girmeyi düşün [B]");
            }
            if (playerCar.wrongWayTimer > 1.5f) warnings.Add("TERS YÖN!");
            if (warnings.Count > 0 && !playerCar.InPit)
            {
                warningBeepTimer -= Time.unscaledDeltaTime;
                if (warningBeepTimer <= 0f)
                {
                    Beep(SynthAudio.PitBeep, 0.7f);
                    warningBeepTimer = 8f;
                }
            }
            else warningBeepTimer = 0f;

            // wrong-way for the player
            Vector3 tan = track.tangent[Mathf.Max(0, playerCar.trackIndex)];
            bool backwards = Vector3.Dot(car.transform.forward, tan) < -0.4f && car.Speed > 4f;
            playerCar.wrongWayTimer = backwards ? playerCar.wrongWayTimer + Time.deltaTime : 0f;
        }

        // ------------------------------------------------------------------ utils
        public static string FormatTime(float t)
        {
            if (t >= float.MaxValue * 0.5f || t < 0f) return "--:--.---";
            int m = Mathf.FloorToInt(t / 60f);
            float s = t - m * 60f;
            return $"{m}:{s:00.000}";
        }

        public static string FormatGap(float g) => g < 0.0005f ? "" : $"+{g:0.000}";

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
