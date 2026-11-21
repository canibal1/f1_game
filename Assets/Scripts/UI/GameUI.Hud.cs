using UnityEngine;

namespace F1Game
{
    public partial class GameUI
    {
        // ------------------------------------------------------------------ in-race HUD
        void Hud(bool dimmed)
        {
            var race = gm.race;
            if (race == null || race.playerCar == null) return;
            var me = race.playerCar;
            var car = me.car;
            var oldColor = GUI.color;
            if (dimmed) GUI.color = new Color(1, 1, 1, 0.35f);

            TimingPanel(race, me);
            if (race.IsRace) Tower(race, me);
            Minimap(race, me);
            Dash(race, me, car);
            TyreFuel(race, me, car);
            GUI.color = oldColor;
            if (dimmed) return;

            // start lights
            if (race.state == RaceState.Countdown)
            {
                var r = new Rect(UI.W * 0.5f - 260, 140, 520, 120);
                UI.Panel(r, true);
                for (int i = 0; i < 5; i++)
                    UI.Dot(new Vector2(r.x + 60 + i * 100, r.y + 60), 70, i < race.lightsOn ? new Color(1f, 0.1f, 0.08f) : new Color(0.25f, 0.05f, 0.05f));
                UI.Text(new Rect(r.x, r.y + 122, r.width, 30), "Işıklar sönünce bas gaza!", UI.center, UI.Dim);
            }

            // event message
            if (race.eventTimer > 0f && !string.IsNullOrEmpty(race.eventMessage))
            {
                float a = Mathf.Clamp01(race.eventTimer * 2f);
                var r = new Rect(UI.W * 0.5f - 520, 90, 1040, 56);
                var c = GUI.color; GUI.color = new Color(1, 1, 1, a);
                UI.Panel(r, true);
                UI.Text(r, race.eventMessage, UI.center, Color.white);
                GUI.color = c;
            }

            // warnings (flashing)
            if (race.warnings.Count > 0 && !me.InPit)
            {
                bool flash = (Time.unscaledTime % 0.8f) < 0.55f;
                for (int i = 0; i < race.warnings.Count; i++)
                {
                    var r = new Rect(UI.W * 0.5f - 480, 160 + i * 62, 960, 56);
                    UI.Fill(r, flash ? new Color(0.75f, 0.05f, 0.05f, 0.85f) : new Color(0.3f, 0.02f, 0.02f, 0.8f));
                    UI.Text(r, race.warnings[i], UI.warn, Color.white);
                }
            }
            if (me.boxRequested && !me.InPit && race.IsRace)
            {
                bool flash = (Time.unscaledTime % 1f) < 0.6f;
                UI.Text(new Rect(UI.W * 0.5f - 300, UI.H - 230, 600, 50), flash ? "BOX BOX — bu tur pite gir" : "", UI.warn, UI.Gold);
            }

            // pit lane / service
            if (me.InPit) PitLanePanel(race, me);

            // DRS
            if (car.drsAvailable || car.drsOpen)
            {
                var r = new Rect(UI.W - 560, UI.H - 300, 130, 56);
                UI.Fill(r, car.drsOpen ? new Color(0.1f, 0.75f, 0.25f, 0.95f) : new Color(0.1f, 0.3f, 0.15f, 0.9f));
                UI.Text(r, car.drsOpen ? "DRS AÇIK" : "DRS [F]", UI.center, Color.white);
            }

            if (gm.pitMenuOpen && race.IsRace) PitMenu(race, me);

            // finished banner
            if (race.state == RaceState.Finished)
            {
                var r = new Rect(UI.W * 0.5f - 400, UI.H * 0.5f - 80, 800, 160);
                UI.Panel(r, true);
                UI.Text(new Rect(r.x, r.y + 20, r.width, 60), race.IsRace ? "DAMALI BAYRAK!" : "SEANS BİTTİ", UI.h1);
                var row = race.results?.Find(x => x.isPlayer);
                if (row != null) UI.Text(new Rect(r.x, r.y + 90, r.width, 50), row.dnf ? "Yarış dışı" : $"{row.position}. sıra", UI.center, UI.Gold);
            }

            if (race.state == RaceState.Running && race.raceTime < 8f)
                UI.Text(new Rect(UI.W * 0.5f - 500, UI.H - 60, 1000, 40), "ESC: Duraklat/Garaj • P: Pit menüsü • B: Pite gir • C: Kamera • R: Piste dön", UI.center, new Color(1, 1, 1, 0.6f));
        }

