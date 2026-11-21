using UnityEngine;

namespace F1Game
{
    /// <summary>Runtime handle to a built circuit (start lights, minimap).</summary>
    public class TrackWorld : MonoBehaviour
    {
        public TrackData data;
        public Material[] startLights;
        public Texture2D minimap;
        public Vector2 mapMin;
        public float mapSize = 1f;
        public Color skyColor;

        /// <summary>0..5 red lights on; -1 = all off (lights out = go).</summary>
        public void SetStartLights(int lit)
        {
            if (startLights == null) return;
            for (int i = 0; i < startLights.Length; i++)
            {
                bool on = lit > i;
                startLights[i].color = on ? new Color(1f, 0.1f, 0.08f) : new Color(0.25f, 0.02f, 0.02f);
                MaterialFactory.SetEmission(startLights[i], on ? new Color(6f, 0.3f, 0.2f) : Color.black);
            }
        }

        /// <summary>World position -> 0..1 minimap coordinates (y up).</summary>
        public Vector2 ToMap(Vector3 w)
        {
            return new Vector2((w.x - mapMin.x) / mapSize, (w.z - mapMin.y) / mapSize);
        }
    }
}
