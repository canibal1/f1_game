using UnityEngine;

namespace F1Game
{
    /// <summary>All screens (IMGUI). Split into partial files: menus here, Garage, Hud and Results elsewhere.</summary>
    public partial class GameUI : MonoBehaviour
    {
        GameManager gm;

        void Awake() { gm = GetComponent<GameManager>(); }

        void OnGUI()
        {
            if (gm == null) gm = GameManager.Instance;
            if (gm == null) return;
            UI.Begin();
            GUI.depth = 0;
            switch (gm.state)
            {
                case GameState.MainMenu: MainMenu(); break;
                case GameState.QuickRaceSetup: QuickRaceSetup(); break;
                case GameState.TimeTrialSelect: TimeTrialSelect(); break;
                case GameState.ChampionshipSetup: ChampionshipSetup(); break;
                case GameState.ChampionshipHub: ChampionshipHub(); break;
                case GameState.PreRace: PreRace(); break;
                case GameState.Garage: if (gm.race != null) Hud(true); Garage(); break;
                case GameState.Settings: if (gm.race != null) Hud(true); Settings(); break;
                case GameState.Racing: Hud(false); break;
                case GameState.Paused: Hud(true); PauseMenu(); break;
                case GameState.Results: Results(); break;
                case GameState.Standings: Standings(); break;
                case GameState.SeasonEnd: SeasonEnd(); break;
                case GameState.Help: Help(); break;
            }
        }

        // ------------------------------------------------------------------ helpers
        void Header(string t, string sub = null)
        {
            UI.Fill(new Rect(0, 0, UI.W, 110), new Color(0f, 0f, 0f, 0.55f));
            UI.Fill(new Rect(0, 108, UI.W, 4), UI.Accent);
            UI.Text(new Rect(60, 14, 1200, 60), t, UI.h1);
            if (sub != null) UI.Text(new Rect(62, 62, 1400, 36), sub, UI.small, UI.Dim);
            UI.Text(new Rect(UI.W - 420, 24, 380, 60), "<b>MONOPOSTO</b> GP", UI.h2, new Color(1, 1, 1, 0.6f));
        }

        static string Laps(float l) => l >= 10 ? $"{l:0}" : $"{l:0.0}";

        string TrackLine(int i)
        {
            var t = GameData.Tracks[i];
            float best = SaveSystem.BestLap(t.id);
            return $"{t.name}  <size=16><color=#aab>({t.location})</color></size>" + (best < 1e6f ? $"  <size=16><color=#fd5>En iyi: {RaceManager.FormatTime(best)}</color></size>" : "");
        }

        void TrackPicker(Rect area)
        {
            UI.Panel(area);
            UI.Text(new Rect(area.x + 20, area.y + 10, 400, 40), "PİST SEÇİMİ", UI.h2);
            for (int i = 0; i < GameData.Tracks.Length; i++)
            {
                var r = new Rect(area.x + 20, area.y + 60 + i * 62, area.width - 40, 54);
                if (UI.Button(r, "", gm.selectedTrack == i) && gm.selectedTrack != i)
                {
                    gm.SelectTrack(i);
                    gm.ShowShowroom(i, false); // preview the chosen circuit in the background
                }
                UI.Text(new Rect(r.x + 16, r.y + 4, r.width - 20, 30), TrackLine(i), UI.labelBold);
                var t = GameData.Tracks[i];
                UI.Text(new Rect(r.x + 16, r.y + 28, r.width - 20, 24), t.description, UI.tiny, UI.Dim);
            }
        }

        void DifficultyRow(Rect r)
        {
            var s = GameSettings.Current;
            UI.Text(new Rect(r.x, r.y, 400, 34), $"YAPAY ZEKA ZORLUĞU: <color=#fd5>{s.DifficultyName()}</color>", UI.labelBold);
            float bw = (r.width - 40) / GameSettings.DifficultyPresets.Length;
            for (int i = 0; i < GameSettings.DifficultyPresets.Length; i++)
            {
                var p = GameSettings.DifficultyPresets[i];
                bool on = Mathf.Abs(p.pace - s.aiPace) < 0.5f && Mathf.Abs(p.aggr - s.aiAggression) < 0.5f;
                if (UI.Button(new Rect(r.x + i * (bw + 10), r.y + 40, bw, 44), p.name, on)) { s.aiPace = p.pace; s.aiAggression = p.aggr; }
            }
            s.aiPace = Mathf.Round(UI.Slider(new Rect(r.x, r.y + 100, r.width, 50), "AI Hızı", s.aiPace, 0, 100, $"{s.aiPace:0}",
                "Rakiplerin viraj ve düzlük temposu (yarış çizgisi hız profilinin %84–%104'ü)."));
            s.aiAggression = Mathf.Round(UI.Slider(new Rect(r.x, r.y + 160, r.width, 50), "AI Agresifliği", s.aiAggression, 0, 100, $"{s.aiAggression:0}",
                "Takip mesafesi, sollama denemeleri, savunma ve geç frenleme."));
        }

