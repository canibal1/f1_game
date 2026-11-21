using UnityEngine;

namespace F1Game
{
    /// <summary>Reads keyboard/gamepad input and drives the player's car (plus optional braking assist).</summary>
    public class PlayerDriver : MonoBehaviour
    {
        RaceManager race;
        RaceCar me;
        CarController car;
        float throttle, brake;
        public bool inputBlocked; // while menus are open

        public void Init(RaceManager r, RaceCar rc)
        {
            race = r;
            me = rc;
            car = rc.car;
        }

        void Update()
        {
            if (race == null || car == null) return;
            if (Time.timeScale == 0f || inputBlocked) return;

            float steer = GameInput.SteerAxis;
            float tIn = GameInput.ThrottleAxis, bIn = GameInput.BrakeAxis;
            float dt = Time.deltaTime;
            // keyboard: ramp pedals so tapping works; analog triggers pass straight through
            throttle = tIn >= 0.999f || tIn <= 0.001f ? Mathf.MoveTowards(throttle, tIn, (tIn > throttle ? 5f : 9f) * dt) : tIn;
            brake = bIn >= 0.999f || bIn <= 0.001f ? Mathf.MoveTowards(brake, bIn, (bIn > brake ? 7f : 10f) * dt) : bIn;

            float thr = throttle, brk = brake;
            if (GameSettings.Current.assists.brakingAssist && !car.frozen && car.gear >= 0)
            {
                float v = car.Speed;
                float vt = race.track.SpeedAt(me.lapDistance + Mathf.Max(5f, v * 0.3f)) * 0.98f;
                if (v > vt + 1.5f)
                {
                    brk = Mathf.Max(brk, Mathf.Clamp01((v - vt) * 0.12f));
                    thr = Mathf.Min(thr, 0.1f);
                }
            }

            car.input = new CarController.Inputs { steer = steer, throttle = thr, brake = brk };

            if (GameInput.Down(GameAction.ShiftUp)) car.RequestShift(1);
            if (GameInput.Down(GameAction.ShiftDown)) car.RequestShift(-1);
            if (GameInput.Down(GameAction.DRS) && car.drsAvailable) car.drsOpen = !car.drsOpen;
            if (GameInput.Down(GameAction.BoxRequest) && race.IsRace)
            {
                me.boxRequested = !me.boxRequested;
                race.ShowEvent(me.boxRequested ? "PİT İSTEĞİ: bu tur kutuya gir (BOX BOX)" : "Pit isteği iptal edildi", 2.5f);
            }
            if (GameInput.Down(GameAction.ResetCar) && !me.InPit && race.state == RaceState.Running) race.Respawn(me);
            if (race.cameraRig != null)
            {
                if (GameInput.Down(GameAction.Camera)) race.cameraRig.Cycle();
                race.cameraRig.lookBack = GameInput.Held(GameAction.LookBack);
            }
        }
    }
}
