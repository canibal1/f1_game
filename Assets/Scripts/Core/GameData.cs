using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    /// <summary>Fictional teams – no real-world team names or liveries are used.</summary>
    public class TeamDef
    {
        public string name;
        public Color primary;
        public Color secondary;
        public float performance; // 0.97 .. 1.0 car pace multiplier for AI
        public TeamDef(string n, Color p, Color s, float perf) { name = n; primary = p; secondary = s; performance = perf; }
    }

    public class DriverDef
    {
        public string name;
        public string shortName;
        public int team;
        public float skill;      // 0.9 .. 1.0
        public float aggression; // 0 .. 1 personal trait (scaled by global AI aggression)
        public DriverDef(string n, string s, int t, float sk, float ag) { name = n; shortName = s; team = t; skill = sk; aggression = ag; }
    }

    public enum Scenery { Parkland, Street, Forest, Desert, Blossom, Speedway }

    public class TrackDef
    {
        public string id;
        public string name;
        public string location;
        public string description;
        public Vector2[] points;
        public float width = 14f;
        public float runoff = 12f;     // distance from track edge to barrier
        public Scenery scenery;
        public Color grass;
        public int defaultLaps = 5;
        public CarSetupPreset recommended;
    }

    public enum CarSetupPreset { LowDownforce, Balanced, HighDownforce }

    public static class GameData
    {
        public static readonly int[] Points = { 25, 18, 15, 12, 10, 8, 6, 4, 2, 1 };
        public const int FastestLapPoint = 1;

        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }

        public static readonly TeamDef[] Teams =
        {
            new TeamDef("Bosphorus Racing", C("#D7263D"), C("#F5F5F5"), 1.000f),
            new TeamDef("Aurora GP",        C("#00A6A6"), C("#F2F2F2"), 0.995f),
            new TeamDef("Vortex Motorsport",C("#FF7A00"), C("#1B1B1B"), 0.993f),
            new TeamDef("Nordlicht Racing", C("#B8C4CC"), C("#00C2FF"), 0.990f),
            new TeamDef("Falcon Speed",     C("#1D3F8F"), C("#FFD100"), 0.986f),
            new TeamDef("Titan Works",      C("#6B2FA3"), C("#E3B341"), 0.982f),
            new TeamDef("Emerald Racing",   C("#0B8A47"), C("#FFFFFF"), 0.978f),
            new TeamDef("Sakura Dynamics",  C("#F06292"), C("#FFFFFF"), 0.975f),
            new TeamDef("Volt Engineering", C("#F2D600"), C("#202020"), 0.972f),
            new TeamDef("Atlas Racing Team",C("#132A4A"), C("#E63946"), 0.968f),
        };

        public static readonly DriverDef[] Drivers =
        {
            new DriverDef("Kaan Demir",      "DEM", 0, 0.995f, 0.70f),
            new DriverDef("Lena Hoffmann",   "HOF", 0, 0.985f, 0.45f),
            new DriverDef("Mateo Rivas",     "RIV", 1, 0.990f, 0.60f),
            new DriverDef("Arjun Mehta",     "MEH", 1, 0.980f, 0.40f),
            new DriverDef("Sofia Lindqvist", "LIN", 2, 0.992f, 0.55f),
            new DriverDef("Lucas Moreau",    "MOR", 2, 0.978f, 0.75f),
            new DriverDef("Hiro Tanabe",     "TAN", 3, 0.986f, 0.35f),
            new DriverDef("Elif Şahin",      "ŞAH", 3, 0.982f, 0.65f),
            new DriverDef("Diego Alvarado",  "ALV", 4, 0.975f, 0.85f),
            new DriverDef("Noah Bennett",    "BEN", 4, 0.972f, 0.50f),
            new DriverDef("Yusuf Kaya",      "KAY", 5, 0.976f, 0.60f),
            new DriverDef("Isabella Costa",  "COS", 5, 0.970f, 0.45f),
            new DriverDef("Finn Gallagher",  "GAL", 6, 0.968f, 0.80f),
            new DriverDef("Aleksei Vorontsov","VOR",6, 0.966f, 0.55f),
            new DriverDef("Chloé Marchand",  "MAR", 7, 0.965f, 0.40f),
            new DriverDef("Ravi Kapoor",     "KAP", 7, 0.962f, 0.70f),
            new DriverDef("Jonas Berglund",  "BER", 8, 0.960f, 0.50f),
            new DriverDef("Tomás Herrera",   "HER", 8, 0.958f, 0.90f),
            new DriverDef("Mason Whitlock",  "WHI", 9, 0.955f, 0.60f),
            new DriverDef("Emre Yalın",      "YAL", 9, 0.952f, 0.45f),
        };

        static Vector2[] P(params float[] xy)
        {
            var v = new Vector2[xy.Length / 2];
            for (int i = 0; i < v.Length; i++) v[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
            return v;
        }

        // Layouts validated with Tools/TrackDesigner/tracks.py (length, min radius, clearance).
        public static readonly TrackDef[] Tracks =
        {
            new TrackDef
            {
                id = "anadolu", name = "Anadolu Park Pisti", location = "Türkiye (kurgusal)",
                description = "Dengeli bir pist: uzun ana düzlük, hızlı S virajları ve keskin bir firkete.",
                points = P(0,0, 0,200, 0,380, 30,470, 120,500, 220,470, 300,520, 330,620, 420,680, 540,640, 600,520,
                           560,400, 470,330, 440,220, 500,110, 520,-20, 450,-140, 330,-170, 240,-120, 170,-220, 110,-380,
                           40,-430, 0,-360, 0,-200),
                scenery = Scenery.Parkland, grass = new Color(0.33f, 0.55f, 0.22f), defaultLaps = 5,
                recommended = CarSetupPreset.Balanced
            },
            new TrackDef
            {
                id = "costa", name = "Costa Azul Sokak Pisti", location = "Akdeniz kıyısı (kurgusal)",
                description = "Duvarlarla çevrili dar sokak pisti. Yüksek downforce ve iyi çekiş ister.",
                points = P(0,0, 0,170, 0,330, 30,400, 100,410, 150,370, 210,390, 230,450, 300,480, 380,450, 400,370,
                           360,300, 380,220, 450,190, 470,100, 420,30, 330,20, 290,-50, 230,-110, 160,-150, 110,-230,
                           60,-300, 10,-290, 0,-180),
                width = 12f, runoff = 3.5f,
                scenery = Scenery.Street, grass = new Color(0.36f, 0.5f, 0.25f), defaultLaps = 5,
                recommended = CarSetupPreset.HighDownforce
            },
            new TrackDef
            {
                id = "fjord", name = "Fjordland Ring", location = "İskandinavya (kurgusal)",
                description = "Çam ormanları arasında akıcı, yüksek hızlı viraj dizileri.",
                points = P(0,0, 0,250, 0,480, -60,620, -200,700, -380,680, -520,600, -560,460, -480,350, -330,330,
                           -260,240, -330,120, -480,60, -560,-60, -520,-200, -380,-280, -220,-260, -130,-360, -40,-420,
                           0,-340, 0,-200),
                scenery = Scenery.Forest, grass = new Color(0.25f, 0.45f, 0.2f), defaultLaps = 5,
                recommended = CarSetupPreset.Balanced
            },
            new TrackDef
            {
                id = "oasis", name = "Vaha Çöl Pisti", location = "Çöl (kurgusal)",
                description = "Çok uzun düzlükler ve ağır frenleme noktaları. Düşük downforce avantajlı.",
                points = P(0,0, 0,300, 0,600, 20,720, 110,760, 200,700, 260,560, 380,520, 480,600, 580,560, 600,420,
                           520,300, 540,140, 620,20, 600,-160, 480,-240, 340,-200, 250,-280, 120,-380, 30,-400, 0,-320, 0,-180),
                scenery = Scenery.Desert, grass = new Color(0.78f, 0.68f, 0.45f), defaultLaps = 5,
                recommended = CarSetupPreset.LowDownforce
            },
            new TrackDef
            {
                id = "sakura", name = "Sakura Tepeleri", location = "Uzak Doğu (kurgusal)",
                description = "Teknik, ritmik bir pist. Orta-yüksek downforce ve dengeli fren gerekir.",
                points = P(0,0, 0,180, 0,330, 40,420, 140,440, 200,380, 180,290, 240,220, 330,250, 380,350, 470,400,
                           560,340, 560,230, 480,170, 500,70, 580,0, 560,-110, 440,-150, 330,-90, 250,-160, 180,-280,
                           90,-360, 20,-350, 0,-260, 0,-130),
                scenery = Scenery.Blossom, grass = new Color(0.4f, 0.58f, 0.27f), defaultLaps = 5,
                recommended = CarSetupPreset.HighDownforce
            },
            new TrackDef
            {
                id = "tempo", name = "Tempo Speedway", location = "Kuzey Amerika (kurgusal)",
                description = "Neredeyse tamamı tam gaz! En düşük kanat açısı ve uzun vitesler.",
                points = P(0,0, 0,350, 0,650, 40,780, 160,840, 300,800, 360,680, 360,450, 380,300, 360,120, 360,-100,
                           330,-260, 220,-340, 100,-380, 20,-340, 0,-230, 0,-100),
                scenery = Scenery.Speedway, grass = new Color(0.35f, 0.56f, 0.24f), defaultLaps = 6,
                recommended = CarSetupPreset.LowDownforce
            },
        };

        public static int TrackIndex(string id)
        {
            for (int i = 0; i < Tracks.Length; i++) if (Tracks[i].id == id) return i;
            return 0;
        }

        public static List<int> DriversOfTeam(int team)
        {
            var l = new List<int>();
            for (int i = 0; i < Drivers.Length; i++) if (Drivers[i].team == team) l.Add(i);
            return l;
        }
    }
}
