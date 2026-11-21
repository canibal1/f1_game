using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using F1Game;
using UnityEngine;

static class Program
{
    static readonly Dictionary<Type, (MethodInfo fixedU, MethodInfo upd)> methods = new Dictionary<Type, (MethodInfo, MethodInfo)>();
    static void Call(string phase)
    {
        foreach (var go in GameObject.All.ToArray())
        {
            if (go.destroyed) continue;
            foreach (var c in go.components.ToArray())
            {
                if (c is Behaviour b && !b.enabled) continue;
                var t = c.GetType();
                if (!methods.TryGetValue(t, out var m))
                {
                    var bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                    m = (t.GetMethod("FixedUpdate", bf), t.GetMethod("Update", bf));
                    methods[t] = m;
                }
                var mi = phase == "fixed" ? m.fixedU : m.upd;
                mi?.Invoke(c, null);
            }
        }
    }

    static void Main(string[] args)
    {
        int trackIdx = args.Length > 0 ? int.Parse(args[0]) : 0;
        int laps = args.Length > 1 ? int.Parse(args[1]) : 5;
        int grid = args.Length > 2 ? int.Parse(args[2]) : 20;
        float pace = args.Length > 3 ? float.Parse(args[3]) : 60f;
        UnityEngine.Random.Seed(42 + trackIdx);
        if (Environment.GetEnvironmentVariable("STANLEY") != null) AIDriver.StanleyGain = float.Parse(Environment.GetEnvironmentVariable("STANLEY"));
        GameSettings.Current = new GameSettings { aiPace = pace, aiAggression = float.Parse(Environment.GetEnvironmentVariable("AGGR") ?? "50"), raceLaps = laps };
        var cfg = new RaceConfig { trackIndex = trackIdx, laps = laps, playerSeat = 1, playerName = "Test", playerCompound = TyreCompound.Medium, playerFuel = 60f,
            playerSetup = CarSetup.Preset(GameData.Tracks[trackIdx].recommended) };
        var su = Environment.GetEnvironmentVariable("SETUP");
        if (su == "low") cfg.playerSetup = CarSetup.Preset(CarSetupPreset.LowDownforce);
        if (su == "high") cfg.playerSetup = CarSetup.Preset(CarSetupPreset.HighDownforce);
        if (su == "lowride") { cfg.playerSetup = CarSetup.Preset(CarSetupPreset.HighDownforce); cfg.playerSetup.rideHeight = 1; cfg.playerSetup.frontSuspension = 1; cfg.playerSetup.rearSuspension = 1; }
        for (int i = 0; i < grid; i++) cfg.seats.Add(i);
        bool tt = Environment.GetEnvironmentVariable("TT") != null;
        if (Environment.GetEnvironmentVariable("NOASSIST") != null) GameSettings.Current.assists = new DriverAssists { autoGearbox = true };
        if (tt) { cfg.type = SessionType.TimeTrial; cfg.laps = 999; cfg.seats = new List<int> { 1 }; }
        var go = new GameObject("Race");
        var race = go.AddComponent<RaceManager>();
        var rig = new GameObject("cam").AddComponent<CameraRig>();
        race.Begin(cfg, rig);
        // let the AI drive the "player" car as well
        race.playerCar.ai.enabled = true;
        race.playerCar.player.enabled = false;
        if (!tt) { race.playerCar.isPlayer = false; race.playerCar.car.isPlayer = false; race.playerCar.car.assists = DriverAssists.Full; }
        int respawns = 0;
        var lastPos = race.cars.ToDictionary(c => c, c => c.car.transform.position);
        float dt = Time.fixedDeltaTime;
        int steps = 0;
        float maxSpeed = 0; RaceCar fastest = null;
        int nanCount = 0; int bottomSteps = 0;
        var offTrackSamples = new Dictionary<RaceCar, int>();
        var offHist = new SortedDictionary<int, int>();
        var wasOff = race.cars.ToDictionary(c => c, c => false);
        var pitLog = new List<string>();
        var prevPit = race.cars.ToDictionary(c => c, c => PitPhase.None);
        float limit = tt ? 300f : 60f * laps * 3f + 60f;
        while ((tt || race.state != RaceState.Finished) && Time.time < limit)
        {
            Time.time += dt; Time.unscaledTime += dt; Time.deltaTime = dt;
            if (steps % 2 == 0) Call("update");
            Call("fixed");
            foreach (var rb in Rigidbody.All) rb.Step(dt);
            steps++;
            foreach (var rc in race.cars)
            {
                var p = rc.car.transform.position;
                if (float.IsNaN(p.x) || float.IsNaN(rc.car.rpm)) nanCount++;
                // detect teleports (respawns)
                if ((p - lastPos[rc]).magnitude > 6f && !rc.InPit && prevPit[rc] == PitPhase.None) { respawns++; if (respawns < 15) Console.WriteLine($"  jump t={race.raceTime:0.0} {rc.shortName} lap={rc.lapsCompleted} d={rc.lapDistance:0} lat={rc.lateral:0.0} spd={rc.car.Speed:0.0} fin={rc.finished}"); }
                lastPos[rc] = p;
                if (rc.car.Speed > maxSpeed) { maxSpeed = rc.car.Speed; fastest = rc; }
                if (rc.car.bottoming > 0.05f) bottomSteps++;
                if (Mathf.Abs(rc.lateral) > race.track.halfWidth + 1.3f && !rc.InPit) { offTrackSamples[rc] = (offTrackSamples.TryGetValue(rc, out var n) ? n : 0) + 1; if (n % 100 == 0 && n < 400) Console.WriteLine($"  off t={race.raceTime:0.0} {rc.shortName} d={rc.lapDistance:0} lat={rc.lateral:0.0} spd={rc.car.Speed*3.6f:0} steer={rc.car.steerAngleDeg:0.0} slip={rc.car.SlipAmount:0.00} target={race.track.SpeedAt(rc.lapDistance)*3.6f:0}"); }
                bool offNow = Mathf.Abs(rc.lateral) > race.track.halfWidth + 1.3f && !rc.InPit;
                if (offNow && !wasOff[rc]) { int bin = (int)(rc.lapDistance / 50f) * 50; offHist[bin] = (offHist.TryGetValue(bin, out var hc) ? hc : 0) + 1; }
                wasOff[rc] = offNow;
                if (prevPit[rc] == PitPhase.None && rc.pit != PitPhase.None) pitLog.Add($"t={race.raceTime:0.0} {rc.shortName} pits on lap {rc.lapsCompleted + 1}: wear {rc.car.MaxTyreWear:0.00} fuel {rc.car.fuel:0.0} -> {rc.planCompound} +{rc.planFuelAdd:0}kg");
                prevPit[rc] = rc.pit;
            }
            string traceName = Environment.GetEnvironmentVariable("TRACE");
            float t0 = float.Parse(Environment.GetEnvironmentVariable("T0") ?? "0"), t1 = float.Parse(Environment.GetEnvironmentVariable("T1") ?? "0");
            float dmin = float.Parse(Environment.GetEnvironmentVariable("DMIN") ?? "-1"), dmax = float.Parse(Environment.GetEnvironmentVariable("DMAX") ?? "1e9");
            var trc = traceName != null ? race.cars.First(c => c.shortName == traceName) : null;
            if (trc != null && race.raceTime >= t0 && race.raceTime <= t1 && steps % 10 == 0 && trc.lapDistance >= dmin && trc.lapDistance <= dmax)
            {
                var rc = trc;
                var ai = rc.ai; var bf = BindingFlags.Instance | BindingFlags.NonPublic;
                float lane = (float)typeof(AIDriver).GetField("lane", bf).GetValue(ai);
                float ot = (float)typeof(AIDriver).GetField("overtakeTimer", bf).GetValue(ai);
                var car = rc.car;
                var lv = car.transform.InverseTransformDirection(car.rb.velocity);
                float beta = Mathf.Atan2(lv.x, Mathf.Abs(lv.z)) * Mathf.Rad2Deg;
                float yawRate = Vector3.Dot(car.rb.angularVelocity, car.transform.up) * Mathf.Rad2Deg;
                string wl = string.Join(" ", car.wheels.Select(w => $"{w.name}:{w.load:0}/{w.slipAngle*Mathf.Rad2Deg:0.0}/{w.surface.ToString()[0]}{(w.sliding?"*":"")}{(w.locked?"L":"")}{(w.spinning?"S":"")}"));
                Console.WriteLine($"t={race.raceTime:0.00} L{rc.lapsCompleted} box={(rc.boxRequested?1:0)} R={1f/Mathf.Max(1e-4f,Mathf.Abs(race.track.LineCurvature(race.track.IndexAt(rc.lapDistance)))):0} d={rc.lapDistance:0} lat={rc.lateral:0.0} line={race.track.LineOffsetAt(rc.lapDistance):0.0} lane={lane:0.0} ot={ot:0.0} v={car.Speed*3.6f:0} tgt={race.track.SpeedAt(rc.lapDistance)*3.6f:0} thr={car.throttleApplied:0.00} brk={car.brakeApplied:0.00} st={car.steerAngleDeg:0.0}/{car.MaxSteerNow:0.0} beta={beta:0.0} yaw={yawRate:0} wake={car.wakeDownforceFactor:0.00} g={car.gear} {wl}");
            }
            // finish when everybody is done
            if (race.checkered && race.cars.All(c => c.finished || c.retired)) race.Finish();
        }
        var td = race.track;
        Console.WriteLine($"== {td.def.name}  L={td.length:0}m  est lap {td.estimatedLapTime:0.0}s  laps={laps} grid={grid} pace={pace}  simTime={race.raceTime:0}s steps={steps}");
        Console.WriteLine($"max speed {maxSpeed * 3.6f:0} km/h ({fastest?.shortName})  NaN={nanCount}  respawn-like jumps={respawns}  fuelRateFull={race.fuelRateFull:0.000}");
        if (Environment.GetEnvironmentVariable("PITLOG") != null) foreach (var l in pitLog.Take(40)) Console.WriteLine("  PIT " + l);
        Console.WriteLine("OFFHIST " + string.Join(" ", offHist.Where(kv => kv.Value >= 2).Select(kv => $"{kv.Key}:{kv.Value}")));
        if (tt) { var p = race.playerCar; Console.WriteLine($"setup={su} CdA={p.car.setup.CdA:0.000} ClA={p.car.setup.FrontClA + p.car.setup.RearClA:0.00} vmax={maxSpeed*3.6f:0} km/h est={VehicleSpec.EstimateTopSpeed(p.car.setup, 840f)*3.6f:0}"); Console.WriteLine($"TT laps={p.lapsCompleted} times=[{string.Join(" ", p.lapTimes.Select(x => x.ToString("0.00")))}] best={p.bestLapTime:0.00} session={race.bestSessionLap:0.00} sectors=[{string.Join(" ", p.lastSectorTimes.Select(x => x.ToString("0.00")))}] ghost={race.ghost.HasGhost} ghostT={race.ghost.BestGhostTime:0.00} fuel={p.car.fuel:0.0} wear={p.car.MaxTyreWear:0.00}"); race.ghost.TryDelta(out var dl); Console.WriteLine($"delta now {dl:0.00} bottomingSteps={bottomSteps}"); return; }
        if (race.results == null) { Console.WriteLine("RACE DID NOT FINISH"); race.Finish(); }
        foreach (var r in race.results)
        {
            var rc = race.cars.First(c => c.seat == r.seat);
            string laptimes = string.Join(" ", rc.lapTimes.Select(t => t.ToString("0.0")));
            Console.WriteLine($"{r.position,2}. {rc.shortName} {GameData.Teams[r.team].name,-18} laps={r.laps} time={(r.dnf ? "DNF" : r.time.ToString("0.0"))} best={(r.bestLap < 1e6 ? r.bestLap.ToString("0.00") : "-")} pits={r.pitStops} {(rc.finished ? "" : "(proj)")} off={offTrackSamples.GetValueOrDefault(rc)} wear={rc.car.MaxTyreWear:0.00} fuel={rc.car.fuel:0.0} cmp={string.Join(",", rc.usedCompounds)} laps:[{laptimes}]");
        }
    }
}
