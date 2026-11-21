using UnityEngine;

namespace F1Game
{
    /// <summary>Chase / far chase / cockpit / T-cam views, plus an orbit view for menus and the garage.</summary>
    public class CameraRig : MonoBehaviour
    {
        public enum Mode { Chase = 0, ChaseFar = 1, Cockpit = 2, TCam = 3, Orbit = 10, Free = 11 }
        public static readonly string[] ModeNames = { "Takip", "Uzak Takip", "Kokpit", "T-Kamera" };

        public Camera cam;
        public CarController target;
        public CarVisual targetVisual;
        public Mode mode = Mode.Chase;
        public Vector3 orbitCenter;
        public float orbitRadius = 9f, orbitHeight = 3f, orbitSpeed = 12f;
        float orbitAngle;
        Vector3 velocity;
        Vector3 smoothedFwd = Vector3.forward;
        float shake;
        public bool lookBack;

        public static CameraRig Create()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var c = go.AddComponent<Camera>();
            c.nearClipPlane = 0.05f;
            c.farClipPlane = 4000f;
            c.fieldOfView = 62f;
            go.AddComponent<AudioListener>();
            var rig = go.AddComponent<CameraRig>();
            rig.cam = c;
            return rig;
        }

        public void Follow(CarController car, CarVisual visual)
        {
            target = car;
            targetVisual = visual;
            if (car != null) smoothedFwd = car.transform.forward;
            if (mode >= Mode.Orbit) mode = (Mode)GameSettings.Current.cameraMode;
            Snap();
        }

        public void Orbit(Vector3 center, float radius, float height)
        {
            mode = Mode.Orbit;
            orbitCenter = center;
            orbitRadius = radius;
            orbitHeight = height;
            if (targetVisual != null) targetVisual.SetCockpitView(false);
        }

        public void Cycle()
        {
            if (mode >= Mode.Orbit) return;
            mode = (Mode)(((int)mode + 1) % 4);
            GameSettings.Current.cameraMode = (int)mode;
            Snap();
        }

        void Snap()
        {
            if (target == null) return;
            var t = target.transform;
            transform.position = t.position - t.forward * 7f + Vector3.up * 2.2f;
            transform.rotation = Quaternion.LookRotation(t.position + Vector3.up - transform.position);
            velocity = Vector3.zero;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            if (mode == Mode.Orbit)
            {
                orbitAngle += orbitSpeed * dt;
                Vector3 p = orbitCenter + Quaternion.Euler(0f, orbitAngle, 0f) * new Vector3(0f, orbitHeight, -orbitRadius);
                transform.position = p;
                transform.rotation = Quaternion.LookRotation(orbitCenter + Vector3.up * 0.6f - p);
                cam.fieldOfView = 50f;
                return;
            }
            if (mode == Mode.Free || target == null) return;

            var t = target.transform;
            bool cockpit = mode == Mode.Cockpit || mode == Mode.TCam;
            if (targetVisual != null) targetVisual.SetCockpitView(mode == Mode.Cockpit);

            float speed = target.Speed;
            if (cockpit)
            {
                Transform anchor = mode == Mode.Cockpit ? targetVisual?.cockpitAnchor : targetVisual?.tcamAnchor;
                Vector3 pos = anchor != null ? anchor.position : t.TransformPoint(new Vector3(0, 1.0f, -0.4f));
                // subtle vibration with speed and on kerbs/bottoming
                bool kerb = false;
                foreach (var w in target.wheels) if (w.surface == SurfaceType.Kerb || w.surface == SurfaceType.Grass) kerb = true;
                shake = Mathf.Lerp(shake, (kerb ? 0.012f : 0f) + target.bottoming * 0.01f + speed * 0.00005f, 0.2f);
                pos += Random.insideUnitSphere * shake * (Time.timeScale > 0 ? 1 : 0);
                Quaternion rot = t.rotation;
                // lean with g-forces like a driver's head
                rot = rot * Quaternion.Euler(Mathf.Clamp(-target.LongitudinalG * 1.2f, -5f, 5f), 0f, Mathf.Clamp(-target.LateralG * 1.5f, -6f, 6f));
                if (lookBack) rot = rot * Quaternion.Euler(0f, 180f, 0f);
                transform.SetPositionAndRotation(pos, rot);
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, mode == Mode.Cockpit ? 72f : 68f, 0.2f);
                return;
            }

            float dist = mode == Mode.Chase ? 6.2f : 9.5f;
            float height = mode == Mode.Chase ? 1.9f : 2.9f;
            Vector3 flatFwd = Vector3.ProjectOnPlane(t.forward, Vector3.up).normalized;
            Vector3 vel = target.rb != null && !target.rb.isKinematic ? target.rb.GetVelocity() : flatFwd * target.kinematicSpeed;
            Vector3 velDir = Vector3.ProjectOnPlane(vel, Vector3.up);
            // follow mostly the car heading, slightly the velocity (shows slides)
            Vector3 dir = velDir.magnitude > 5f ? Vector3.Slerp(flatFwd, velDir.normalized, 0.25f) : flatFwd;
            smoothedFwd = Vector3.Slerp(smoothedFwd, dir, 1f - Mathf.Exp(-dt * 6f));
            Vector3 back = lookBack ? -smoothedFwd : smoothedFwd;
            Vector3 desired = t.position - back * (dist + speed * 0.012f) + Vector3.up * height;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, 0.06f, Mathf.Infinity, dt);
            Vector3 look = t.position + Vector3.up * 1.0f + back * 3f;
            transform.rotation = Quaternion.LookRotation(look - transform.position);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f + Mathf.Clamp01(speed / 90f) * 12f, 0.1f);
        }
    }
}