        void TeamPicker(Rect r, ref int team)
        {
            UI.Text(new Rect(r.x, r.y, 400, 34), "TAKIM", UI.labelBold);
            int cols = 5;
            float bw = (r.width - (cols - 1) * 10) / cols;
            for (int i = 0; i < GameData.Teams.Length; i++)
            {
                var t = GameData.Teams[i];
                var br = new Rect(r.x + (i % cols) * (bw + 10), r.y + 40 + (i / cols) * 58, bw, 50);
                if (UI.Button(br, "", team == i)) team = i;
                UI.Fill(new Rect(br.x + 8, br.y + 10, 10, 30), t.primary);
                UI.Fill(new Rect(br.x + 20, br.y + 10, 5, 30), t.secondary);
                UI.Text(new Rect(br.x + 32, br.y, bw - 36, 50), t.name, UI.small);
            }
        }

        // ------------------------------------------------------------------ main menu
        void MainMenu()
        {
            UI.Fill(new Rect(0, 0, 640, UI.H), new Color(0f, 0f, 0f, 0.55f));
            UI.Fill(new Rect(636, 0, 4, UI.H), UI.Accent);
            UI.Text(new Rect(60, 60, 600, 80), "MONOPOSTO", UI.title);
            UI.Text(new Rect(64, 130, 600, 60), "<color=#e33>GRAND PRIX</color> ŞAMPİYONASI", UI.h2);
            UI.Text(new Rect(64, 175, 560, 50), "Tek kişilik açık tekerlekli yarış simülasyonu", UI.small, UI.Dim);

            float y = 250;
            var ch = Championship.State;
            if (SaveSystem.Data.hasChampionship && ch.IsActive)
            {
                if (UI.Button(new Rect(60, y, 520, 64), $"ŞAMPİYONAYA DEVAM ET  (Yarış {ch.round + 1}/{ch.Rounds})", true))
                {
                    gm.session = SessionKind.Championship;
                    gm.selectedTrack = ch.CurrentTrack;
                    gm.GoTo(GameState.ChampionshipHub);
                }
                y += 76;
            }
            if (UI.Button(new Rect(60, y, 520, 64), "YENİ ŞAMPİYONA (KARİYER)")) { gm.session = SessionKind.Championship; gm.GoTo(GameState.ChampionshipSetup); }
            y += 76;
            if (UI.Button(new Rect(60, y, 520, 64), "HIZLI YARIŞ")) { gm.session = SessionKind.QuickRace; gm.GoTo(GameState.QuickRaceSetup); }
            y += 76;
            if (UI.Button(new Rect(60, y, 520, 64), "ZAMANA KARŞI / ANTRENMAN")) { gm.session = SessionKind.TimeTrial; gm.GoTo(GameState.TimeTrialSelect); }
            y += 76;
            if (UI.Button(new Rect(60, y, 520, 64), "GARAJ — ARAÇ AYARLARI")) { gm.OpenGarage(); }
            y += 76;
            if (UI.Button(new Rect(60, y, 520, 64), "AYARLAR (ZORLUK, ASİSTLER, SES)")) { gm.OpenSettings(); }
            y += 76;
            if (UI.Button(new Rect(60, y, 520, 64), "KONTROLLER / NASIL OYNANIR")) { gm.GoTo(GameState.Help); }
            y += 76;
            if (UI.Button(new Rect(60, y, 520, 64), "ÇIKIŞ")) { SaveSystem.Save(); Application.Quit(); }

            // right info panel
            var info = new Rect(UI.W - 560, UI.H - 420, 520, 380);
            UI.Panel(info);
            UI.Text(new Rect(info.x + 20, info.y + 12, 480, 40), "REKORLAR & KUPALAR", UI.h2);
            float yy = info.y + 60;
            for (int i = 0; i < GameData.Tracks.Length; i++)
            {
                float b = SaveSystem.BestLap(GameData.Tracks[i].id);
                UI.Text(new Rect(info.x + 20, yy, 330, 28), GameData.Tracks[i].name, UI.small);
                UI.Text(new Rect(info.x + 340, yy, 160, 28), b < 1e6f ? RaceManager.FormatTime(b) : "—", UI.right, UI.Gold);
                yy += 30;
            }
            yy += 10;
            var tr = SaveSystem.Data.trophies;
            UI.Text(new Rect(info.x + 20, yy, 480, 28), tr.Count == 0 ? "Henüz kupa yok — bir şampiyona kazan!" : $"KUPA DOLABI: {tr.Count} sezon", UI.small, UI.Gold);
            for (int i = Mathf.Max(0, tr.Count - 3); i < tr.Count; i++)
            {
                yy += 26;
                UI.Text(new Rect(info.x + 20, yy, 480, 26), tr[i], UI.tiny, UI.Dim);
            }
            UI.Text(new Rect(UI.W - 560, UI.H - 30, 540, 24), "Modeller ve sesler: Kenney (CC0) • Müzik: cynicmusic (CC0)", UI.tiny, new Color(1, 1, 1, 0.5f));
        }