        void TimingPanel(RaceManager race, RaceCar me)
        {
            var r = new Rect(24, 24, 430, race.IsRace ? 150 : 250);
            UI.Panel(r);
            if (race.IsRace)
            {
                UI.Text(new Rect(r.x + 18, r.y + 8, 200, 64), $"P{me.position}", UI.h1, UI.Gold);
                UI.Text(new Rect(r.x + 130, r.y + 16, 140, 50), $"/{race.cars.Count}", UI.h2, UI.Dim);
                int lap = Mathf.Clamp(me.lapsCompleted + 1, 1, race.TotalLaps);
                UI.Text(new Rect(r.x + 240, r.y + 10, 180, 30), "TUR", UI.small, UI.Dim);
                UI.Text(new Rect(r.x + 240, r.y + 30, 180, 44), $"{lap}/{race.TotalLaps}", UI.h2);
                UI.Text(new Rect(r.x + 18, r.y + 78, 200, 28), "Tur süresi", UI.small, UI.Dim);
                UI.Text(new Rect(r.x + 18, r.y + 100, 200, 34), race.state == RaceState.Running ? RaceManager.FormatTime(me.CurrentLapTime(race.raceTime)) : "--", UI.labelBold);
                UI.Text(new Rect(r.x + 220, r.y + 78, 200, 28), "Son / En iyi", UI.small, UI.Dim);
                UI.Text(new Rect(r.x + 220, r.y + 100, 200, 34),
                    $"{(me.lastLapTime > 0 ? RaceManager.FormatTime(me.lastLapTime) : "--")}  <color=#c6f>{(me.bestLapTime < 1e6f ? RaceManager.FormatTime(me.bestLapTime) : "")}</color>", UI.small);
            }
            else
            {
                bool timed = me.totalDistance > 0f;
                UI.Text(new Rect(r.x + 18, r.y + 8, 400, 30), timed ? $"TUR {me.lapsCompleted + 1}" + (me.lapInvalid ? "  <color=#f55>GEÇERSİZ</color>" : "") : "ÇIKIŞ TURU", UI.labelBold);
                UI.Text(new Rect(r.x + 18, r.y + 36, 400, 56), timed ? RaceManager.FormatTime(me.CurrentLapTime(race.raceTime)) : "--:--.---", UI.h1, me.lapInvalid ? UI.Bad : Color.white);
                if (race.ghost != null && race.ghost.TryDelta(out float delta))
                    UI.Text(new Rect(r.x + 250, r.y + 44, 170, 44), (delta >= 0 ? "+" : "−") + Mathf.Abs(delta).ToString("0.00"), UI.h2, delta <= 0 ? UI.Good : UI.Bad);
                UI.Text(new Rect(r.x + 18, r.y + 96, 400, 28), $"Son: {(me.lastLapTime > 0 ? RaceManager.FormatTime(me.lastLapTime) : "--")}", UI.small);
                UI.Text(new Rect(r.x + 18, r.y + 122, 400, 28), $"Seans en iyi: <color=#c6f>{(race.bestSessionLap < 1e6f ? RaceManager.FormatTime(race.bestSessionLap) : "--")}</color>", UI.small);
                float pb = SaveSystem.BestLap(race.track.def.id);
                UI.Text(new Rect(r.x + 18, r.y + 148, 400, 28), $"Rekor: <color=#fd5>{(pb < 1e6f ? RaceManager.FormatTime(pb) : "--")}</color>", UI.small);
                // sectors
                for (int k = 0; k < 3; k++)
                {
                    float st = me.lastSectorTimes[k];
                    Color c = Color.white;
                    if (st > 0f && Mathf.Abs(st - race.bestSectors[k]) < 0.0005f) c = UI.Purple;
                    var sr = new Rect(r.x + 18 + k * 136, r.y + 186, 128, 48);
                    UI.Fill(sr, new Color(c.r, c.g, c.b, 0.25f));
                    UI.Text(new Rect(sr.x + 6, sr.y, 120, 22), $"S{k + 1}", UI.tiny, UI.Dim);
                    UI.Text(new Rect(sr.x + 6, sr.y + 20, 120, 26), st > 0f ? st.ToString("0.000") : "--", UI.small, c);
                }
            }
        }

