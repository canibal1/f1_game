using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    /// <summary>
    /// Builds the visible car: the CC0 Kenney "race" model (stretched to single-seater proportions)
    /// with team livery colours, compound-coloured tyre sidewalls, a halo, helmet and steering wheel
    /// for the cockpit view, rear rain light and tyre-smoke / spark effects.
    /// Falls back to a procedural body when the model file is missing.
    /// </summary>
    public class CarVisual : MonoBehaviour
    {
        public static readonly Vector3 BodyScale = new Vector3(1.5f, 1.3f, 2.1f);
        public const float WheelScale = VehicleSpec.WheelRadius / 0.3f;

        public CarController car;
        public Color primary = Color.red, secondary = Color.white;
        public Transform[] wheelPivots = new Transform[4];
        public Transform[] steerPivots = new Transform[4];
        public Transform[] spinPivots = new Transform[4];
        public Transform steeringWheel;
        public Transform cockpitAnchor;
        public Transform tcamAnchor;
        public Renderer[] bodyRenderers;
        Renderer[] helmetRenderers;
        Material rainLightMat;
        readonly List<(Renderer r, int sub)> sidewallSlots = new List<(Renderer, int)>();
        TyreCompound shownCompound = (TyreCompound)(-1);
        ParticleSystem smoke, sparks;
        bool ghost;
        float blink;

        public void Build(CarController c, Color teamPrimary, Color teamSecondary, bool isGhost = false)
        {
            car = c;
            primary = teamPrimary;
            secondary = teamSecondary;
            ghost = isGhost;

            var pack = MeshPack.Load("race_car");
            var bodyRoot = new GameObject("BodyRoot").transform;
            bodyRoot.SetParent(transform, false);
            bodyRoot.localScale = BodyScale;

            Color carbon = new Color(0.10f, 0.10f, 0.12f);
            if (pack != null)
            {
                var body = pack.FindPart("body");
                if (body != null)
                {
                    var go = new GameObject("Body");
                    go.transform.SetParent(bodyRoot, false);
                    go.transform.localPosition = body.position;
                    go.transform.localRotation = body.rotation;
                    go.transform.localScale = body.scale;
                    go.AddComponent<MeshFilter>().sharedMesh = body.mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    var mats = new Material[body.colors.Length];
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Color src = body.colors[i];
                        Color dst;
                        if (src.r > src.g + 0.2f && src.r > src.b + 0.2f) dst = primary;          // main livery
                        else if (src.r > 0.8f && src.g > 0.8f && src.b > 0.8f) dst = secondary;  // accents
                        else dst = carbon;                                                       // wings, floor
                        mats[i] = ghost ? MaterialFactory.Fade(new Color(dst.r, dst.g, dst.b, 0.35f)) : MaterialFactory.Lit(dst, dst == carbon ? 0.45f : 0.65f, dst == carbon ? 0.1f : 0.05f);
                    }
                    mr.sharedMaterials = mats;
                }
            }
            else
            {
                BuildFallbackBody(bodyRoot, carbon);
            }

            // wheels
            string[] partNames = { "wheel-front-left", "wheel-front-right", "wheel-back-left", "wheel-back-right" };
            for (int i = 0; i < 4; i++)
            {
                var pivot = new GameObject("Wheel" + i).transform;
                pivot.SetParent(transform, false);
                pivot.localPosition = car.WheelCenterLocal(i);
                wheelPivots[i] = pivot;
                var steer = new GameObject("Steer").transform;
                steer.SetParent(pivot, false);
                steerPivots[i] = steer;
                var spin = new GameObject("Spin").transform;
                spin.SetParent(steer, false);
                spinPivots[i] = spin;

                var part = pack != null ? pack.FindPart(partNames[i]) : null;
                if (part != null)
                {
                    var go = new GameObject("Tyre");
                    go.transform.SetParent(spin, false);
                    go.transform.localRotation = part.rotation;
                    go.transform.localScale = Vector3.Scale(part.scale * WheelScale, new Vector3(i >= 2 ? 1.3f : 1.08f, 1f, 1f));
                    go.AddComponent<MeshFilter>().sharedMesh = part.mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    var mats = new Material[part.colors.Length];
                    for (int k = 0; k < mats.Length; k++)
                    {
                        Color src = part.colors[k];
                        bool sidewall = src.r > 0.9f && src.g > 0.5f && src.b < 0.45f; // orange rim stripe in the source palette
                        if (sidewall) sidewallSlots.Add((mr, k));
                        Color dst = sidewall ? Color.white : (src.b > src.r + 0.05f ? new Color(0.22f, 0.22f, 0.25f) : new Color(0.07f, 0.07f, 0.08f));
                        mats[k] = ghost ? MaterialFactory.Fade(new Color(dst.r, dst.g, dst.b, 0.35f)) : MaterialFactory.Lit(dst, 0.15f);
                    }
                    mr.sharedMaterials = mats;
                }
                else
                {
                    var mb = new MeshBuilder();
                    mb.AddCylinderX(Vector3.zero, VehicleSpec.WheelRadius, i < 2 ? 0.32f : 0.4f, 20, Quaternion.identity);
                    var go = new GameObject("Tyre");
                    go.transform.SetParent(spin, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("tyre");
                    go.AddComponent<MeshRenderer>().sharedMaterial = MaterialFactory.Lit(new Color(0.08f, 0.08f, 0.09f), 0.1f);
                }
            }

            if (!ghost) BuildCockpit(carbon);
            bodyRenderers = GetComponentsInChildren<Renderer>();
            if (!ghost) BuildEffects();
            UpdateCompoundColour(true);
        }

        void BuildFallbackBody(Transform root, Color carbon)
        {
            // simple procedural monocoque (used only if the CC0 model is missing)
            var mb = new MeshBuilder(2);
            mb.Sub(0);
            mb.AddBox(new Vector3(0, 0.35f, 0.2f), new Vector3(0.55f, 0.32f, 1.9f), Quaternion.identity);
            mb.AddBox(new Vector3(0, 0.38f, -0.6f), new Vector3(0.95f, 0.38f, 0.9f), Quaternion.identity);
            mb.AddBox(new Vector3(0, 0.55f, -0.9f), new Vector3(0.3f, 0.35f, 0.5f), Quaternion.identity);
            mb.Sub(1);
            mb.AddBox(new Vector3(0, 0.12f, 1.25f), new Vector3(1.2f, 0.06f, 0.25f), Quaternion.identity);
            mb.AddBox(new Vector3(0, 0.6f, -1.25f), new Vector3(0.75f, 0.06f, 0.2f), Quaternion.identity);
            mb.AddBox(new Vector3(0, 0.1f, -0.2f), new Vector3(0.9f, 0.04f, 2.2f), Quaternion.identity);
            var go = new GameObject("Body");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("fallbackBody");
            go.AddComponent<MeshRenderer>().sharedMaterials = new[] { MaterialFactory.Lit(primary, 0.6f), MaterialFactory.Lit(carbon, 0.4f) };
        }

        void BuildCockpit(Color carbon)
        {
            // halo: tube arc around the cockpit opening + centre pillar
            var halo = new MeshBuilder();
            Vector3 c = new Vector3(0f, 0.97f, -0.2f);
            const float rx = 0.36f, rz = 0.44f;
            int seg = 16;
            Vector3 prev = Vector3.zero;
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(-100f, 100f, i / (float)seg) * Mathf.Deg2Rad; // front half arc (+z)
                Vector3 p = c + new Vector3(Mathf.Sin(a) * rx, -Mathf.Abs(Mathf.Sin(a)) * 0.08f, Mathf.Cos(a) * rz);
                if (i > 0) Tube(halo, prev, p, 0.028f);
                prev = p;
            }
            // side mounts down to the bodywork
            Tube(halo, c + new Vector3(-rx * 0.985f, -0.08f, -rz * 0.17f), new Vector3(-0.38f, 0.74f, -0.55f), 0.03f);
            Tube(halo, c + new Vector3(rx * 0.985f, -0.08f, -rz * 0.17f), new Vector3(0.38f, 0.74f, -0.55f), 0.03f);
            Tube(halo, c + new Vector3(0, 0, rz), new Vector3(0f, 0.74f, 0.40f), 0.03f); // centre pillar
            var hg = new GameObject("Halo");
            hg.transform.SetParent(transform, false);
            hg.AddComponent<MeshFilter>().sharedMesh = halo.ToMesh("halo");
            hg.AddComponent<MeshRenderer>().sharedMaterial = MaterialFactory.Lit(new Color(0.12f, 0.12f, 0.13f), 0.5f, 0.3f);

            // helmet
            var helmet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(helmet.GetComponent<Collider>());
            helmet.name = "Helmet";
            helmet.transform.SetParent(transform, false);
            helmet.transform.localPosition = new Vector3(0f, 0.86f, -0.32f);
            helmetRenderers = new Renderer[] { helmet.GetComponent<Renderer>(), null };
            helmet.transform.localScale = new Vector3(0.27f, 0.27f, 0.30f);
            helmet.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Lit(secondary, 0.8f, 0.1f);
            var visor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(visor.GetComponent<Collider>());
            visor.name = "Visor";
            visor.transform.SetParent(helmet.transform, false);
            visor.transform.localPosition = new Vector3(0f, 0.08f, 0.38f);
            visor.transform.localScale = new Vector3(0.75f, 0.22f, 0.3f);
            visor.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Lit(new Color(0.05f, 0.05f, 0.07f), 0.95f, 0.5f);
            helmetRenderers[1] = visor.GetComponent<Renderer>();

            // steering wheel with a small display
            var sw = new MeshBuilder(2);
            sw.Sub(0);
            sw.AddBox(new Vector3(0, 0, 0), new Vector3(0.28f, 0.13f, 0.03f), Quaternion.identity);
            sw.AddBox(new Vector3(-0.15f, 0.0f, 0), new Vector3(0.05f, 0.16f, 0.05f), Quaternion.identity);
            sw.AddBox(new Vector3(0.15f, 0.0f, 0), new Vector3(0.05f, 0.16f, 0.05f), Quaternion.identity);
            sw.Sub(1);
            sw.AddBox(new Vector3(0, 0.02f, -0.018f), new Vector3(0.12f, 0.06f, 0.01f), Quaternion.identity);
            var swg = new GameObject("SteeringWheel");
            swg.transform.SetParent(transform, false);
            swg.transform.localPosition = new Vector3(0f, 0.80f, -0.08f);
            swg.transform.localRotation = Quaternion.Euler(-20f, 0f, 0f);
            swg.AddComponent<MeshFilter>().sharedMesh = sw.ToMesh("steeringWheel");
            swg.AddComponent<MeshRenderer>().sharedMaterials = new[]
            {
                MaterialFactory.Lit(new Color(0.08f, 0.08f, 0.09f), 0.3f),
                MaterialFactory.Emissive(new Color(0.1f, 0.4f, 0.2f), new Color(0.1f, 0.8f, 0.3f))
            };
            steeringWheel = swg.transform;

            cockpitAnchor = new GameObject("CockpitCam").transform;
            cockpitAnchor.SetParent(transform, false);
            cockpitAnchor.localPosition = new Vector3(0f, 0.95f, -0.36f);

            tcamAnchor = new GameObject("TCam").transform;
            tcamAnchor.SetParent(transform, false);
            tcamAnchor.localPosition = new Vector3(0f, 1.18f, -0.95f);

            // rear rain light (blinks in the pit lane / when the limiter is on)
            var rl = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(rl.GetComponent<Collider>());
            rl.name = "RainLight";
            rl.transform.SetParent(transform, false);
            rl.transform.localPosition = new Vector3(0f, 0.42f, -2.70f);
            rl.transform.localScale = new Vector3(0.12f, 0.06f, 0.03f);
            rainLightMat = MaterialFactory.Emissive(new Color(0.3f, 0.02f, 0.02f), Color.black);
            rl.GetComponent<Renderer>().sharedMaterial = rainLightMat;
        }

        static void Tube(MeshBuilder mb, Vector3 a, Vector3 b, float r)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-6f) return;
            Quaternion rot = Quaternion.FromToRotation(Vector3.right, d.normalized);
            mb.AddCylinderX((a + b) * 0.5f, r, d.magnitude + r, 8, rot, false);
        }

        void BuildEffects()
        {
            var mat = new Material(MaterialFactory.VertexColor()) { mainTexture = ProceduralTextures.Dot() };
            smoke = MakeParticles("TyreSmoke", mat, 1.2f, new Color(0.85f, 0.85f, 0.85f, 0.45f), 1.4f, 2.5f);
            sparks = MakeParticles("Sparks", mat, 0.25f, new Color(1f, 0.75f, 0.3f, 1f), 0.12f, 0.2f);
        }

        ParticleSystem MakeParticles(string n, Material mat, float life, Color col, float size0, float size1)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = life;
            main.startSpeed = 0.5f;
            main.startSize = size0;
            main.startColor = col;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var em = ps.emission;
            em.enabled = false;
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, size1 / Mathf.Max(0.01f, size0)));
            var col2 = ps.colorOverLifetime;
            col2.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(col, 0), new GradientColorKey(col, 1) }, new[] { new GradientAlphaKey(col.a, 0), new GradientAlphaKey(0, 1) });
            col2.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        public void UpdateCompoundColour(bool force = false)
        {
            if (car == null || (!force && shownCompound == car.compound)) return;
            shownCompound = car.compound;
            Color c = Tyres.Color(car.compound);
            var m = ghost ? MaterialFactory.Fade(new Color(c.r, c.g, c.b, 0.35f)) : MaterialFactory.Lit(c, 0.2f);
            foreach (var (r, sub) in sidewallSlots)
            {
                var mats = r.sharedMaterials;
                mats[sub] = m;
                r.sharedMaterials = mats;
            }
        }

        /// <summary>Hides the driver's own helmet when the camera sits inside it.</summary>
        public void SetCockpitView(bool cockpit)
        {
            if (helmetRenderers == null) return;
            foreach (var r in helmetRenderers) if (r != null) r.enabled = !cockpit;
        }

        public void SetVisible(bool v)
        {
            if (bodyRenderers == null) return;
            foreach (var r in bodyRenderers) if (r != null) r.enabled = v;
        }

        void LateUpdate()
        {
            if (car == null || car.wheels == null) return;
            for (int i = 0; i < 4; i++)
            {
                wheelPivots[i].localPosition = car.WheelCenterLocal(i);
                steerPivots[i].localRotation = i < 2 ? Quaternion.Euler(0f, car.steerAngleDeg, 0f) : Quaternion.identity;
                spinPivots[i].localRotation = Quaternion.Euler(car.wheels[i].spinAngle % 360f, 0f, 0f);
            }
            if (steeringWheel != null)
                steeringWheel.localRotation = Quaternion.Euler(-20f, 0f, 0f) * Quaternion.Euler(0f, 0f, -car.steerAngleDeg * 6f);
            UpdateCompoundColour();

            if (rainLightMat != null)
            {
                bool on = car.IsKinematic;
                blink += Time.deltaTime;
                MaterialFactory.SetEmission(rainLightMat, on && (blink % 0.5f) < 0.25f ? new Color(4f, 0.2f, 0.2f) : Color.black);
            }

            if (smoke != null && Time.timeScale > 0f)
            {
                for (int i = 0; i < 4; i++)
                {
                    var w = car.wheels[i];
                    if (!w.grounded) continue;
                    bool smoking = (w.locked || w.spinning || (w.sliding && car.Speed > 12f));
                    if (smoking && Random.value < 0.6f)
                    {
                        var ep = new ParticleSystem.EmitParams
                        {
                            position = w.contactPoint + Vector3.up * 0.2f,
                            velocity = Vector3.up * 0.6f + Random.insideUnitSphere * 0.5f
                        };
                        smoke.Emit(ep, 1);
                    }
                    if (w.surface == SurfaceType.Grass || w.surface == SurfaceType.Gravel)
                    {
                        if (car.Speed > 8f && Random.value < 0.3f)
                        {
                            var ep = new ParticleSystem.EmitParams
                            {
                                position = w.contactPoint + Vector3.up * 0.1f,
                                velocity = Vector3.up * 1.2f + Random.insideUnitSphere,
                                startColor = w.surface == SurfaceType.Gravel ? new Color(0.75f, 0.68f, 0.5f, 0.6f) : new Color(0.45f, 0.38f, 0.25f, 0.5f)
                            };
                            smoke.Emit(ep, 1);
                        }
                    }
                }
                if (car.bottoming > 0.1f && car.Speed > 20f)
                {
                    int n = Mathf.CeilToInt(car.bottoming * 4f);
                    for (int k = 0; k < n; k++)
                    {
                        var ep = new ParticleSystem.EmitParams
                        {
                            position = transform.TransformPoint(new Vector3(Random.Range(-0.4f, 0.4f), 0.05f, Random.Range(-1.5f, 0.5f))),
                            velocity = -transform.forward * Random.Range(8f, 20f) + Vector3.up * Random.Range(1f, 4f) + Random.insideUnitSphere * 2f
                        };
                        sparks.Emit(ep, 1);
                    }
                }
            }
        }
    }
}