        // ------------------------------------------------------------------ quick race / time trial
        void QuickRaceSetup()
        {
            var s = GameSettings.Current;
            Header("HIZLI YARIŞ", "Pisti, tur sayısını ve rakiplerin zorluğunu seç");
            TrackPicker(new Rect(40, 140, 760, 460));

            var r = new Rect(830, 140, UI.W - 870, 720);
            UI.Panel(r);
            float x = r.x + 24, w = r.width - 48, y = r.y + 16;
            UI.Text(new Rect(x, y, 200, 34), "TUR SAYISI", UI.labelBold);
            int[] laps = { 3, 5, 8, 10, 15, 20 };
            for (int i = 0; i < laps.Length; i++)
                if (UI.Button(new Rect(x + 220 + i * 90, y - 2, 80, 40), laps[i].ToString(), s.raceLaps == laps[i])) s.raceLaps = laps[i];
            y += 56;
            UI.Text(new Rect(x, y, 200, 34), "GRID", UI.labelBold);
            int[] grids = { 10, 16, 20 };
            for (int i = 0; i < grids.Length; i++)
                if (UI.Button(new Rect(x + 220 + i * 130, y - 2, 120, 40), grids[i] + " araç", s.gridSize == grids[i])) s.gridSize = grids[i];
            y += 56;
            UI.Text(new Rect(x, y, 200, 34), "BAŞLANGIÇ", UI.labelBold);
            string[] sp = { "Rastgele", "En arka", "Orta", "Pole" };
            for (int i = 0; i < sp.Length; i++)
                if (UI.Button(new Rect(x + 220 + i * 150, y - 2, 140, 40), sp[i], (int)s.startPosition == i)) s.startPosition = (StartPosition)i;
            y += 60;
            int team = s.playerTeam;
            TeamPicker(new Rect(x, y, w, 160), ref team);
            if (team != s.playerTeam)
            {
                s.playerTeam = team;
                gm.ShowShowroom(gm.selectedTrack, false); // repaint the display car in the new livery
            }
            y += 170;
            DifficultyRow(new Rect(x, y, w, 220));

            if (UI.Button(new Rect(40, UI.H - 110, 260, 70), "GERİ")) { SaveSystem.Save(); gm.GoTo(GameState.MainMenu); }
            if (UI.Button(new Rect(UI.W - 460, UI.H - 110, 420, 70), "DEVAM — YARIŞ HAZIRLIĞI »", true)) { SaveSystem.Save(); gm.OpenPreRace(SessionKind.QuickRace); }
        }

        void TimeTrialSelect()
        {
            Header("ZAMANA KARŞI / ANTRENMAN", "Tek başına pistte: tur zamanını geliştir, en iyi turunun hayaletine karşı sür, ayarları dene.");
            TrackPicker(new Rect(40, 140, 760, 460));
            var r = new Rect(830, 140, UI.W - 870, 460);
            UI.Panel(r);
            UI.Text(new Rect(r.x + 24, r.y + 16, r.width - 48, 400),
                "• Lastik aşınması ve yakıt tüketimi kapalıdır; sadece hızına odaklan.\n" +
                "• En iyi geçerli turun yarı saydam <b>hayalet araç</b> olarak gösterilir ve canlı <b>delta</b> süresi ekranda.\n" +
                "• Dört tekerlek de pist dışına çıkarsa tur <color=#f55>geçersiz</color> sayılır.\n" +
                "• ESC → Garaj ile ayarları değiştir; KAYDET dediğin anda araç fiziği güncellenir ve pistte farkı hemen hissedersin.\n" +
                "• Sektör süreleri: <color=#c6f>mor</color> = kişisel en iyi, <color=#5e6>yeşil</color> = önceki turdan iyi.", UI.label);
            if (UI.Button(new Rect(40, UI.H - 110, 260, 70), "GERİ")) gm.GoTo(GameState.MainMenu);
            if (UI.Button(new Rect(UI.W - 460, UI.H - 110, 420, 70), "DEVAM »", true)) gm.OpenPreRace(SessionKind.TimeTrial);
        }

