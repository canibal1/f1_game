using System;
using UnityEngine;

namespace F1Game
{
    public enum StrategyLevel { Off = 0, Normal = 1, High = 2 }
    public enum StartPosition { Random = 0, Back = 1, Middle = 2, Pole = 3 }

    /// <summary>Player options. Persisted by SaveSystem.</summary>
    [Serializable]
    public class GameSettings
    {
        // AI difficulty (0..100)
        public float aiPace = 60f;
        public float aiAggression = 50f;
        // assists
        public DriverAssists assists = DriverAssists.Default;
        public bool showRacingLine = true;
        // race
        public int raceLaps = 5;
        public int gridSize = 20;
        public StrategyLevel strategy = StrategyLevel.Normal;
        public StartPosition startPosition = StartPosition.Middle;
        public bool drsEnabled = true;
        // audio / video
        public float masterVolume = 0.8f;
        public float sfxVolume = 0.9f;
        public float musicVolume = 0.35f;
        public int cameraMode = 0;
        public int quality = 2; // 0 low, 1 medium, 2 high
        public string playerName = "Oyuncu";
        public int playerTeam = 0;

        public static GameSettings Current = new GameSettings();

        public static float SfxVolume => Current.masterVolume * Current.sfxVolume;
        public static float MusicVolume => Current.masterVolume * Current.musicVolume;

        public float WearFactor => strategy == StrategyLevel.Off ? 0f : strategy == StrategyLevel.High ? 1.5f : 1f;
        public float FuelFactor => strategy == StrategyLevel.Off ? 0f : strategy == StrategyLevel.High ? 1.3f : 1f;

        /// <summary>Named difficulty presets for the settings screen.</summary>
        public static readonly (string name, float pace, float aggr)[] DifficultyPresets =
        {
            ("Çaylak", 15f, 20f),
            ("Kolay", 35f, 35f),
            ("Orta", 60f, 50f),
            ("Zor", 82f, 65f),
            ("Efsane", 100f, 80f),
        };

        public string DifficultyName()
        {
            string best = "Özel";
            foreach (var p in DifficultyPresets)
                if (Mathf.Abs(p.pace - aiPace) < 0.5f && Mathf.Abs(p.aggr - aiAggression) < 0.5f) best = p.name;
            return best;
        }

        public void ApplyQuality()
        {
            QualitySettings.shadowDistance = quality == 0 ? 60f : quality == 1 ? 120f : 220f;
            QualitySettings.shadows = quality == 0 ? ShadowQuality.HardOnly : ShadowQuality.All;
            QualitySettings.shadowResolution = quality == 2 ? ShadowResolution.High : ShadowResolution.Medium;
            QualitySettings.antiAliasing = quality == 0 ? 0 : quality == 1 ? 2 : 4;
            QualitySettings.vSyncCount = 1;
            AudioListener.volume = 1f;
        }
    }
}
