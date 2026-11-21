using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace F1Game
{
    [Serializable]
    public class SetupEntry { public string trackId; public CarSetup setup; }

    [Serializable]
    public class BestLapEntry { public string trackId; public float time; }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public GameSettings settings = new GameSettings();
        public List<SetupEntry> setups = new List<SetupEntry>();
        public List<BestLapEntry> bestLaps = new List<BestLapEntry>();
        public bool hasChampionship;
        public ChampionshipState championship = new ChampionshipState();
        public List<string> trophies = new List<string>();
    }

    /// <summary>JSON save file in Application.persistentDataPath (settings, setups per track, best laps, championship).</summary>
    public static class SaveSystem
    {
        public static SaveData Data { get; private set; } = new SaveData();
        static string PathFile => Path.Combine(Application.persistentDataPath, "monoposto_gp_save.json");

        public static void Load()
        {
            try
            {
                if (File.Exists(PathFile))
                {
                    var json = File.ReadAllText(PathFile);
                    var d = JsonUtility.FromJson<SaveData>(json);
                    if (d != null) Data = d;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] could not read save file: " + e.Message);
                Data = new SaveData();
            }
            if (Data.settings == null) Data.settings = new GameSettings();
            if (Data.setups == null) Data.setups = new List<SetupEntry>();
            if (Data.bestLaps == null) Data.bestLaps = new List<BestLapEntry>();
            if (Data.championship == null) Data.championship = new ChampionshipState();
            if (Data.trophies == null) Data.trophies = new List<string>();
            GameSettings.Current = Data.settings;
        }

        public static void Save()
        {
            try
            {
                Data.settings = GameSettings.Current;
                File.WriteAllText(PathFile, JsonUtility.ToJson(Data, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Save] could not write save file: " + e.Message);
            }
        }

        public static CarSetup GetSetup(string trackId)
        {
            foreach (var e in Data.setups)
                if (e.trackId == trackId && e.setup != null) return e.setup.Clone();
            var def = GameData.Tracks[GameData.TrackIndex(trackId)];
            return CarSetup.Preset(def.recommended);
        }

        public static void SetSetup(string trackId, CarSetup s)
        {
            s = s.Clone();
            s.Clamp();
            foreach (var e in Data.setups)
                if (e.trackId == trackId) { e.setup = s; Save(); return; }
            Data.setups.Add(new SetupEntry { trackId = trackId, setup = s });
            Save();
        }

        public static float BestLap(string trackId)
        {
            foreach (var e in Data.bestLaps) if (e.trackId == trackId) return e.time;
            return float.MaxValue;
        }

        public static void RecordBestLap(string trackId, float t)
        {
            foreach (var e in Data.bestLaps)
                if (e.trackId == trackId)
                {
                    if (t < e.time) { e.time = t; Save(); }
                    return;
                }
            Data.bestLaps.Add(new BestLapEntry { trackId = trackId, time = t });
            Save();
        }
    }
}