        // ------------------------------------------------------------------ championship
        string newName;
        int newTeam = -1;
        int newLaps = 5, newGrid = 20;

        void ChampionshipSetup()
        {
            var s = GameSettings.Current;
            if (newName == null) newName = s.playerName;
            if (newTeam < 0) newTeam = s.playerTeam;
            Header("YENİ ŞAMPİYONA", $"{GameData.Tracks.Length} yarışlık sezon • Puanlar: 25-18-15-12-10-8-6-4-2-1 (+1 en hızlı tur)");
            var r = new Rect(40, 140, UI.W - 80, 760);
            UI.Panel(r);
            float x = r.x + 30, w = Mathf.Min(1100, r.width - 60), y = r.y + 20;
            UI.Text(new Rect(x, y, 260, 40), "PİLOT ADI", UI.labelBold);
            newName = GUI.TextField(new Rect(x + 260, y, 420, 44), newName ?? "", 18, UI.field);
            y += 70;
            TeamPicker(new Rect(x, y, w, 160), ref newTeam);
            y += 180;
            UI.Text(new Rect(x, y, 260, 34), "YARIŞ BAŞINA TUR", UI.labelBold);
            int[] laps = { 3, 5, 8, 10, 15 };
            for (int i = 0; i < laps.Length; i++)
                if (UI.Button(new Rect(x + 260 + i * 90, y - 2, 80, 40), laps[i].ToString(), newLaps == laps[i])) newLaps = laps[i];
            y += 56;
            UI.Text(new Rect(x, y, 260, 34), "GRID", UI.labelBold);
            int[] grids = { 10, 20 };
            for (int i = 0; i < grids.Length; i++)
                if (UI.Button(new Rect(x + 260 + i * 130, y - 2, 120, 40), grids[i] + " araç", newGrid == grids[i])) newGrid = grids[i];
            y += 70;
            DifficultyRow(new Rect(x, y, w, 220));

            var cal = new Rect(r.x + r.width - 520, r.y + 20, 490, 420);
            UI.Text(new Rect(cal.x, cal.y, 480, 34), "TAKVİM", UI.labelBold);
            for (int i = 0; i < GameData.Tracks.Length; i++)
                UI.Text(new Rect(cal.x, cal.y + 40 + i * 34, 480, 32), $"{i + 1}. {GameData.Tracks[i].name}", UI.label);
            if (SaveSystem.Data.hasChampionship && Championship.State.IsActive)
                UI.Text(new Rect(cal.x, cal.y + 300, 480, 80), "<color=#f88>Uyarı:</color> yeni şampiyona, devam eden sezonun yerine geçer.", UI.small);

            if (UI.Button(new Rect(40, UI.H - 110, 260, 70), "GERİ")) gm.GoTo(GameState.MainMenu);
            if (UI.Button(new Rect(UI.W - 460, UI.H - 110, 420, 70), "SEZONU BAŞLAT »", true))
            {
                s.playerName = string.IsNullOrWhiteSpace(newName) ? "Oyuncu" : newName.Trim();
                s.playerTeam = newTeam;
                Championship.StartNew(s.playerName, newTeam, newLaps, newGrid);
                gm.session = SessionKind.Championship;
                gm.selectedTrack = Championship.State.CurrentTrack;
                gm.Confirm();
                gm.GoTo(GameState.ChampionshipHub);
            }
        }

