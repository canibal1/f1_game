using UnityEngine;

namespace F1Game
{
    public partial class GameUI
    {
        // ------------------------------------------------------------------ race results
        void Results()
        {
            var rows = gm.lastResults;
            if (rows == null) { gm.ContinueFromResults(); return; }
            bool champ = gm.session == SessionKind.Championship;
            var t = GameData.Tracks[gm.lastRoundTrack];
            var me = rows.Find(x => x.isPlayer);
            Header("YARIŞ SONUCU — " + t.name, me != null ? (me.dnf ? "Yarışı tamamlayamadın." : $"{me.position}. sırada bitirdin" + (champ ? $" ve {me.points} puan kazandın." : ".")) : null);

            var r = new Rect(40, 130, UI.W - 80, UI.H - 260);
            UI.Panel(r);
            float x = r.x + 20;
            float[] col = { 0, 70, 420, 760, 1000, 1200, 1310, 1420 };
            string[] head = { "SIRA", "PİLOT", "TAKIM", "SÜRE / FARK", "EN İYİ TUR", "PİT", "PUAN", "" };
            for (int i = 0; i < head.Length; i++) UI.Text(new Rect(x + col[i], r.y + 10, 300, 30), head[i], UI.small, UI.Dim);
            float leaderTime = rows.Count > 0 ? rows[0].time : 0f;
            int leaderLaps = rows.Count > 0 ? rows[0].laps : 0;
            float rowH = Mathf.Min(38f, (r.height - 60f) / Mathf.Max(1, rows.Count));
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                float y = r.y + 46 + i * rowH;
                if (row.isPlayer) UI.Fill(new Rect(r.x + 6, y, r.width - 12, rowH - 2), new Color(0.85f, 0.1f, 0.15f, 0.35f));
                Color pc = i == 0 ? UI.Gold : i == 1 ? new Color(0.85f, 0.85f, 0.9f) : i == 2 ? new Color(0.85f, 0.55f, 0.3f) : Color.white;
                UI.Text(new Rect(x + col[0], y, 60, rowH), row.dnf ? "—" : row.position.ToString(), UI.labelBold, pc);
                UI.Fill(new Rect(x + col[1] - 12, y + 6, 6, rowH - 12), GameData.Teams[row.team].primary);
                UI.Text(new Rect(x + col[1], y, 340, rowH), row.name, UI.label);
                UI.Text(new Rect(x + col[2], y, 330, rowH), GameData.Teams[row.team].name, UI.small, UI.Dim);
                string time;
                if (row.dnf) time = "Yarış dışı (DNF)";
                else if (i == 0) time = RaceManager.FormatTime(row.time);
                else if (row.laps < leaderLaps) time = $"+{leaderLaps - row.laps} tur";
                else time = "+" + (row.time - leaderTime).ToString("0.000") + " sn";
                UI.Text(new Rect(x + col[3], y, 230, rowH), time, UI.label);
                UI.Text(new Rect(x + col[4], y, 190, rowH), row.bestLap < 1e6f ? RaceManager.FormatTime(row.bestLap) : "--", UI.label, row.fastestLap ? UI.Purple : Color.white);
                UI.Text(new Rect(x + col[5], y, 100, rowH), row.pitStops.ToString(), UI.label);
                UI.Text(new Rect(x + col[6], y, 100, rowH), row.points > 0 ? "+" + row.points : "", UI.labelBold, UI.Gold);
                if (row.fastestLap) UI.Text(new Rect(x + col[7], y, 200, rowH), "en hızlı tur", UI.tiny, UI.Purple);
            }
            if (UI.Button(new Rect(UI.W - 420, UI.H - 110, 380, 70), champ ? "PUAN DURUMU »" : "ANA MENÜ »", true)) gm.ContinueFromResults();
        }

