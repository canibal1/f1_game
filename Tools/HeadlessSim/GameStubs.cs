// Stand-ins for presentation-only game classes (visuals, audio, UI) so the race logic compiles headless.
using UnityEngine;
namespace F1Game
{
    public class CarVisual : MonoBehaviour { public void Build(CarController c, Color a, Color b, bool ghost = false) { } public void SetCockpitView(bool v) { } }
    public class CarAudio : MonoBehaviour { public void Init(CarController c, bool p) { } }
    public class RacingLineDisplay : MonoBehaviour { public void Init(RaceManager r, RaceCar rc) { } }
    public class TrackWorld : MonoBehaviour { public TrackData data; public void SetStartLights(int n) { } }
    public class CameraRig : MonoBehaviour { public bool lookBack; public void Follow(CarController c, CarVisual v) { } public void Cycle() { } }
    public class GameManager : MonoBehaviour { public static GameManager Instance; public void OnPlayerSetupChangedInPit(CarSetup s) { } }
    public static class SaveSystem { public static void RecordBestLap(string id, float t) { } }
    public static class SynthAudio { public static AudioClip BeepLow, BeepHigh, PitBeep; }
    public static class TrackBuilder
    {
        public static TrackWorld Build(TrackData td, Transform parent) { var g = new GameObject("track"); var w = g.AddComponent<TrackWorld>(); w.data = td; return w; }
        public static Vector3 GridSlot(TrackData td, int slot, out Quaternion rot)
        {
            float d = -(12f + slot * 8f);
            float side = (slot % 2 == 0) ? -2.7f : 2.7f;
            Vector3 p = td.PositionAt(d) + td.RightAt(d) * side;
            rot = Quaternion.LookRotation(td.TangentAt(d));
            return p;
        }
    }
}