        void ChampionshipHub()
        {
            var st = Championship.State;
            var t = GameData.Tracks[st.CurrentTrack];
            Header($"ŞAMPİYONA — YARIŞ {st.round + 1}/{st.Rounds}", $"{st.playerName} • {GameData.Teams[st.playerTeam].name} • Şampiyonada {Championship.PlayerStanding()}. sıra, {st.driverPoints[st.playerSeat]} puan");
            var left = new Rect(40, 140, 760, 720);
            UI.Panel(left);
            UI.Text(new Rect(left.x + 24, left.y + 16, 700, 44), "SIRADAKİ YARIŞ", UI.h2, UI.Gold);
            UI.Text(new Rect(left.x + 24, left.y + 60, 700, 50), t.name, UI.h1);
            UI.Text(new Rect(left.x + 24, left.y + 110, 700, 30), $"{t.location} • {st.laps} tur", UI.label, UI.Dim);
            UI.Text(new Rect(left.x + 24, left.y + 145, 700, 60), t.description, UI.label);
            float y = left.y + 230;
            UI.Text(new Rect(left.x + 24, y, 700, 34), "TAKVİM", UI.labelBold);
            for (int i = 0; i < st.calendar.Count; i++)
            {
                y += 36;
                string res = "";
                if (i < st.history.Count)
                {
                    var h = st.history[i];
                    int pPos = h.order.IndexOf(st.playerSeat) + 1;
                    res = $"Kazanan: {Championship.SeatName(h.order[0])} • Sen: {(h.dnf[pPos - 1] ? "DNF" : pPos + ".")}";
                }
                Color c = i == st.round ? UI.Gold : i < st.round ? UI.Dim : Color.white;
                UI.Text(new Rect(left.x + 24, y, 320, 34), $"{i + 1}. {GameData.Tracks[st.calendar[i]].name}", UI.label, c);
                UI.Text(new Rect(left.x + 350, y, 390, 34), res, UI.small, UI.Dim);
            }

            var right = new Rect(830, 140, UI.W - 870, 720);
            UI.Panel(right);
            UI.Text(new Rect(right.x + 24, right.y + 16, 500, 40), "PİLOTLAR KLASMANI", UI.h2);
            var standings = Championship.DriverStandings();
            for (int i = 0; i < Mathf.Min(14, standings.Count); i++)
            {
                int seat = standings[i];
                float yy = right.y + 66 + i * 44;
                bool me = seat == st.playerSeat;
                if (me) UI.Fill(new Rect(right.x + 14, yy - 2, right.width - 28, 40), new Color(0.8f, 0.1f, 0.15f, 0.35f));
                UI.Text(new Rect(right.x + 24, yy, 50, 36), (i + 1).ToString(), UI.labelBold);
                UI.Fill(new Rect(right.x + 74, yy + 6, 6, 26), GameData.Teams[GameData.Drivers[seat].team].primary);
                UI.Text(new Rect(right.x + 90, yy, 360, 36), Championship.SeatName(seat), UI.label);
                UI.Text(new Rect(right.x + 450, yy, 300, 36), GameData.Teams[GameData.Drivers[seat].team].name, UI.small, UI.Dim);
                UI.Text(new Rect(right.x + right.width - 160, yy, 130, 36), $"{st.driverPoints[seat]} p", UI.right, UI.Gold);
            }

            if (UI.Button(new Rect(40, UI.H - 110, 260, 70), "ANA MENÜ")) gm.GoTo(GameState.MainMenu);
            if (UI.Button(new Rect(320, UI.H - 110, 300, 70), "PUAN DURUMU")) gm.GoTo(GameState.Standings);
            if (UI.Button(new Rect(640, UI.H - 110, 260, 70), "GARAJ")) gm.OpenGarage();
            if (UI.Button(new Rect(UI.W - 460, UI.H - 110, 420, 70), "YARIŞ HAZIRLIĞI »", true)) gm.OpenPreRace(SessionKind.Championship);
        }

