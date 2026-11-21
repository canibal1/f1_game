// Headless stand-in for the parts of UnityEngine used by the gameplay code, including a simple
// 6-DOF rigid-body integrator and a flat-ground raycast. Used only to run the real game logic offline.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static void Destroy(Object o) { if (o is GameObject g) g.destroyed = true; }
        public static T FindObjectOfType<T>() where T : Object => null;
        public static T[] FindObjectsOfType<T>() where T : Object => new T[0];
        public static implicit operator bool(Object o) => o != null;
        public int GetInstanceID() => GetHashCode();
    }

    public class GameObject : Object
    {
        public readonly List<Component> components = new List<Component>();
        public Transform transform;
        public int layer;
        public bool activeSelf = true;
        public bool destroyed;
        public string tag;
        public static readonly List<GameObject> All = new List<GameObject>();
        public GameObject(string n = "GameObject") { name = n; transform = new Transform(); transform.gameObjectRef = this; components.Add(transform); All.Add(this); }
        public T AddComponent<T>() where T : Component, new()
        {
            var c = new T();
            c.gameObjectRef = this;
            components.Add(c);
            if (c is Rigidbody rb) rb.tr = transform;
            var awake = typeof(T).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            awake?.Invoke(c, null);
            return c;
        }
        public T GetComponent<T>() where T : class { foreach (var c in components) if (c is T t) return t; return null; }
        public T[] GetComponents<T>() where T : class { var l = new List<T>(); foreach (var c in components) if (c is T t) l.Add(t); return l.ToArray(); }
        public void SetActive(bool v) { activeSelf = v; }
        public static GameObject Find(string n) => null;
        public static GameObject CreatePrimitive(PrimitiveType t) => new GameObject(t.ToString());
    }
    public enum PrimitiveType { Sphere, Cube }

    public class Component : Object
    {
        internal GameObject gameObjectRef;
        public GameObject gameObject => gameObjectRef;
        public Transform transform => gameObjectRef.transform;
        public T GetComponent<T>() where T : class => gameObjectRef.GetComponent<T>();
        public T[] GetComponents<T>() where T : class => gameObjectRef.GetComponents<T>();
        public T[] GetComponentsInChildren<T>() where T : class => gameObjectRef.GetComponents<T>();
    }
    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { }

    public class Transform : Component
    {
        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;
        public Vector3 localScale = Vector3.one;
        public Transform parent;
        public Vector3 localPosition { get => position; set => position = value; }
        public Quaternion localRotation { get => rotation; set => rotation = value; }
        public Vector3 forward => rotation * Vector3.forward;
        public Vector3 up => rotation * Vector3.up;
        public Vector3 right => rotation * Vector3.right;
        public Vector3 TransformPoint(Vector3 p) => position + rotation * p;
        public Vector3 TransformDirection(Vector3 d) => rotation * d;
        public Vector3 InverseTransformPoint(Vector3 p) => Quaternion.Inverse(rotation) * (p - position);
        public Vector3 InverseTransformDirection(Vector3 d) => Quaternion.Inverse(rotation) * d;
        public void SetPositionAndRotation(Vector3 p, Quaternion r) { position = p; rotation = r; }
        public void SetParent(Transform p, bool worldStays = true) { parent = p; }
    }


    public enum RigidbodyInterpolation { None, Interpolate, Extrapolate }
    public enum CollisionDetectionMode { Discrete, Continuous, ContinuousDynamic, ContinuousSpeculative }
    public enum QueryTriggerInteraction { UseGlobal, Ignore, Collide }

    public class Rigidbody : Component
    {
        internal Transform tr;
        public float mass = 1f;
        public Vector3 velocity, angularVelocity;
        public Vector3 centerOfMass;
        public Vector3 inertiaTensor = Vector3.one;
        public Quaternion inertiaTensorRotation = Quaternion.identity;
        public bool isKinematic;
        public float drag, angularDrag = 0.05f;
        public RigidbodyInterpolation interpolation;
        public CollisionDetectionMode collisionDetectionMode;
        Vector3 force, torque;
        Vector3? movePos; Quaternion? moveRot;
        public Vector3 position { get => tr.position; set => tr.position = value; }
        public Quaternion rotation { get => tr.rotation; set => tr.rotation = value; }
        public Vector3 worldCenterOfMass => tr.position + tr.rotation * centerOfMass;
        public void AddForce(Vector3 f) { if (!isKinematic) force += f; }
        public void AddForceAtPosition(Vector3 f, Vector3 p)
        {
            if (isKinematic) return;
            force += f;
            torque += Vector3.Cross(p - worldCenterOfMass, f);
        }
        public Vector3 GetPointVelocity(Vector3 p) => velocity + Vector3.Cross(angularVelocity, p - worldCenterOfMass);
        public void MovePosition(Vector3 p) { movePos = p; }
        public void MoveRotation(Quaternion r) { moveRot = r; }

        public static readonly List<Rigidbody> All = new List<Rigidbody>();
        public Rigidbody() { All.Add(this); }

        public void Step(float dt)
        {
            if (isKinematic)
            {
                if (movePos.HasValue) tr.position = movePos.Value;
                if (moveRot.HasValue) tr.rotation = moveRot.Value;
                movePos = null; moveRot = null; force = torque = Vector3.zero;
                return;
            }
            movePos = null; moveRot = null;
            Vector3 com = worldCenterOfMass;
            velocity += (force / mass + new Vector3(0, -9.81f, 0)) * dt;
            velocity *= Mathf.Clamp01(1f - drag * dt);
            // world inertia
            Quaternion R = tr.rotation * inertiaTensorRotation;
            Vector3 wl = Quaternion.Inverse(R) * angularVelocity;
            Vector3 tl = Quaternion.Inverse(R) * torque;
            Vector3 Iw = new Vector3(inertiaTensor.x * wl.x, inertiaTensor.y * wl.y, inertiaTensor.z * wl.z);
            Vector3 gyro = Vector3.Cross(wl, Iw);
            Vector3 dw = new Vector3((tl.x - gyro.x) / inertiaTensor.x, (tl.y - gyro.y) / inertiaTensor.y, (tl.z - gyro.z) / inertiaTensor.z);
            wl += dw * dt;
            angularVelocity = R * wl;
            angularVelocity *= Mathf.Clamp01(1f - angularDrag * dt);
            if (angularVelocity.magnitude > 50f) angularVelocity = angularVelocity.normalized * 50f; // PhysX maxAngularVelocity-ish
            com += velocity * dt;
            Vector3 w = angularVelocity;
            Quaternion q = tr.rotation;
            Quaternion dq = new Quaternion(w.x * dt * 0.5f, w.y * dt * 0.5f, w.z * dt * 0.5f, 0f) * q;
            q = new Quaternion(q.x + dq.x, q.y + dq.y, q.z + dq.z, q.w + dq.w).normalized;
            tr.rotation = q;
            tr.position = com - q * centerOfMass;
            force = torque = Vector3.zero;
        }
    }

    public struct RaycastHit { public Vector3 point, normal; public float distance; }

    public static class Physics
    {
        public const int DefaultRaycastLayers = ~4;
        public static bool Raycast(Vector3 o, Vector3 d, out RaycastHit hit, float maxDist, int mask, QueryTriggerInteraction q)
        {
            hit = default;
            if (d.y >= -1e-6f) return false;
            float t = o.y / -d.y;
            if (t < 0f || t > maxDist) return false;
            hit.point = o + d * t; hit.normal = Vector3.up; hit.distance = t;
            return true;
        }
        public static void IgnoreCollision(Collider a, Collider b, bool v = true) { }
    }

    public class Collider : Component { public bool isTrigger; public PhysicMaterial sharedMaterial; }
    public class BoxCollider : Collider { public Vector3 center, size; }
    public enum PhysicMaterialCombine { Average, Minimum, Multiply, Maximum }
    public class PhysicMaterial : Object
    {
        public PhysicMaterial(string n) { name = n; }
        public float dynamicFriction, staticFriction, bounciness;
        public PhysicMaterialCombine frictionCombine, bounceCombine;
    }
    public class Collision { public Vector3 relativeVelocity; }
    public class AudioClip : Object { }
    public class AudioSource : Behaviour { public float spatialBlend; public void PlayOneShot(AudioClip c, float v) { } }
    public static class Resources { public static T Load<T>(string p) where T : class => null; }
    public static class AudioListener { public static float volume; public static bool pause; }

    public static class Time
    {
        public static float fixedDeltaTime = 0.01f, deltaTime = 0.01f, time, unscaledTime, unscaledDeltaTime = 0.01f, timeScale = 1f;
    }

    public static class Random
    {
        static System.Random r = new System.Random(1234);
        public static void Seed(int s) { r = new System.Random(s); }
        public static float value => (float)r.NextDouble();
        public static float Range(float a, float b) => a + (float)r.NextDouble() * (b - a);
        public static int Range(int a, int b) => r.Next(a, b);
        public static Vector3 insideUnitSphere => new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)) * 0.577f;
    }

    public enum ShadowQuality { Disable, HardOnly, All }
    public enum ShadowResolution { Low, Medium, High, VeryHigh }
    public static class QualitySettings
    {
        public static float shadowDistance; public static ShadowQuality shadows; public static ShadowResolution shadowResolution;
        public static int antiAliasing, vSyncCount;
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        public Quaternion normalized { get { float m = (float)Math.Sqrt(x * x + y * y + z * z + w * w); return new Quaternion(x / m, y / m, z / m, w / m); } }
        public static Quaternion operator *(Quaternion a, Quaternion b) => new Quaternion(
            a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
            a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
            a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
            a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            Vector3 u = new Vector3(q.x, q.y, q.z);
            Vector3 t = 2f * Vector3.Cross(u, v);
            return v + q.w * t + Vector3.Cross(u, t);
        }
        public static Quaternion Inverse(Quaternion q) => new Quaternion(-q.x, -q.y, -q.z, q.w);
        public static Quaternion AngleAxis(float deg, Vector3 axis)
        {
            axis = axis.normalized; float h = deg * Mathf.Deg2Rad * 0.5f; float s = (float)Math.Sin(h);
            return new Quaternion(axis.x * s, axis.y * s, axis.z * s, (float)Math.Cos(h));
        }
        public static Quaternion Euler(float ex, float ey, float ez) =>
            AngleAxis(ey, Vector3.up) * AngleAxis(ex, Vector3.right) * AngleAxis(ez, Vector3.forward);
        public static Quaternion LookRotation(Vector3 f, Vector3 up)
        {
            Vector3 zf = f.normalized;
            Vector3 xr = Vector3.Cross(up, zf).normalized;
            Vector3 yu = Vector3.Cross(zf, xr);
            float m00 = xr.x, m01 = yu.x, m02 = zf.x, m10 = xr.y, m11 = yu.y, m12 = zf.y, m20 = xr.z, m21 = yu.z, m22 = zf.z;
            float tr = m00 + m11 + m22;
            Quaternion q;
            if (tr > 0) { float s = (float)Math.Sqrt(tr + 1.0) * 2; q = new Quaternion((m21 - m12) / s, (m02 - m20) / s, (m10 - m01) / s, 0.25f * s); }
            else if (m00 > m11 && m00 > m22) { float s = (float)Math.Sqrt(1.0 + m00 - m11 - m22) * 2; q = new Quaternion(0.25f * s, (m01 + m10) / s, (m02 + m20) / s, (m21 - m12) / s); }
            else if (m11 > m22) { float s = (float)Math.Sqrt(1.0 + m11 - m00 - m22) * 2; q = new Quaternion((m01 + m10) / s, 0.25f * s, (m12 + m21) / s, (m02 - m20) / s); }
            else { float s = (float)Math.Sqrt(1.0 + m22 - m00 - m11) * 2; q = new Quaternion((m02 + m20) / s, (m12 + m21) / s, 0.25f * s, (m10 - m01) / s); }
            return q.normalized;
        }
        public static Quaternion LookRotation(Vector3 f) => LookRotation(f, Vector3.up);
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            float d = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
            if (d < 0) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); d = -d; }
            return new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t).normalized;
        }
        public static Quaternion FromToRotation(Vector3 a, Vector3 b)
        {
            Vector3 c = Vector3.Cross(a.normalized, b.normalized); float d = Vector3.Dot(a.normalized, b.normalized);
            return new Quaternion(c.x, c.y, c.z, 1f + d).normalized;
        }
    }

    public class Debug { public static void Log(object o) => Console.WriteLine(o); public static void LogWarning(object o) => Console.WriteLine("WARN " + o); public static void LogError(object o) => Console.WriteLine("ERR " + o); }
    public class AnimationCurve { }
    public class SerializeField : Attribute { }
    [AttributeUsage(AttributeTargets.Class)] public class RequireComponent : Attribute { public RequireComponent(Type t) { } }
}
