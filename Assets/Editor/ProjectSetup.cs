using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace F1Game.EditorTools
{
    /// <summary>
    /// One-time project setup that runs automatically when the project is opened:
    /// creates the template materials in Resources (so the Standard / Skybox shaders are included
    /// in player builds), creates Assets/Scenes/Main.unity and registers it in the build settings.
    /// The game itself is generated at runtime (see GameBootstrap), so the scene can stay empty.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Main.unity";
        const string MatDir = "Assets/Resources/Materials";

        static ProjectSetup()
        {
            EditorApplication.delayCall += () => Run(false);
        }

        [MenuItem("Monoposto GP/Projeyi Kur (sahne + materyaller)")]
        static void RunFromMenu() => Run(true);

        [MenuItem("Monoposto GP/Kayıt Dosyasını Sil")]
        static void DeleteSave()
        {
            string p = Path.Combine(Application.persistentDataPath, "monoposto_gp_save.json");
            if (File.Exists(p) && EditorUtility.DisplayDialog("Kayıt silinsin mi?", p, "Sil", "Vazgeç"))
            {
                File.Delete(p);
                Debug.Log("[Monoposto GP] Kayıt dosyası silindi: " + p);
            }
        }

        static void Run(bool verbose)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool created = EnsureMaterials();
            created |= EnsureScene();
            if (created || verbose) Debug.Log("[Monoposto GP] Proje hazır. Assets/Scenes/Main.unity sahnesini açıp Play'e basın.");
        }

        static bool EnsureMaterials()
        {
            bool created = false;
            if (!Directory.Exists(MatDir)) Directory.CreateDirectory(MatDir);

            created |= CreateMaterial(MatDir + "/F1_Lit.mat", "Standard", m =>
            {
                m.SetFloat("_Glossiness", 0.3f);
                m.enableInstancing = true;
            });
            created |= CreateMaterial(MatDir + "/F1_Fade.mat", "Standard", m =>
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
            });
            created |= CreateMaterial(MatDir + "/F1_Emissive.mat", "Standard", m =>
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.red);
            });
            created |= CreateMaterial(MatDir + "/F1_Sky.mat", "Skybox/Procedural", m =>
            {
                if (m.HasProperty("_SunSize")) m.SetFloat("_SunSize", 0.035f);
                if (m.HasProperty("_AtmosphereThickness")) m.SetFloat("_AtmosphereThickness", 1.0f);
                if (m.HasProperty("_Exposure")) m.SetFloat("_Exposure", 1.25f);
            });
            created |= CreateMaterial(MatDir + "/F1_VertexColor.mat", "Sprites/Default", null);
            if (created) AssetDatabase.SaveAssets();
            return created;
        }

        static bool CreateMaterial(string path, string shaderName, System.Action<Material> configure)
        {
            if (File.Exists(path)) return false;
            var sh = Shader.Find(shaderName);
            if (sh == null)
            {
                Debug.LogWarning($"[Monoposto GP] Shader bulunamadı: {shaderName}. (Built-in Render Pipeline gerekli.)");
                return false;
            }
            var m = new Material(sh);
            configure?.Invoke(m);
            AssetDatabase.CreateAsset(m, path);
            return true;
        }

        static bool EnsureScene()
        {
            bool created = false;
            if (!File.Exists(ScenePath))
            {
                var active = SceneManager.GetActiveScene();
                bool fresh = string.IsNullOrEmpty(active.path) && !active.isDirty;
                if (!fresh && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Monoposto GP — Play'e basın (oyun çalışma anında kurulur)");
                EditorSceneManager.SaveScene(scene, ScenePath);
                created = true;
                if (PlayerSettings.productName == "f1_game" || string.IsNullOrEmpty(PlayerSettings.productName))
                    PlayerSettings.productName = "Monoposto GP";
            }
            var scenes = EditorBuildSettings.scenes;
            bool listed = false;
            foreach (var s in scenes) if (s.path == ScenePath) listed = true;
            if (!listed)
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes);
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
                created = true;
            }
            // open the game scene when the editor starts on an empty untitled scene
            var cur = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(cur.path) && !cur.isDirty) EditorSceneManager.OpenScene(ScenePath);
            return created;
        }
    }
}