        // ------------------------------------------------------------------ pre race (strategy)
        void PreRace()
        {
            var t = GameData.Tracks[gm.selectedTrack];
            bool tt = gm.session == SessionKind.TimeTrial;
            int laps = gm.SessionLaps();
            float est = gm.EstimatedLapTime(gm.selectedTrack) * 1.08f;
            Header(tt ? "ZAMANA KARŞI — " + t.name : "YARIŞ HAZIRLIĞI — " + t.name,
                tt ? "Garajda ayar yap, sonra piste çık." : $"{laps} tur • tahmini tur ~{RaceManager.FormatTime(est)} • Strateji: lastik hamuru ve başlangıç yakıtı");

            var left = new Rect(40, 140, 860, 740);
            UI.Panel(left);
            float x = left.x + 24, w = left.width - 48, y = left.y + 16;
            if (!tt)
            {
                UI.Text(new Rect(x, y, w, 40), "BAŞLANGIÇ LASTİĞİ", UI.h2);
                y += 50;
                for (int i = 0; i < 3; i++)
                {
                    var c = (TyreCompound)i;
                    var br = new Rect(x + i * 270, y, 255, 120);
                    if (UI.Button(br, "", gm.startCompound == c)) gm.startCompound = c;
                    UI.Dot(new Vector2(br.x + 40, br.y + 40), 46, Tyres.Color(c));
                    UI.Text(new Rect(br.x + 30, br.y + 18, 22, 44), Tyres.Letter(c), UI.center, Color.black);
                    UI.Text(new Rect(br.x + 78, br.y + 14, 170, 34), Tyres.Name(c), UI.labelBold);
                    float lifeLaps = Tyres.LifeFraction(c) * laps;
                    UI.Text(new Rect(br.x + 78, br.y + 46, 175, 70),
                        $"Tutuş: %{Tyres.Grip(c) * 100f:0.0}\nÖmür: ~{Laps(lifeLaps)} tur", UI.small, UI.Dim);
                }
                y += 140;
                float perLap = gm.FuelPerLapPreview();
                UI.Text(new Rect(x, y, w, 40), "BAŞLANGIÇ YAKITI", UI.h2);
                y += 46;
                if (GameSettings.Current.strategy == StrategyLevel.Off)
                {
                    UI.Text(new Rect(x, y, w, 60), "Strateji (lastik aşınması/yakıt tüketimi) ayarlardan kapatılmış.", UI.label, UI.Dim);
                    y += 60;
                }
                else
                {
                    gm.startFuel = Mathf.Round(UI.Slider(new Rect(x, y, w, 50), "Yakıt (kg)", gm.startFuel, 5f, VehicleSpec.FuelTankKg,
                        $"{gm.startFuel:0} kg ≈ {Laps(gm.startFuel / Mathf.Max(perLap, 0.01f))} tur",
                        $"Tur başına ~{perLap:0.0} kg. Ağır araç yavaştır (+10 kg ≈ daha az ivme/tutuş); hafif başlarsan erken pite girersin."));
                    y += 70;
                    if (UI.Button(new Rect(x, y, 360, 48), "ÖNERİLEN STRATEJİ"))
                    {
                        gm.SuggestStartStrategy(out var c, out var f);
                        gm.startCompound = c;
                        gm.startFuel = Mathf.Round(f);
                    }
                    float totalNeed = perLap * laps;
                    int minStops = Mathf.Max(0, Mathf.CeilToInt(totalNeed / VehicleSpec.FuelTankKg - 0.001f) - (gm.startFuel >= totalNeed ? 1 : 0));
                    UI.Text(new Rect(x + 380, y, w - 380, 48), $"Yarış için toplam ~{totalNeed:0} kg yakıt gerekir (depo {VehicleSpec.FuelTankKg:0} kg).", UI.small, UI.Dim);
                    y += 70;
                }
                UI.Text(new Rect(x, y, w, 120),
                    "<b>Pit stop:</b> yarışta <b>P</b> ile pit menüsünü aç, hamur ve eklenecek yakıtı seç, <b>B</b> ile \"bu tur pite gir\" isteği ver. " +
                    "Lastik %70 aşınınca ya da yakıt azalınca ekranda uyarı çıkar. Pit girişine gelince araç otomatik olarak hız sınırlayıcıyla kutuya gider, " +
                    "seçtiğin lastik/yakıt takılır ve piste geri döner.", UI.small);
            }
            else
            {
                float best = SaveSystem.BestLap(t.id);
                UI.Text(new Rect(x, y, w, 40), "KİŞİSEL REKOR: " + (best < 1e6f ? RaceManager.FormatTime(best) : "—"), UI.h2, UI.Gold);
                UI.Text(new Rect(x, y + 60, w, 200), t.description + "\n\nÖnerilen ayar tipi: " + PresetName(t.recommended), UI.label);
            }

            var right = new Rect(930, 140, UI.W - 970, 740);
            UI.Panel(right);
            var setup = SaveSystem.GetSetup(t.id);
            float rx = right.x + 24, rw = right.width - 48, ry = right.y + 16;
            UI.Text(new Rect(rx, ry, rw, 40), "ARAÇ AYARI", UI.h2);
            ry += 50;
            UI.Text(new Rect(rx, ry, rw, 120), setup.Summary(), UI.label);
            ry += 80;
            float top = VehicleSpec.EstimateTopSpeed(setup, 850f) * 3.6f;
            UI.Text(new Rect(rx, ry, rw, 34), $"Tahmini son hız: <color=#fd5>{top:0} km/sa</color> • 250 km/sa'da downforce: <color=#fd5>{VehicleSpec.Downforce(setup, 69.4f) / 9.81f:0} kg</color>", UI.label);
            ry += 44;
            UI.Text(new Rect(rx, ry, rw, 34), "Bu pist için önerilen: " + PresetName(t.recommended), UI.small, UI.Dim);
            ry += 44;
            if (UI.Button(new Rect(rx, ry, 300, 56), "GARAJA GİT")) gm.OpenGarage();
            ry += 80;
            var s = GameSettings.Current;
            if (!tt)
            {
                UI.Text(new Rect(rx, ry, rw, 30), $"Rakipler: <color=#fd5>{s.DifficultyName()}</color> (hız {s.aiPace:0}, agresiflik {s.aiAggression:0})", UI.label);
                ry += 40;
            }
            UI.Text(new Rect(rx, ry, rw, 120),
                $"Asistler: Çekiş kontrolü {(s.assists.tractionControl ? "açık" : "kapalı")}, ABS {(s.assists.abs ? "açık" : "kapalı")}, " +
                $"vites {(s.assists.autoGearbox ? "otomatik" : "manuel")}, direksiyon {(s.assists.steeringAssist ? "yardımlı" : "serbest")}, " +
                $"fren yardımı {(s.assists.brakingAssist ? "açık" : "kapalı")}, yarış çizgisi {(s.showRacingLine ? "açık" : "kapalı")}.", UI.small, UI.Dim);
            ry += 90;
            if (UI.Button(new Rect(rx, ry, 300, 50), "AYARLAR")) gm.OpenSettings();

            if (UI.Button(new Rect(40, UI.H - 110, 260, 70), "GERİ"))
            {
                gm.GoTo(gm.session == SessionKind.Championship ? GameState.ChampionshipHub : gm.session == SessionKind.TimeTrial ? GameState.TimeTrialSelect : GameState.QuickRaceSetup);
            }
            if (UI.Button(new Rect(UI.W - 460, UI.H - 110, 420, 70), tt ? "PİSTE ÇIK »" : "YARIŞA BAŞLA »", true)) gm.StartSession();
        }