        void Tower(RaceManager race, RaceCar me)
        {
            var order = race.Order;
            int n = order.Count;
            float rowH = n > 16 ? 27f : 32f;
            var r = new Rect(24, 186, 330, 12 + n * rowH);
            UI.Panel(r);
            for (int i = 0; i < n; i++)
            {
                var rc = order[i];
                float y = r.y + 6 + i * rowH;
                if (rc.isPlayer) UI.Fill(new Rect(r.x + 4, y, r.width - 8, rowH - 2), new Color(0.85f, 0.1f, 0.15f, 0.4f));
                UI.Text(new Rect(r.x + 10, y, 34, rowH), (i + 1).ToString(), UI.small);
                UI.Fill(new Rect(r.x + 44, y + 5, 5, rowH - 10), rc.color);
                UI.Text(new Rect(r.x + 56, y, 70, rowH), rc.shortName, UI.small);
                UI.Dot(new Vector2(r.x + 140, y + rowH * 0.5f), rowH * 0.62f, Tyres.Color(rc.car.compound));
                string gap;
                if (rc.retired) gap = "DNF";
                else if (rc.InPit) gap = "PİT";
                else if (i == 0) gap = rc.finished ? "BİTİRDİ" : "Lider";
                else if (rc.lapsDownToLeader > 0 && !rc.finished) gap = $"+{rc.lapsDownToLeader} TUR";
                else gap = "+" + rc.gapLeader.ToString("0.0");
                UI.Text(new Rect(r.x + 160, y, 160, rowH), gap, UI.right, rc.InPit ? UI.Mid : Color.white);
            }
        }

        void Minimap(RaceManager race, RaceCar me)
        {
            var w = race.world;
            if (w == null || w.minimap == null) return;
            float size = 300;
            var r = new Rect(UI.W - size - 24, 24, size, size);
            UI.Panel(r);
            var inner = new Rect(r.x + 8, r.y + 8, size - 16, size - 16);
            GUI.DrawTexture(inner, w.minimap);
            foreach (var rc in race.cars)
            {
                if (rc.isPlayer) continue;
                Vector2 m = w.ToMap(rc.car.transform.position);
                UI.Dot(new Vector2(inner.x + m.x * inner.width, inner.y + (1f - m.y) * inner.height), 13, rc.color);
            }
            Vector2 pm = w.ToMap(me.car.transform.position);
            Vector2 pp = new Vector2(inner.x + pm.x * inner.width, inner.y + (1f - pm.y) * inner.height);
            UI.Dot(pp, 22, Color.white);
            UI.Dot(pp, 15, me.color);
            UI.Text(new Rect(r.x, r.y + size + 4, size, 26), race.track.def.name, UI.center, UI.Dim);
        }