        // ------------------------------------------------------------------ standings
        void Standings()
        {
            var st = Championship.State;
            Header($"PUAN DURUMU — {st.round}/{st.Rounds} yarış tamamlandı", $"{st.playerName}: {Championship.PlayerStanding()}. sıra, {st.driverPoints[st.playerSeat]} puan");
            var left = new Rect(40, 130, 1100, UI.H - 260);
            UI.Panel(left);
            UI.Text(new Rect(left.x + 20, left.y + 10, 600, 40), "PİLOTLAR", UI.h2);
            UI.Text(new Rect(left.x + 760, left.y + 16, 320, 30), "Galibiyet / Podyum / Puan", UI.small, UI.Dim);
            var ds = Championship.DriverStandings();
            float rowH = Mathf.Min(38f, (left.height - 70f) / Mathf.Max(1, ds.Count));
            for (int i = 0; i < ds.Count; i++)
            {
                int seat = ds[i];
                float y = left.y + 56 + i * rowH;
                if (seat == st.playerSeat) UI.Fill(new Rect(left.x + 6, y, left.width - 12, rowH - 2), new Color(0.85f, 0.1f, 0.15f, 0.35f));
                UI.Text(new Rect(left.x + 20, y, 50, rowH), (i + 1).ToString(), UI.labelBold, i == 0 ? UI.Gold : Color.white);
                int team = GameData.Drivers[seat].team;
                UI.Fill(new Rect(left.x + 70, y + 6, 6, rowH - 12), GameData.Teams[team].primary);
                UI.Text(new Rect(left.x + 86, y, 330, rowH), Championship.SeatName(seat), UI.label);
                UI.Text(new Rect(left.x + 420, y, 330, rowH), GameData.Teams[team].name, UI.small, UI.Dim);
                UI.Text(new Rect(left.x + 760, y, 200, rowH), $"{st.wins[seat]} / {st.podiums[seat]}", UI.label);
                UI.Text(new Rect(left.x + 960, y, 120, rowH), st.driverPoints[seat].ToString(), UI.right, UI.Gold);
            }

            var right = new Rect(1160, 130, UI.W - 1200, UI.H - 260);
            UI.Panel(right);
            UI.Text(new Rect(right.x + 20, right.y + 10, 500, 40), "TAKIMLAR", UI.h2);
            var ts = Championship.TeamStandings();
            for (int i = 0; i < ts.Count; i++)
            {
                int team = ts[i];
                float y = right.y + 56 + i * 44;
                if (team == st.playerTeam) UI.Fill(new Rect(right.x + 6, y, right.width - 12, 40), new Color(0.85f, 0.1f, 0.15f, 0.35f));
                UI.Text(new Rect(right.x + 20, y, 50, 40), (i + 1).ToString(), UI.labelBold);
                UI.Fill(new Rect(right.x + 64, y + 8, 18, 24), GameData.Teams[team].primary);
                UI.Fill(new Rect(right.x + 84, y + 8, 6, 24), GameData.Teams[team].secondary);
                UI.Text(new Rect(right.x + 100, y, 360, 40), GameData.Teams[team].name, UI.label);
                UI.Text(new Rect(right.x + right.width - 140, y, 120, 40), st.teamPoints[team].ToString(), UI.right, UI.Gold);
            }

            if (st.finished)
            {
                if (UI.Button(new Rect(UI.W - 420, UI.H - 110, 380, 70), "SEZON SONU »", true)) gm.GoTo(GameState.SeasonEnd);
            }
            else
            {
                if (UI.Button(new Rect(40, UI.H - 110, 300, 70), "ANA MENÜ")) gm.GoTo(GameState.MainMenu);
                if (UI.Button(new Rect(UI.W - 420, UI.H - 110, 380, 70), "SIRADAKİ YARIŞ »", true))
                {
                    gm.selectedTrack = st.CurrentTrack;
                    gm.GoTo(GameState.ChampionshipHub);
                }
            }
        }

        // ------------------------------------------------------------------ season end / trophy
        void SeasonEnd()
        {
            var st = Championship.State;
            var ds = Championship.DriverStandings();
            int champ = ds[0];
            int myPos = Championship.PlayerStanding();
            bool won = champ == st.playerSeat;
            UI.Fill(new Rect(0, 0, UI.W, 200), new Color(0, 0, 0, 0.55f));
            UI.Text(new Rect(0, 30, UI.W, 80), won ? "DÜNYA ŞAMPİYONU!" : "SEZON TAMAMLANDI", UI.huge, won ? UI.Gold : Color.white);
            UI.Text(new Rect(0, 130, UI.W, 50), won
                ? $"Tebrikler {st.playerName}! {st.driverPoints[st.playerSeat]} puanla şampiyonluk kupası senin."
                : $"Şampiyon: {Championship.SeatName(champ)} ({st.driverPoints[champ]} puan) • Sen {myPos}. oldun ({st.driverPoints[st.playerSeat]} puan)", UI.center, Color.white);

            var r = new Rect(UI.W - 640, UI.H - 520, 600, 380);
            UI.Panel(r);
            UI.Text(new Rect(r.x + 20, r.y + 10, 560, 40), "PODYUM", UI.h2, UI.Gold);
            for (int i = 0; i < Mathf.Min(3, ds.Count); i++)
            {
                int seat = ds[i];
                UI.Text(new Rect(r.x + 20, r.y + 60 + i * 50, 560, 44), $"{i + 1}. {Championship.SeatName(seat)} — {st.driverPoints[seat]} p", UI.h2, i == 0 ? UI.Gold : Color.white);
            }
            var tsTeam = Championship.TeamStandings();
            UI.Text(new Rect(r.x + 20, r.y + 230, 560, 34), $"Takımlar şampiyonu: {GameData.Teams[tsTeam[0]].name}", UI.label);
            UI.Text(new Rect(r.x + 20, r.y + 270, 560, 34), $"Senin sezonun: {st.wins[st.playerSeat]} galibiyet, {st.podiums[st.playerSeat]} podyum", UI.label);
            if (UI.Button(new Rect(UI.W - 420, UI.H - 110, 380, 70), "ANA MENÜ", true))
            {
                SaveSystem.Data.hasChampionship = false;
                SaveSystem.Save();
                gm.GoTo(GameState.MainMenu);
            }
        }
    }
}
