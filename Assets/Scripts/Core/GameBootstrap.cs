using UnityEngine;

namespace F1Game
{
    /// <summary>
    /// Starts the game in any scene: press Play and everything (track, cars, UI) is generated at runtime.
    /// Scene cameras/directional lights are disabled because the game creates its own.
    /// </summary>
    public static class GameBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (GameManager.Instance != null) return;
            if (GameObject.Find("NoAutoBoot") != null) return; // escape hatch for test scenes

#if UNITY_2023_1_OR_NEWER
            var cams = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
#else
            var cams = Object.FindObjectsOfType<Camera>();
            var lights = Object.FindObjectsOfType<Light>();
            var listeners = Object.FindObjectsOfType<AudioListener>();
#endif
            foreach (var c in cams) c.gameObject.SetActive(false);
            foreach (var l in lights) if (l.type == LightType.Directional) l.gameObject.SetActive(false);
            foreach (var a in listeners) a.enabled = false;

            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
        }
    }
}