        void Dash(RaceManager race, RaceCar me, CarController car)
        {
            var r = new Rect(UI.W - 420, UI.H - 230, 396, 206);
            UI.Panel(r);
            // shift lights
            float rpm01 = Mathf.InverseLerp(9000f, VehicleSpec.MaxRpm, car.rpm);
            for (int i = 0; i < 10; i++)
            {
                Color c = i < 4 ? UI.Good : i < 7 ? UI.Bad : UI.Purple;
                bool on = rpm01 * 10f > i + 0.5f;
                if (car.rpm > VehicleSpec.MaxRpm && (Time.unscaledTime % 0.2f) < 0.1f) on = false;
                UI.Fill(new Rect(r.x + 18 + i * 36, r.y + 12, 30, 12), on ? c : new Color(c.r, c.g, c.b, 0.15f));
            }
            UI.Bar(new Rect(r.x + 18, r.y + 32, 360, 8), car.rpm / VehicleSpec.LimiterRpm, new Color(0.9f, 0.9f, 0.95f));
            UI.Text(new Rect(r.x + 10, r.y + 44, 220, 90), $"{car.SpeedKmh:0}", UI.big, Color.white);
            UI.Text(new Rect(r.x + 236, r.y + 96, 80, 30), "km/sa", UI.small, UI.Dim);
            string gear = car.gear < 0 ? "R" : car.gear == 0 ? "N" : car.gear.ToString();
            UI.Text(new Rect(r.x + 280, r.y + 40, 100, 100), gear, UI.huge, car.shiftTimer > 0 ? UI.Gold : Color.white);
            // pedals
            UI.Bar(new Rect(r.x + 18, r.y + 146, 170, 14), car.throttleApplied, UI.Good);
            UI.Bar(new Rect(r.x + 208, r.y + 146, 170, 14), car.brakeApplied, UI.Bad);
            UI.Text(new Rect(r.x + 18, r.y + 164, 360, 30),
                $"{car.rpm:0} dev/dk   {(GameSettings.Current.assists.autoGearbox ? "OTO" : "MAN")}   {(me.InPit ? "<color=#fd5>LİMİTÖR</color>" : "")}", UI.tiny, UI.Dim);
        }

        void TyreFuel(RaceManager race, RaceCar me, CarController car)
        {
            var r = new Rect(24, UI.H - 250, 400, 226);
            UI.Panel(r);
            UI.Text(new Rect(r.x + 16, r.y + 8, 260, 30), "LASTİKLER", UI.labelBold);
            UI.Dot(new Vector2(r.x + 175, r.y + 23), 26, Tyres.Color(car.compound));
            UI.Text(new Rect(r.x + 195, r.y + 8, 200, 30), Tyres.Name(car.compound), UI.small);
            // car outline with four tyres
            float cx = r.x + 90, cy = r.y + 120;
            UI.Fill(new Rect(cx - 10, cy - 60, 20, 120), new Color(1, 1, 1, 0.25f));
            UI.Fill(new Rect(cx - 34, cy - 70, 68, 8), new Color(1, 1, 1, 0.25f));
            UI.Fill(new Rect(cx - 30, cy + 58, 60, 8), new Color(1, 1, 1, 0.25f));
            Vector2[] pos = { new Vector2(cx - 52, cy - 52), new Vector2(cx + 30, cy - 52), new Vector2(cx - 52, cy + 14), new Vector2(cx + 30, cy + 14) };
            for (int i = 0; i < 4; i++)
            {
                var w = car.wheels[i];
                var tr = new Rect(pos[i].x, pos[i].y, 22, 40);
                Color c = UI.WearColor(w.wear);
                if (w.locked || w.spinning) c = (Time.unscaledTime % 0.2f) < 0.1f ? Color.white : c;
                UI.Fill(tr, c);
                UI.Text(new Rect(i % 2 == 0 ? tr.x - 50 : tr.x + 26, tr.y + 8, 48, 24), $"{w.wear * 100f:0}%", UI.tiny, Color.white);
            }
            // fuel
            float perLap = Mathf.Max(me.fuelPerLap, 0.01f);
            UI.Text(new Rect(r.x + 200, r.y + 50, 190, 28), "YAKIT", UI.labelBold);
            UI.Text(new Rect(r.x + 200, r.y + 78, 190, 34), $"{car.fuel:0.0} kg", UI.h2, car.fuel < perLap * 1.4f && race.IsRace && car.fuelMultiplier > 0 ? UI.Bad : Color.white);
            if (race.IsRace && car.fuelMultiplier > 0f)
                UI.Text(new Rect(r.x + 200, r.y + 112, 190, 26), $"≈ {car.fuel / perLap:0.0} tur", UI.small, UI.Dim);
            UI.Bar(new Rect(r.x + 200, r.y + 140, 180, 10), car.fuel / VehicleSpec.FuelTankKg, new Color(0.3f, 0.7f, 1f));
            if (race.IsRace)
            {
                string plan = me.boxRequested ? $"<color=#fd5>BOX:</color> {Tyres.Name(me.planCompound)} +{me.planFuelAdd:0} kg" : "Pit: [P] plan  [B] box";
                UI.Text(new Rect(r.x + 200, r.y + 160, 196, 60), plan, UI.tiny, Color.white);
            }
        }

        void PitLanePanel(RaceManager race, RaceCar me)
        {
            var r = new Rect(UI.W * 0.5f - 360, UI.H - 260, 720, 150);
            UI.Panel(r, true);
            string phase = me.pit switch
            {
                PitPhase.Entering => "PİT ŞERİDİNE GİRİŞ",
                PitPhase.ToBox => "KUTUYA GİDİLİYOR — 80 km/sa",
                PitPhase.Service => "PİT STOP",
                PitPhase.Leaving => "PİTTEN ÇIKILIYOR",
                PitPhase.Exiting => "PİSTE DÖNÜŞ",
                _ => ""
            };
            UI.Text(new Rect(r.x, r.y + 12, r.width, 40), phase, UI.center, UI.Gold);
            if (me.pit == PitPhase.Service)
            {
                UI.Bar(new Rect(r.x + 40, r.y + 62, r.width - 80, 22), me.serviceTimer / Mathf.Max(0.1f, me.serviceTotal), UI.Good);
                UI.Text(new Rect(r.x, r.y + 90, r.width, 30),
                    $"{me.serviceTimer:0.0} / {me.serviceTotal:0.0} sn • {Tyres.Name(me.planCompound)} lastik • +{me.planFuelAdd:0} kg yakıt" + (me.planWingDelta != 0 ? $" • ön kanat {me.planWingDelta:+0;-0}" : ""), UI.center, Color.white);
                if (!string.IsNullOrEmpty(me.serviceNote)) UI.Text(new Rect(r.x, r.y + 118, r.width, 28), me.serviceNote, UI.center, UI.Bad);
            }
            else
            {
                UI.Text(new Rect(r.x, r.y + 62, r.width, 30), $"Plan: {Tyres.Name(me.planCompound)} lastik, +{me.planFuelAdd:0} kg yakıt", UI.center, Color.white);
            }
        }

        // ------------------------------------------------------------------ pit strategy menu (live, not paused)
        void PitMenu(RaceManager race, RaceCar me)
        {
            var car = me.car;
            var r = new Rect(UI.W - 560, 360, 536, 500);
            UI.Panel(r, true);
            float x = r.x + 20, w = r.width - 40, y = r.y + 12;
            UI.Text(new Rect(x, y, w, 40), "PİT STRATEJİSİ", UI.h2, UI.Gold);
            UI.Text(new Rect(x + 300, y + 6, 200, 30), "[P] kapat", UI.right, UI.Dim);
            y += 46;
            int remaining = race.LapsRemaining(me);
            UI.Text(new Rect(x, y, w, 28), $"Kalan tur: {remaining} • Lastik: {Tyres.Name(car.compound)} %{car.MaxTyreWear * 100f:0} • Yakıt: {car.fuel:0} kg (~{car.fuel / Mathf.Max(me.fuelPerLap, 0.01f):0.0} tur)", UI.tiny);
            y += 34;

            // keyboard shortcuts (IMGUI events work with both input back-ends)
            var e = Event.current;
            if (e.type == EventType.KeyDown)
            {
                bool used = true;
                switch (e.keyCode)
                {
                    case KeyCode.Alpha1: case KeyCode.Keypad1: me.planCompound = TyreCompound.Soft; break;
                    case KeyCode.Alpha2: case KeyCode.Keypad2: me.planCompound = TyreCompound.Medium; break;
                    case KeyCode.Alpha3: case KeyCode.Keypad3: me.planCompound = TyreCompound.Hard; break;
                    case KeyCode.Alpha4: case KeyCode.Keypad4: me.planFuelAdd = Mathf.Max(0f, me.planFuelAdd - 5f); break;
                    case KeyCode.Alpha5: case KeyCode.Keypad5: me.planFuelAdd = Mathf.Min(VehicleSpec.FuelTankKg - car.fuel, me.planFuelAdd + 5f); break;
                    case KeyCode.Alpha6: case KeyCode.Keypad6: me.planWingDelta = Mathf.Max(-3, me.planWingDelta - 1); break;
                    case KeyCode.Alpha7: case KeyCode.Keypad7: me.planWingDelta = Mathf.Min(3, me.planWingDelta + 1); break;
                    default: used = false; break;
                }
                if (used) { me.planEdited = true; gm.Click(); e.Use(); }
            }

            UI.Text(new Rect(x, y, w, 30), "Lastik hamuru  [1] [2] [3]", UI.labelBold);
            y += 34;
            for (int i = 0; i < 3; i++)
            {
                var c = (TyreCompound)i;
                var br = new Rect(x + i * 168, y, 158, 48);
                if (UI.Button(br, "    " + Tyres.Name(c), me.planCompound == c)) { me.planCompound = c; me.planEdited = true; }
                UI.Dot(new Vector2(br.x + 26, br.y + 24), 26, Tyres.Color(c));
            }
            y += 60;
            float maxAdd = Mathf.Max(0f, VehicleSpec.FuelTankKg - car.fuel);
            UI.Text(new Rect(x, y, w, 30), $"Eklenecek yakıt  [4] −  [5] +   <color=#fd5>{me.planFuelAdd:0} kg</color> (≈{me.planFuelAdd / Mathf.Max(me.fuelPerLap, 0.01f):0.0} tur)", UI.labelBold);
            y += 34;
            float nf = GUI.HorizontalSlider(new Rect(x, y + 6, w, 20), me.planFuelAdd, 0f, maxAdd);
            if (Mathf.Abs(nf - me.planFuelAdd) > 0.01f) { me.planFuelAdd = Mathf.Round(nf); me.planEdited = true; }
            y += 38;
            UI.Text(new Rect(x, y, w, 30), $"Ön kanat ayarı  [6] −  [7] +   <color=#fd5>{me.planWingDelta:+0;-0;0}</color>  (şu an {car.setup.frontWing:0})", UI.labelBold);
            y += 44;
            race.SuggestPitPlan(me, out var sc, out var sf);
            UI.Text(new Rect(x, y, w, 28), $"Mühendis önerisi: {Tyres.Name(sc)} + {sf:0} kg", UI.small, UI.Dim);
            y += 34;
            if (UI.Button(new Rect(x, y, 230, 48), "ÖNERİYİ UYGULA")) { me.planCompound = sc; me.planFuelAdd = Mathf.Round(sf); me.planEdited = true; }
            if (UI.Button(new Rect(x + 250, y, w - 250, 48), me.boxRequested ? "BOX İPTAL [B]" : "BU TUR PİTE GİR [B]", me.boxRequested))
            {
                me.boxRequested = !me.boxRequested;
                race.ShowEvent(me.boxRequested ? "PİT İSTEĞİ: bu tur kutuya gir (BOX BOX)" : "Pit isteği iptal edildi", 2.5f);
            }
            y += 60;
            UI.Text(new Rect(x, y, w, 60), "Pit girişinde araç otomatik olarak kutuya gider. İstek vermesen de pit girişinden şeride girersen bu plan uygulanır.", UI.tiny, UI.Dim);
        }

        // ------------------------------------------------------------------ pause
        void PauseMenu()
        {
            var race = gm.race;
            var r = new Rect(UI.W * 0.5f - 300, UI.H * 0.5f - 330, 600, 660);
            UI.Panel(r, true);
            UI.Text(new Rect(r.x, r.y + 16, r.width, 50), "DURAKLATILDI", UI.center, UI.Gold);
            float y = r.y + 80;
            float x = r.x + 60, w = r.width - 120;
            if (UI.Button(new Rect(x, y, w, 60), "DEVAM ET", true)) gm.Resume();
            y += 72;
            if (UI.Button(new Rect(x, y, w, 60), "GARAJ (ayarlar anında uygulanır)")) gm.OpenGarage();
            y += 72;
            if (race != null && race.IsRace)
            {
                if (UI.Button(new Rect(x, y, w, 60), "PİT STRATEJİSİ")) { gm.Resume(); gm.pitMenuOpen = true; gm.PreparePitPlan(); }
                y += 72;
            }
            if (UI.Button(new Rect(x, y, w, 60), "AYARLAR")) gm.OpenSettings();
            y += 72;
            if (UI.Button(new Rect(x, y, w, 60), "YENİDEN BAŞLAT")) gm.RestartSession();
            y += 72;
            if (race != null && race.IsRace)
            {
                if (UI.Button(new Rect(x, y, w, 60), "YARIŞTAN ÇEKİL (DNF)")) gm.RetireFromRace();
                y += 72;
            }
            if (UI.Button(new Rect(x, y, w, 60), "ANA MENÜ")) gm.EndSession(true);
        }
    }
}