        static string PresetName(CarSetupPreset p)
        {
            switch (p)
            {
                case CarSetupPreset.LowDownforce: return "Düşük downforce (yüksek son hız)";
                case CarSetupPreset.HighDownforce: return "Yüksek downforce (viraj tutuşu)";
                default: return "Dengeli";
            }
        }

        // ------------------------------------------------------------------ settings
        void Settings()
        {
            var s = GameSettings.Current;
            UI.Fill(new Rect(0, 0, UI.W, UI.H), new Color(0, 0, 0, 0.45f));
            Header("AYARLAR", "Zorluk, sürüş asistleri, strateji, ses ve grafik");
            var r1 = new Rect(40, 140, 900, 820);
            UI.Panel(r1);
            float x = r1.x + 24, w = r1.width - 48, y = r1.y + 16;
            DifficultyRow(new Rect(x, y, w, 220));
            y += 240;
            UI.Text(new Rect(x, y, w, 34), "SÜRÜŞ ASİSTLERİ", UI.h2);
            y += 46;
            var a = s.assists;
            a.tractionControl = UI.Toggle(new Rect(x, y, w, 60), "Çekiş kontrolü", a.tractionControl, "Gaza basınca arka tekerleklerin patinajını önler.");
            y += 62;
            a.abs = UI.Toggle(new Rect(x, y, w, 60), "ABS", a.abs, "Frende tekerlek kilitlenmesini önler.");
            y += 62;
            a.autoGearbox = UI.Toggle(new Rect(x, y, w, 60), "Otomatik vites", a.autoGearbox, "Kapalıyken E/Q (veya omuz tuşları) ile vites değiştir.");
            y += 62;
            a.steeringAssist = UI.Toggle(new Rect(x, y, w, 60), "Direksiyon yardımı", a.steeringAssist, "Hızla birlikte direksiyon açısını lastiğin tutuş sınırına göre ayarlar (klavye için önerilir).");
            y += 62;
            a.brakingAssist = UI.Toggle(new Rect(x, y, w, 60), "Fren yardımı", a.brakingAssist, "Virajlara fazla hızlı girersen otomatik fren yapar (yeni başlayanlar için).");
            s.assists = a;
            y += 62;
            s.showRacingLine = UI.Toggle(new Rect(x, y, w, 60), "Yarış çizgisi", s.showRacingLine, "Yeşil = gaz, sarı = gazı kes, kırmızı = fren.");

            var r2 = new Rect(960, 140, UI.W - 1000, 820);
            UI.Panel(r2);
            x = r2.x + 24; w = r2.width - 48; y = r2.y + 16;
            UI.Text(new Rect(x, y, w, 34), "STRATEJİ & YARIŞ", UI.h2);
            y += 46;
            UI.Text(new Rect(x, y, 330, 34), "Lastik aşınması / yakıt", UI.labelBold);
            string[] lv = { "Kapalı", "Normal", "Yoğun" };
            for (int i = 0; i < 3; i++)
                if (UI.Button(new Rect(x + 340 + i * 130, y - 2, 120, 40), lv[i], (int)s.strategy == i)) s.strategy = (StrategyLevel)i;
            y += 54;
            s.drsEnabled = UI.Toggle(new Rect(x, y, w, 60), "DRS", s.drsEnabled, "Ana düzlükte, öndeki araca 1 sn'den yakınsan arka kanadı aç (F tuşu).");
            y += 70;
            UI.Text(new Rect(x, y, w, 34), "SES", UI.h2);
            y += 46;
            s.masterVolume = UI.Slider(new Rect(x, y, w, 40), "Genel", s.masterVolume, 0, 1, $"%{s.masterVolume * 100:0}");
            y += 44;
            s.sfxVolume = UI.Slider(new Rect(x, y, w, 40), "Efektler / Motor", s.sfxVolume, 0, 1, $"%{s.sfxVolume * 100:0}");
            y += 44;
            s.musicVolume = UI.Slider(new Rect(x, y, w, 40), "Müzik", s.musicVolume, 0, 1, $"%{s.musicVolume * 100:0}");
            y += 60;
            UI.Text(new Rect(x, y, w, 34), "GRAFİK", UI.h2);
            y += 46;
            string[] q = { "Düşük", "Orta", "Yüksek" };
            for (int i = 0; i < 3; i++)
                if (UI.Button(new Rect(x + i * 160, y, 150, 44), q[i], s.quality == i)) { s.quality = i; s.ApplyQuality(); }
            y += 64;
            UI.Text(new Rect(x, y, 260, 34), "Varsayılan kamera", UI.labelBold);
            for (int i = 0; i < CameraRig.ModeNames.Length; i++)
                if (UI.Button(new Rect(x + 260 + i * 150, y - 2, 140, 40), CameraRig.ModeNames[i], s.cameraMode == i))
                {
                    s.cameraMode = i;
                    if (gm.rig != null && gm.race != null) { gm.rig.mode = (CameraRig.Mode)i; }
                }
            y += 60;
            UI.Text(new Rect(x, y, 260, 40), "Pilot adı (hızlı yarış)", UI.labelBold);
            s.playerName = GUI.TextField(new Rect(x + 260, y, 360, 42), s.playerName ?? "", 18, UI.field);

            if (UI.Button(new Rect(UI.W - 360, UI.H - 100, 320, 66), "KAYDET VE DÖN", true)) gm.CloseSettings();
        }

