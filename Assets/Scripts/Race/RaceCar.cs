using System.Collections.Generic;
using UnityEngine;

namespace F1Game
{
    public enum PitPhase { None, Entering, ToBox, Service, Leaving, Exiting }

    /// <summary>Everything the race needs to know about one car/driver.</summary>
    public class RaceCar
    {
        public int id;
        public int seat;          // index into GameData.Drivers (player replaces one seat)
        public int team;
        public bool isPlayer;
        public string name, shortName;
        public Color color;

        public CarController car;
        public CarVisual visual;
        public AIDriver ai;
        public PlayerDriver player;

        // progress / timing
        public float totalDistance;
        public float lapDistance;     // 0..L
        public float lateral;         // offset from the centre line
        public int trackIndex = -1;
        public int lapsCompleted;
        public float lapStartTime;
        public float lastLapTime = -1f, bestLapTime = float.MaxValue;
        public readonly List<float> lapTimes = new List<float>();
        public readonly float[] sectorTimes = new float[3];
        public readonly float[] lastSectorTimes = new float[3];
        public int sector;
        public float sectorStart;
        public bool lapInvalid;
        public readonly List<float> markerTimes = new List<float>(); // race time at every 20 m from the start
        public int position = 1;
        public bool finished, retired;
        public float finishTime;
        public float avgSpeed;
        public float gapAhead, gapLeader;
        public int lapsDownToLeader;

        // strategy
        public TyreCompound startCompound;
        public readonly List<TyreCompound> usedCompounds = new List<TyreCompound>();
        public int pitStops;
        public bool boxRequested;
        public TyreCompound planCompound = TyreCompound.Medium;
        public float planFuelAdd;
        public int planWingDelta;
        public bool planEdited;
        public float fuelAtLapStart;
        public float fuelPerLap;
        public float wearAtLapStart;
        public float wearPerLap;
        public bool pitDecisionMade;

        // pit state machine
        public PitPhase pit = PitPhase.None;
        public float pitS, pitSpeed, pitEntryS, pitOffsetStart, serviceTimer, serviceTotal;
        public string serviceNote = "";
        public float ghostTimer;

        // misc
        public float stuckTimer, wrongWayTimer;
        public bool drsEligible;
        public float lastSignedDist;

        public bool InPit => pit != PitPhase.None;
        public float CurrentLapTime(float raceTime) => raceTime - lapStartTime;
    }

    public class ResultRow
    {
        public int seat, team;
        public bool isPlayer;
        public string name;
        public int laps;
        public float time;
        public bool dnf;
        public float bestLap;
        public int pitStops;
        public int points;
        public bool fastestLap;
        public int position;
    }
}
