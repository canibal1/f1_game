using System;
using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    [Serializable]
    public class RoundResult
    {
        public int trackIndex;
        public List<int> order = new List<int>();   // seats in finishing order
        public List<int> points = new List<int>();
        public List<bool> dnf = new List<bool>();
        public int fastestSeat = -1;
    }

    [Serializable]
    public class ChampionshipState
    {
        public string playerName = "Oyuncu";
        public int playerTeam;
        public int playerSeat = 1;
        public List<int> calendar = new List<int>();
        public List<int> seats = new List<int>();
        public int round;
        public int laps = 5;
        public int[] driverPoints = new int[20];
        public int[] wins = new int[20];
        public int[] podiums = new int[20];
        public int[] teamPoints = new int[10];
        public List<RoundResult> history = new List<RoundResult>();
        public bool finished;

        public bool IsActive => calendar != null && calendar.Count > 0 && !finished;
        public int Rounds => calendar.Count;
        public int CurrentTrack => calendar[Mathf.Clamp(round, 0, calendar.Count - 1)];
    }

    /// <summary>Season logic: calendar, grid, points (25-18-15-…, +1 fastest lap), standings and the title.</summary>
    public static class Championship
    {
        public static ChampionshipState State => SaveSystem.Data.championship;

        public static void StartNew(string playerName, int team, int laps, int gridSize)
        {
            var st = new ChampionshipState
            {
                playerName = string.IsNullOrWhiteSpace(playerName) ? "Oyuncu" : playerName.Trim(),
                playerTeam = team,
                laps = laps,
            };
            var teamSeats = GameData.DriversOfTeam(team);
            st.playerSeat = teamSeats[teamSeats.Count > 1 ? 1 : 0];
            for (int i = 0; i < GameData.Tracks.Length; i++) st.calendar.Add(i);
            st.seats = SeatsForGrid(gridSize, st.playerSeat);
            SaveSystem.Data.championship = st;
            SaveSystem.Data.hasChampionship = true;
            SaveSystem.Save();
        }

        public static List<int> SeatsForGrid(int gridSize, int playerSeat)
        {
            var seats = new List<int>();
            if (gridSize >= GameData.Drivers.Length)
            {
                for (int i = 0; i < GameData.Drivers.Length; i++) seats.Add(i);
                return seats;
            }
            int playerTeam = GameData.Drivers[playerSeat].team;
            for (int t = 0; t < GameData.Teams.Length && seats.Count < gridSize; t++)
            {
                var ds = GameData.DriversOfTeam(t);
                seats.Add(t == playerTeam ? playerSeat : ds[0]);
            }
            // fill remaining with second drivers if the grid is larger than one car per team
            for (int t = 0; t < GameData.Teams.Length && seats.Count < gridSize; t++)
            {
                foreach (var d in GameData.DriversOfTeam(t)) if (!seats.Contains(d) && seats.Count < gridSize) seats.Add(d);
            }
            if (!seats.Contains(playerSeat)) seats[seats.Count - 1] = playerSeat;
            return seats;
        }

        /// <summary>Starting grid: AI ordered by a simulated qualifying (skill × car + luck), player placed by the setting.</summary>
        public static List<int> MakeGrid(List<int> seats, int playerSeat, StartPosition where, int seed)
        {
            var rnd = new System.Random(seed);
            var ai = new List<int>();
            foreach (var s in seats) if (s != playerSeat) ai.Add(s);
            var score = new Dictionary<int, float>();
            foreach (var s in ai)
            {
                var d = GameData.Drivers[s];
                score[s] = d.skill * GameData.Teams[d.team].performance + (float)rnd.NextDouble() * 0.02f;
            }
            ai.Sort((a, b) => score[b].CompareTo(score[a]));
            int pos;
            switch (where)
            {
                case StartPosition.Pole: pos = 0; break;
                case StartPosition.Back: pos = ai.Count; break;
                case StartPosition.Middle: pos = ai.Count / 2; break;
                default: pos = rnd.Next(ai.Count + 1); break;
            }
            ai.Insert(Mathf.Clamp(pos, 0, ai.Count), playerSeat);
            return ai;
        }

        public static string SeatName(int seat)
        {
            var st = State;
            if (st != null && st.calendar != null && st.calendar.Count > 0 && seat == st.playerSeat) return st.playerName;
            return GameData.Drivers[seat].name;
        }

        public static void ApplyResults(List<ResultRow> rows)
        {
            var st = State;
            var rr = new RoundResult { trackIndex = st.CurrentTrack };
            foreach (var r in rows)
            {
                rr.order.Add(r.seat);
                rr.points.Add(r.points);
                rr.dnf.Add(r.dnf);
                if (r.fastestLap) rr.fastestSeat = r.seat;
                st.driverPoints[r.seat] += r.points;
                st.teamPoints[r.team] += r.points;
                if (!r.dnf && r.position == 1) st.wins[r.seat]++;
                if (!r.dnf && r.position <= 3) st.podiums[r.seat]++;
            }
            st.history.Add(rr);
            st.round++;
            if (st.round >= st.calendar.Count)
            {
                st.finished = true;
                int champ = DriverStandings()[0];
                string trophy = $"{DateTime.Now:yyyy-MM-dd} Sezon — Şampiyon: {SeatName(champ)} | Sen: {PlayerStanding()}. sıra";
                SaveSystem.Data.trophies.Add(trophy);
            }
            SaveSystem.Save();
        }

        public static List<int> DriverStandings()
        {
            var st = State;
            var l = new List<int>(st.seats);
            l.Sort((a, b) =>
            {
                int c = st.driverPoints[b].CompareTo(st.driverPoints[a]);
                if (c != 0) return c;
                c = st.wins[b].CompareTo(st.wins[a]);
                if (c != 0) return c;
                return st.podiums[b].CompareTo(st.podiums[a]);
            });
            return l;
        }

        public static List<int> TeamStandings()
        {
            var st = State;
            var teams = new List<int>();
            foreach (var s in st.seats)
            {
                int t = GameData.Drivers[s].team;
                if (!teams.Contains(t)) teams.Add(t);
            }
            teams.Sort((a, b) => st.teamPoints[b].CompareTo(st.teamPoints[a]));
            return teams;
        }

        public static int PlayerStanding()
        {
            var l = DriverStandings();
            return l.IndexOf(State.playerSeat) + 1;
        }
    }
}