        // ------------------------------------------------------------------ help
        void Help()
        {
            Header("KONTROLLER / NASIL OYNANIR");
            var r = new Rect(40, 140, UI.W - 80, 800);
            UI.Panel(r);
            UI.Text(new Rect(r.x + 30, r.y + 20, 800, 760),
                "<b>KLAVYE</b>\n" +
                "W / ↑  : Gaz\nS / ↓ / Boşluk : Fren (durunca basılı tut = geri vites)\nA D / ← → : Direksiyon\n" +
                "E / Sol Shift : Vites yükselt (manuel)\nQ / Sol Ctrl : Vites düşür (manuel)\n" +
                "F : DRS aç/kapat (izin verildiğinde)\nC : Kamera (Takip, Uzak, Kokpit, T-Kamera)\nV : Geriye bak\n" +
                "P / Tab : Pit stratejisi menüsü (1/2/3 hamur, 4/5 yakıt, 6/7 ön kanat)\nB : Bu tur pite gir (BOX)\n" +
                "R : Aracı piste geri koy\nESC : Duraklat (Garaj, Ayarlar, Yeniden başlat)\n\n" +
                "<b>GAMEPAD</b> (Input System etkin olduğunda)\n" +
                "Sol çubuk: direksiyon • RT: gaz • LT: fren • RB/LB: vites • Y: kamera\nX: piste geri koy • Start: duraklat • Back: pit menüsü • D-pad aşağı: BOX • A: DRS",
                UI.label);
            UI.Text(new Rect(r.x + 860, r.y + 20, r.width - 900, 760),
                "<b>İPUÇLARI</b>\n" +
                "• Ayarlar: düşük kanat = yüksek son hız ama virajda az tutuş. Yüksek kanat = sağlam viraj, yavaş düzlük.\n" +
                "• Sert süspansiyon aerodinamik platformu korur; yumuşak süspansiyon yavaş virajlarda mekanik tutuş verir.\n" +
                "• Düşük sürüş yüksekliği downforce'u artırır ama yüksek hızda zemine sürtünme (kıvılcım) olur.\n" +
                "• Fren dengesi öne kaydıkça ön lastikler kilitlenir (düz gider), arkaya kaydıkça araç fren altında döner.\n" +
                "• Vites oranları: kısa son oran = hızlı ivme, ama son viteste devir sınırına takılırsın.\n\n" +
                "• Öndeki aracın 1–2 saniye arkasında vakum (slipstream) düzlükte hız kazandırır; " +
                "fakat çok yakında ön kanat kirli havadan dolayı downforce kaybeder.\n" +
                "• Yumuşak lastik en hızlı ama en kısa ömürlü; sert lastik yavaş ama uzun ömürlü.\n" +
                "• Yakıt ağırlığı tur süresini etkiler: hafif başla ve erken pite gir, ya da ağır başla ve uzun git.",
                UI.label);
            if (UI.Button(new Rect(40, UI.H - 110, 260, 70), "GERİ")) gm.GoTo(GameState.MainMenu);
        }
    }
}
