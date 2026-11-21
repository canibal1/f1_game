using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    public enum GameState
    {
        MainMenu, QuickRaceSetup, TimeTrialSelect, ChampionshipSetup, ChampionshipHub, PreRace,
        Garage, Settings, Racing, Paused, Results, Standings, SeasonEnd, Help
    }

    public enum SessionKind { QuickRace, Championship, TimeTrial }

    /// <summary>
    /// Top-level flow: menus, showroom backdrop, starting/ending sessions, pause, results and the
    /// championship loop. Created automatically by GameBootstrap – no scene setup required.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState state = GameState.MainMenu;
        public GameState returnState = GameState.MainMenu;
        public SessionKind session = SessionKind.QuickRace;
        public CameraRig rig;
        public RaceManager race;
        public int selectedTrack;
        public CarSetup editingSetup;
        public CarSetup setupBeforeEdit;
        public TyreCompound startCompound = TyreCompound.Medium;
        public float startFuel = 60f;
        public bool pitMenuOpen;
        public bool resultsApplied;
        public float finishedAt = -1f;
        public List<ResultRow> lastResults;
        public int lastRoundTrack;

        GameObject showroom;
        int showroomTrack = -1;
        int showroomTeam = -1;
        Transform showroomCar;
        AudioSource music, ui;
        AudioClip clickClip, confirmClip;

        // ------------------------------------------------------------------ lifecycle
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Time.fixedDeltaTime = 0.01f; // 100 Hz physics for stable stiff suspension at 300+ km/h
            Application.targetFrameRate = 120;
            SaveSystem.Load();
            GameSettings.Current.ApplyQuality();

            rig = CameraRig.Create();
            DontDestroyOnLoad(rig.gameObject);
            gameObject.AddComponent<GameUI>();

            music = gameObject.AddComponent<AudioSource>();
            music.clip = Resources.Load<AudioClip>("FreeAssets/Audio/menu_music");
            music.loop = true;
            music.spatialBlend = 0f;
            music.ignoreListenerPause = true;
            ui = gameObject.AddComponent<AudioSource>();
            ui.spatialBlend = 0f;
            ui.ignoreListenerPause = true;
            clickClip = Resources.Load<AudioClip>("FreeAssets/Audio/ui");
            confirmClip = Resources.Load<AudioClip>("FreeAssets/Audio/confirm");

            selectedTrack = SaveSystem.Data.hasChampionship && Championship.State.IsActive ? Championship.State.CurrentTrack : 0;
            GoTo(GameState.MainMenu);
        }

        void Update()
        {
            if (music != null)
            {
                bool menus = race == null || state == GameState.Results || state == GameState.Standings || state == GameState.SeasonEnd;
                float want = menus ? GameSettings.MusicVolume : 0f;
                music.volume = Mathf.MoveTowards(music.volume, want, Time.unscaledDeltaTime * 0.5f);
                if (want > 0f && !music.isPlaying && music.clip != null) music.Play();
                if (music.volume <= 0.001f && want <= 0f && music.isPlaying) music.Pause();
            }

            if (race != null)
            {
                if ((state == GameState.Racing || state == GameState.Paused) && GameInput.Down(GameAction.Pause))
                {
                    if (state == GameState.Racing) Pause();
                    else Resume();
                }
                if (state == GameState.Racing && GameInput.Down(GameAction.PitMenu) && race.IsRace)
                {
                    pitMenuOpen = !pitMenuOpen;
                    if (pitMenuOpen) PreparePitPlan();
                }
                if (race.state == RaceState.Finished && state == GameState.Racing)
                {
                    if (finishedAt < 0f) finishedAt = Time.unscaledTime;
                    if (Time.unscaledTime - finishedAt > 3.5f) ShowResults();
                }
                Cursor.visible = state != GameState.Racing || pitMenuOpen;
            }
            else Cursor.visible = true;
        }

        public void Click() { if (clickClip != null) ui.PlayOneShot(clickClip, GameSettings.SfxVolume * 0.8f); }
        public void Confirm() { if (confirmClip != null) ui.PlayOneShot(confirmClip, GameSettings.SfxVolume); }

        public void GoTo(GameState s)
        {
            state = s;
            switch (s)
            {
                case GameState.MainMenu:
                case GameState.QuickRaceSetup:
                case GameState.TimeTrialSelect:
                case GameState.ChampionshipSetup:
                case GameState.ChampionshipHub:
                case GameState.PreRace:
                case GameState.Standings:
                case GameState.Help:
                    if (race == null) ShowShowroom(selectedTrack, false);
                    break;
                case GameState.SeasonEnd:
                    ShowShowroom(selectedTrack, true);
                    break;
                case GameState.Garage:
                    if (race == null) ShowShowroom(selectedTrack, false, true);
                    break;
            }
        }

        // ------------------------------------------------------------------ showroom backdrop
        public void ShowShowroom(int trackIndex, bool trophy, bool close = false)
        {
            if (race != null) return;
            int teamNow = SessionTeam();
            if (showroom == null || showroomTrack != trackIndex || showroomTeam != teamNow)
            {
                if (showroom != null) Destroy(showroom);
                showroom = new GameObject("Showroom");
                showroomTrack = trackIndex;
                showroomTeam = teamNow;
                var td = TrackData.Build(GameData.Tracks[trackIndex]);
                var world = TrackBuilder.Build(td, showroom.transform);
                world.SetStartLights(0);
                int team = SessionTeam();
                // display car parked in front of its team's garage
                float d = td.BoxDistance(team);
                Vector3 pos = td.PositionAt(d) + td.RightAt(d) * td.BoxLaneOffset;
                var carGo = new GameObject("DisplayCar");
                carGo.transform.SetParent(showroom.transform, false);
                carGo.transform.position = pos;
                carGo.transform.rotation = Quaternion.LookRotation(td.TangentAt(d));
                var rb = carGo.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                var cc = carGo.AddComponent<CarController>();
                cc.enabled = false;
                cc.ApplySetup(SaveSystem.GetSetup(GameData.Tracks[trackIndex].id));
                var vis = carGo.AddComponent<CarVisual>();
                vis.Build(cc, GameData.Teams[team].primary, GameData.Teams[team].secondary);
                showroomCar = carGo.transform;
                showroomTrophy = null;
            }
            if (trophy && showroomTrophy == null) showroomTrophy = BuildTrophy(showroomCar.position + showroomCar.forward * 4.5f + showroomCar.right * -1.5f);
            Vector3 c = showroomTrophy != null && trophy ? showroomTrophy.position : showroomCar.position;
            rig.Orbit(c, trophy ? 5f : close ? 6.5f : 9f, trophy ? 1.6f : close ? 2.2f : 3.2f);
        }

        Transform showroomTrophy;

        Transform BuildTrophy(Vector3 at)
        {
            var g = new GameObject("Trophy");
            g.transform.SetParent(showroom.transform, false);
            g.transform.position = at;
            var mb = new MeshBuilder(2);
            mb.Sub(1);
            mb.AddBox(new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.5f, 0.9f), Quaternion.identity);
            mb.Sub(0);
            var profile = new List<Vector2>
            {
                new Vector2(0.30f, 0.50f), new Vector2(0.12f, 0.62f), new Vector2(0.08f, 0.9f), new Vector2(0.10f, 1.05f),
                new Vector2(0.35f, 1.15f), new Vector2(0.48f, 1.45f), new Vector2(0.52f, 1.85f), new Vector2(0.50f, 1.9f)
            };
            mb.AddLathe(Vector3.zero, profile, 28);
            mb.AddCylinderX(new Vector3(-0.55f, 1.6f, 0), 0.04f, 0.25f, 8, Quaternion.Euler(0, 0, 90f));
            mb.AddCylinderX(new Vector3(0.55f, 1.6f, 0), 0.04f, 0.25f, 8, Quaternion.Euler(0, 0, 90f));
            var go = new GameObject("Cup");
            go.transform.SetParent(g.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.ToMesh("trophy");
            go.AddComponent<MeshRenderer>().sharedMaterials = new[]
            {
                MaterialFactory.Lit(new Color(1f, 0.78f, 0.25f), 0.9f, 1f),
                MaterialFactory.Lit(new Color(0.1f, 0.1f, 0.12f), 0.6f, 0.2f)
            };
            g.transform.localScale = Vector3.one * 1.4f;
            var l = new GameObject("TrophyLight").AddComponent<Light>();
            l.transform.SetParent(g.transform, false);
            l.transform.localPosition = new Vector3(0.6f, 2.5f, -1.2f);
            l.type = LightType.Point;
            l.range = 8f;
            l.intensity = 2.5f;
            l.color = new Color(1f, 0.9f, 0.7f);
            return g.transform;
        }

        void DestroyShowroom()
        {
            if (showroom != null) Destroy(showroom);
            showroom = null;
            showroomTrack = -1;
            Resources.UnloadUnusedAssets();
        }

        public int SessionTeam()
        {
            if (session == SessionKind.Championship && Championship.State.IsActive) return Championship.State.playerTeam;
            return Mathf.Clamp(GameSettings.Current.playerTeam, 0, GameData.Teams.Length - 1);
        }

        public int SessionPlayerSeat()
        {
            if (session == SessionKind.Championship) return Championship.State.playerSeat;
            var seats = GameData.DriversOfTeam(SessionTeam());
            return seats[seats.Count > 1 ? 1 : 0];
        }

        public string SessionPlayerName()
        {
            if (session == SessionKind.Championship) return Championship.State.playerName;
            return GameSettings.Current.playerName;
        }

        public int SessionLaps()
        {
            if (session == SessionKind.Championship) return Championship.State.laps;
            return GameSettings.Current.raceLaps;
        }

        // ------------------------------------------------------------------ menus -> session
        public void SelectTrack(int idx)
        {
            selectedTrack = Mathf.Clamp(idx, 0, GameData.Tracks.Length - 1);
        }

        public void OpenPreRace(SessionKind kind)
        {
            session = kind;
            if (kind == SessionKind.Championship) selectedTrack = Championship.State.CurrentTrack;
            // default strategy suggestion
            SuggestStartStrategy(out startCompound, out startFuel);
            GoTo(GameState.PreRace);
            ShowShowroom(selectedTrack, false);
        }

        readonly Dictionary<int, float> lapEstimate = new Dictionary<int, float>();

        /// <summary>Racing-line lap time estimate (cached; building TrackData takes a few ms).</summary>
        public float EstimatedLapTime(int trackIndex)
        {
            if (!lapEstimate.TryGetValue(trackIndex, out var t))
            {
                t = TrackData.Build(GameData.Tracks[trackIndex]).estimatedLapTime;
                lapEstimate[trackIndex] = t;
            }
            return t;
        }

        public void SuggestStartStrategy(out TyreCompound compound, out float fuel)
        {
            int laps = SessionLaps();
            compound = laps <= 3 ? TyreCompound.Soft : TyreCompound.Medium;
            float est = EstimatedLapTime(selectedTrack) * 1.08f;
            float rate = VehicleSpec.FuelTankKg / (RaceManager.TankLaps(Mathf.Max(2, laps)) * est * 0.62f);
            float perLap = rate * 0.62f * est * GameSettings.Current.FuelFactor;
            float firstStop = Mathf.Clamp(Mathf.Round(Tyres.LifeFraction(compound) * laps), 1, Mathf.Max(1, laps - 1));
            fuel = Mathf.Clamp(perLap * (firstStop + 0.7f), 10f, VehicleSpec.FuelTankKg);
            if (GameSettings.Current.strategy == StrategyLevel.Off) fuel = 40f;
        }

        public float FuelPerLapPreview()
        {
            int laps = SessionLaps();
            float est = EstimatedLapTime(selectedTrack) * 1.08f;
            float rate = VehicleSpec.FuelTankKg / (RaceManager.TankLaps(Mathf.Max(2, laps)) * est * 0.62f);
            return rate * 0.62f * est * GameSettings.Current.FuelFactor;
        }

        public void StartSession()
        {
            Confirm();
            var s = GameSettings.Current;
            var cfg = new RaceConfig
            {
                trackIndex = selectedTrack,
                playerName = SessionPlayerName(),
                playerSeat = SessionPlayerSeat(),
                playerSetup = SaveSystem.GetSetup(GameData.Tracks[selectedTrack].id),
                playerCompound = startCompound,
                playerFuel = startFuel,
            };
            switch (session)
            {
                case SessionKind.TimeTrial:
                    cfg.type = SessionType.TimeTrial;
                    cfg.laps = 999;
                    cfg.seats = new List<int> { cfg.playerSeat };
                    break;
                case SessionKind.Championship:
                    cfg.type = SessionType.Race;
                    cfg.laps = Championship.State.laps;
                    cfg.championship = true;
                    cfg.round = Championship.State.round;
                    cfg.seats = Championship.MakeGrid(Championship.State.seats, cfg.playerSeat, s.startPosition, Random.Range(0, 100000));
                    break;
                default:
                    cfg.type = SessionType.Race;
                    cfg.laps = s.raceLaps;
                    var seats = Championship.SeatsForGrid(s.gridSize, cfg.playerSeat);
                    cfg.seats = Championship.MakeGrid(seats, cfg.playerSeat, s.startPosition, Random.Range(0, 100000));
                    break;
            }
            DestroyShowroom();
            var go = new GameObject("Race");
            race = go.AddComponent<RaceManager>();
            race.Begin(cfg, rig);
            pitMenuOpen = false;
            resultsApplied = false;
            finishedAt = -1f;
            lastResults = null;
            lastRoundTrack = selectedTrack;
            state = GameState.Racing;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        public void Pause()
        {
            if (race == null) return;
            state = GameState.Paused;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            pitMenuOpen = false;
        }

        public void Resume()
        {
            state = GameState.Racing;
            Time.timeScale = 1f;
            AudioListener.pause = false;
        }

        public void RestartSession()
        {
            EndSession(false);
            StartSession();
        }

        /// <summary>Retire / quit from the running session.</summary>
        public void RetireFromRace()
        {
            if (race == null) return;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (race.IsRace)
            {
                race.Finish(true);
                ShowResults();
            }
            else
            {
                EndSession(true);
            }
        }

        public void EndSession(bool toMenu)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (race != null) Destroy(race.gameObject);
            race = null;
            pitMenuOpen = false;
            Resources.UnloadUnusedAssets(); // free the procedural meshes of the finished session
            if (toMenu) GoTo(GameState.MainMenu);
        }

        void ShowResults()
        {
            if (race == null || race.results == null) return;
            lastResults = race.results;
            if (session == SessionKind.Championship && !resultsApplied)
            {
                Championship.ApplyResults(lastResults);
                resultsApplied = true;
            }
            state = GameState.Results;
            Time.timeScale = 1f;
            AudioListener.pause = false;
            if (rig != null && race.playerCar != null) rig.Orbit(race.playerCar.car.transform.position, 9f, 3f);
        }

        public void ContinueFromResults()
        {
            Click();
            EndSession(false);
            if (session == SessionKind.Championship)
            {
                if (Championship.State.finished)
                {
                    GoTo(GameState.SeasonEnd);
                }
                else
                {
                    selectedTrack = Championship.State.CurrentTrack;
                    GoTo(GameState.Standings);
                }
            }
            else GoTo(GameState.MainMenu);
        }

        // ------------------------------------------------------------------ garage
        public string GarageTrackId => GameData.Tracks[race != null ? race.config.trackIndex : selectedTrack].id;

        public void OpenGarage()
        {
            Click();
            returnState = state;
            editingSetup = race != null && race.playerCar != null ? race.playerCar.car.setup.Clone() : SaveSystem.GetSetup(GarageTrackId);
            setupBeforeEdit = editingSetup.Clone();
            GoTo(GameState.Garage);
        }

        /// <summary>Saves the setup for this track and applies it to the car on track immediately.</summary>
        public void SaveGarage()
        {
            editingSetup.Clamp();
            SaveSystem.SetSetup(GarageTrackId, editingSetup);
            if (race != null && race.playerCar != null)
            {
                race.playerCar.car.ApplySetup(editingSetup);
                race.ShowEvent("Yeni ayarlar araca uygulandı — farkı pistte hisset!", 3f);
            }
            setupBeforeEdit = editingSetup.Clone();
            Confirm();
        }

        public void CloseGarage()
        {
            Click();
            state = returnState;
            if (race == null) ShowShowroom(selectedTrack, false);
        }

        public void OnPlayerSetupChangedInPit(CarSetup s)
        {
            // front-wing change at a pit stop: race-only, the saved garage setup is not altered
        }

        public void OpenSettings()
        {
            Click();
            returnState = state;
            state = GameState.Settings;
        }

        public void CloseSettings()
        {
            Click();
            GameSettings.Current.ApplyQuality();
            SaveSystem.Save();
            if (race != null)
            {
                // assists can be changed mid-session
                race.playerCar.car.assists = GameSettings.Current.assists;
            }
            state = returnState;
        }

        // ------------------------------------------------------------------ pit plan
        public void PreparePitPlan()
        {
            if (race == null || race.playerCar == null) return;
            var rc = race.playerCar;
            if (!rc.planEdited)
            {
                race.SuggestPitPlan(rc, out var c, out var f);
                rc.planCompound = c;
                rc.planFuelAdd = f;
            }
        }

        void OnApplicationQuit()
        {
            SaveSystem.Save();
        }
    }
}
