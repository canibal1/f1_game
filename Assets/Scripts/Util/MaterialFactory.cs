using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    /// <summary>
    /// Creates and caches materials at runtime. Everything uses the built-in "Standard" shader.
    /// The editor setup script stores template materials in Resources/Materials so the shader
    /// variants are included in player builds; in the editor Shader.Find is enough.
    /// </summary>
    public static class MaterialFactory
    {
        static Material litTemplate;
        static Material fadeTemplate;
        static Material vertexColorTemplate;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        static Material LitTemplate
        {
            get
            {
                if (litTemplate == null)
                {
                    litTemplate = Resources.Load<Material>("Materials/F1_Lit");
                    if (litTemplate == null)
                    {
                        var sh = Shader.Find("Standard");
                        if (sh == null) sh = Shader.Find("Legacy Shaders/Diffuse");
                        if (sh == null) sh = Shader.Find("Unlit/Color");
                        litTemplate = new Material(sh);
                    }
                }
                return litTemplate;
            }
        }

        public static Material Lit(Color color, float smoothness = 0.2f, float metallic = 0f, Texture tex = null, Vector2? tiling = null)
        {
            string key = $"L{ColorUtility.ToHtmlStringRGBA(color)}_{smoothness:0.00}_{metallic:0.00}_{(tex ? tex.GetInstanceID() : 0)}_{tiling}";
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(LitTemplate) { name = key, color = color };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (tex != null)
            {
                m.mainTexture = tex;
                if (tiling.HasValue) m.mainTextureScale = tiling.Value;
            }
            m.enableInstancing = true;
            cache[key] = m;
            return m;
        }

        /// <summary>Lit material with emission (start lights, brake light, kerb glow...). Not cached.</summary>
        public static Material Emissive(Color color, Color emission)
        {
            var m = new Material(LitTemplate) { color = color };
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return m;
        }

        public static void SetEmission(Material m, Color emission)
        {
            if (m != null && m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emission);
        }

        /// <summary>Semi-transparent material (ghost car in time trial).</summary>
        public static Material Fade(Color color)
        {
            if (fadeTemplate == null)
            {
                fadeTemplate = Resources.Load<Material>("Materials/F1_Fade");
                if (fadeTemplate == null)
                {
                    fadeTemplate = new Material(LitTemplate);
                    ConfigureFade(fadeTemplate);
                }
            }
            var m = new Material(fadeTemplate) { color = color };
            return m;
        }

        public static void ConfigureFade(Material m)
        {
            m.SetFloat("_Mode", 2f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.EnableKeyword("_ALPHABLEND_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = 3000;
        }

        /// <summary>Unlit material that shows mesh vertex colours (racing line, markers).</summary>
        public static Material VertexColor()
        {
            if (vertexColorTemplate == null)
            {
                var sh = Shader.Find("Sprites/Default");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                vertexColorTemplate = new Material(sh) { name = "VertexColor" };
            }
            return vertexColorTemplate;
        }
    }
}
