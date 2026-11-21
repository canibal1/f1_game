using UnityEngine;

namespace F1Game
{
    public partial class GameUI
    {
        // ------------------------------------------------------------------ garage (car setup)
        void Garage()
        {
            var s = gm.editingSetup;
            if (s == null) { gm.CloseGarage(); return; }
            var before = gm.setupBeforeEdit ?? s;
            bool inRace = gm.race != null;
            string trackName = GameData.Tracks[inRace ? gm.race.config.trackIndex : gm.selectedTrack].name;
            UI.Fill(new Rect(0, 0, UI.W, UI.H), new Color(0, 0, 0, inRace ? 0.55f : 0.25f));
            Header("GARAJ — " + trackName, inRace
                ? "KAYDET dediğinde ayarlar araca ANINDA uygulanır; oyuna dönünce farkı hissedersin."
                : "Her pist için ayrı ayar kaydedilir. Yarışta ESC → Garaj ile de değiştirebilirsin.");

            if (!inRace)
            {
                // track selector for editing setups from the main menu
                float bx = 60;
                for (int i = 0; i < GameData.Tracks.Length; i++)
                {
                    string label = GameData.Tracks[i].name.Split(' ')[0];
                    if (UI.Button(new Rect(bx, 122, 190, 40), label, gm.selectedTrack == i) && gm.selectedTrack != i)
                    {
                        gm.SelectTrack(i);
                        gm.editingSetup = SaveSystem.GetSetup(GameData.Tracks[i].id);
                        gm.setupBeforeEdit = gm.editingSetup.Clone();
                        gm.ShowShowroom(i, false, true);
                        return;
                    }
                    bx += 200;
                }
            }

            var left = new Rect(40, 175, 980, UI.H - 300);
            UI.Panel(left);
            float x = left.x + 24, w = left.width - 48, y = left.y + 14;
            UI.Text(new Rect(x, y, w, 36), "AERODİNAMİK", UI.h2, UI.Gold);
            y += 42;
            s.frontWing = Mathf.Round(UI.Slider(new Rect(x, y, w, 56), "Ön kanat açısı", s.frontWing, CarSetup.WingMin, CarSetup.WingMax, $"{s.frontWing:0}",
                "Yüksek = virajlara daha keskin dönüş (ön tutuş), az ek sürtünme."));
            y += 60;
            s.rearWing = Mathf.Round(UI.Slider(new Rect(x, y, w, 56), "Arka kanat açısı", s.rearWing, CarSetup.WingMin, CarSetup.WingMax, $"{s.rearWing:0}",
                "Yüksek = arka stabilite ve çekiş, ama çok daha fazla sürtünme → düşük son hız."));
            y += 64;
            UI.Text(new Rect(x, y, w, 36), "SÜSPANSİYON", UI.h2, UI.Gold);
            y += 42;
            s.frontSuspension = Mathf.Round(UI.Slider(new Rect(x, y, w, 56), "Ön süspansiyon sertliği", s.frontSuspension, CarSetup.SuspMin, CarSetup.SuspMax, $"{s.frontSuspension:0}",
                $"Yay {s.FrontSpring / 1000f:0} kN/m. Sert ön = daha az yalpa ama daha çok önden kayma (understeer)."));
            y += 60;
            s.rearSuspension = Mathf.Round(UI.Slider(new Rect(x, y, w, 56), "Arka süspansiyon sertliği", s.rearSuspension, CarSetup.SuspMin, CarSetup.SuspMax, $"{s.rearSuspension:0}",
                $"Yay {s.RearSpring / 1000f:0} kN/m. Sert arka = çevik ama arka kayabilir (oversteer); yumuşak = daha çok çekiş."));
            y += 60;
            s.rideHeight = Mathf.Round(UI.Slider(new Rect(x, y, w, 56), "Sürüş yüksekliği", s.rideHeight, CarSetup.RideMin, CarSetup.RideMax, $"{s.PlankClearance * 1000f:0} mm",
                "Düşük = daha fazla zemin etkisi (downforce), fakat yüksek hızda dibe vurma ve sürtünme."));
            y += 64;
            UI.Text(new Rect(x, y, w, 36), "FREN & ŞANZIMAN", UI.h2, UI.Gold);
            y += 42;
            s.brakeBias = UI.Slider(new Rect(x, y, w, 56), "Fren dengesi (ön)", s.brakeBias, CarSetup.BiasMin, CarSetup.BiasMax, $"%{s.brakeBias:0.0}",
                "Öne fazla = ön kilitlenir; arkaya fazla = fren altında arka kayar.");
            y += 60;
            s.finalDrive = UI.Slider(new Rect(x, y, w, 56), "Son dişli oranı", s.finalDrive, CarSetup.FinalMin, CarSetup.FinalMax, $"{s.finalDrive:0.00}",
                "Yüksek (kısa) = hızlı ivmelenme, düşük (uzun) = yüksek son hız.");
            y += 60;
            s.firstGear = UI.Slider(new Rect(x, y, w, 56), "1. vites oranı", s.firstGear, CarSetup.FirstMin, CarSetup.FirstMax, $"{s.firstGear:0.00}",
                "Kalkış ve yavaş virajlardan çıkış.");
            y += 60;
            s.topGear = UI.Slider(new Rect(x, y, w, 56), "8. vites oranı", s.topGear, CarSetup.TopMin, CarSetup.TopMax, $"{s.topGear:0.00}",
                "Son vitesin boyu: ara vitesler 1. ve 8. arasında otomatik dağıtılır.");
            s.Clamp();

            // ---- analysis panel ----
            var right = new Rect(1040, 175, UI.W - 1080, UI.H - 300);
            UI.Panel(right);
            float rx = right.x + 24, rw = right.width - 48, ry = right.y + 14;
            UI.Text(new Rect(rx, ry, rw, 36), "PERFORMANS ANALİZİ", UI.h2, UI.Gold);
            ry += 46;
            float mass = 850f;
            float top = VehicleSpec.EstimateTopSpeed(s, mass) * 3.6f, top0 = VehicleSpec.EstimateTopSpeed(before, mass) * 3.6f;
            float df = VehicleSpec.Downforce(s, 69.4f) / 9.81f, df0 = VehicleSpec.Downforce(before, 69.4f) / 9.81f;
            float g100 = VehicleSpec.LateralGrip(s, 27.8f, mass, 0.978f) / 9.81f, g1000 = VehicleSpec.LateralGrip(before, 27.8f, mass, 0.978f) / 9.81f;
            float g250 = VehicleSpec.LateralGrip(s, 69.4f, mass, 0.978f) / 9.81f, g2500 = VehicleSpec.LateralGrip(before, 69.4f, mass, 0.978f) / 9.81f;
            ry = Stat(rx, ry, rw, "Son hız", top, top0, "km/sa", "0");
            ry = Stat(rx, ry, rw, "Downforce @250 km/sa", df, df0, "kg", "0");
            ry = Stat(rx, ry, rw, "Sürtünme (CdA)", s.CdA, before.CdA, "m²", "0.000", true);
            ry = Stat(rx, ry, rw, "Viraj tutuşu @100 km/sa", g100, g1000, "g", "0.00");
            ry = Stat(rx, ry, rw, "Viraj tutuşu @250 km/sa", g250, g2500, "g", "0.00");
            // balance bar
            float bal = s.AeroBalance;
            UI.Text(new Rect(rx, ry, 300, 30), "Aero denge (ön)", UI.labelBold);
            UI.Text(new Rect(rx + rw - 200, ry, 200, 30), $"%{bal * 100f:0.0}", UI.right, UI.Gold);
            ry += 32;
            UI.Bar(new Rect(rx, ry, rw, 14), Mathf.InverseLerp(0.38f, 0.52f, bal), new Color(0.3f, 0.7f, 1f));
            UI.Text(new Rect(rx, ry + 14, rw, 24), bal > 0.47f ? "Ön ağırlıklı: keskin dönüş, yüksek hızda arka hafifler (oversteer)" :
                bal < 0.43f ? "Arka ağırlıklı: stabil ama virajda önden kayma (understeer)" : "Dengeli", UI.tiny, UI.Dim);
            ry += 50;

            UI.Text(new Rect(rx, ry, rw, 30), "VİTES TABLOSU (12.500 dev/dk'da hız)", UI.labelBold);
            ry += 34;
            float colW = rw / 4f;
            for (int g = 1; g <= CarSetup.Gears; g++)
            {
                int c = (g - 1) % 4, row = (g - 1) / 4;
                float v = s.SpeedAtRpm(g, VehicleSpec.MaxRpm) * 3.6f;
                UI.Text(new Rect(rx + c * colW, ry + row * 30, colW, 28), $"{g}: <color=#fd5>{v:0}</color> km/sa", UI.small);
            }
            ry += 70;
            float limitTop = s.SpeedAtRpm(8, VehicleSpec.LimiterRpm) * 3.6f;
            if (limitTop < top + 2f) UI.Text(new Rect(rx, ry, rw, 26), "Uyarı: son viteste devir sınırına takılıyorsun — son oranı düşür ya da 8. vitesi uzat.", UI.tiny, UI.Bad);
            ry += 34;

            UI.Text(new Rect(rx, ry, rw, 30), "HAZIR AYARLAR", UI.labelBold);
            ry += 34;
            float pw = (rw - 20) / 3f;
            if (UI.Button(new Rect(rx, ry, pw, 46), "Düşük DF")) gm.editingSetup = CarSetup.Preset(CarSetupPreset.LowDownforce);
            if (UI.Button(new Rect(rx + pw + 10, ry, pw, 46), "Dengeli")) gm.editingSetup = CarSetup.Preset(CarSetupPreset.Balanced);
            if (UI.Button(new Rect(rx + 2 * (pw + 10), ry, pw, 46), "Yüksek DF")) gm.editingSetup = CarSetup.Preset(CarSetupPreset.HighDownforce);

            bool changed = JsonUtility.ToJson(s) != JsonUtility.ToJson(before);
            if (UI.Button(new Rect(40, UI.H - 110, 240, 70), "KAPAT")) gm.CloseGarage();
            if (UI.Button(new Rect(300, UI.H - 110, 260, 70), "GERİ AL")) gm.editingSetup = before.Clone();
            if (changed) UI.Text(new Rect(580, UI.H - 100, 600, 50), "Kaydedilmemiş değişiklikler var", UI.label, UI.Mid);
            if (UI.Button(new Rect(UI.W - 480, UI.H - 110, 440, 70), inRace ? "KAYDET VE ARACA UYGULA" : "KAYDET", true)) gm.SaveGarage();
        }

        float Stat(float x, float y, float w, string name, float v, float v0, string unit, string fmt, bool lowerIsBetter = false)
        {
            UI.Text(new Rect(x, y, w * 0.55f, 30), name, UI.labelBold);
            float d = v - v0;
            string delta = "";
            if (Mathf.Abs(d) > 0.0005f)
            {
                bool good = lowerIsBetter ? d < 0 : d > 0;
                string col = good ? "#5e6" : "#f66";
                delta = $"  <size=16><color={col}>({(d > 0 ? "+" : "")}{d.ToString(fmt)})</color></size>";
            }
            UI.Text(new Rect(x + w * 0.45f, y, w * 0.55f, 30), $"{v.ToString(fmt)} {unit}{delta}", UI.right, Color.white);
            return y + 34;
        }
    }
}
