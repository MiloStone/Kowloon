using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Difficulty pools and per-floor scenario selection.
/// Floors 1–2: easy. Floors 3–5: 50/50 easy/medium. Floors 6–9: 25/50/25
/// easy/medium/hard. Floor 10+: 50/50 medium/hard.
/// </summary>
public static class ScenarioPools
{
    public static readonly List<Scenario> Easy = new()
    {
        new Scenario("Easy A",
            new AtLeastNRooms(6),
            new MaxDegreeAtLeast(3)),
        new Scenario("Easy B",
            new AtLeastNRooms(7),
            new MinimumSizeSuites(2, 2, 2)),
        new Scenario("Easy C",
            new AtLeastNRooms(6),
            new AtLeastNIsolatedRooms(2)),
        new Scenario("Easy D",
            new AtLeastNRooms(5),
            new AtLeastNDeadEnds(2)),
        new Scenario("Easy E",
            new AtMostNRooms(10),
            new MinimumSizeSuites(3)),
    };

    public static readonly List<Scenario> Medium = new()
    {
        new Scenario("Medium A",
            new AtLeastNRooms(6),
            new ExactlyNSuites(2),
            new AllSuitesAtLeast(3)),
        new Scenario("Medium B",
            new AtLeastNRooms(7),
            new NoIsolatedRooms(),
            new MaxDegreeAtLeast(3)),
        new Scenario("Medium C",
            new AtLeastNRooms(8),
            new MinimumSizeSuites(6)),
        new Scenario("Medium D",
            new AtLeastNRooms(8),
            new AtMostNSuites(3),
            new MinimumSizeSuites(5)),
    };

    public static readonly List<Scenario> Hard = new()
    {
        new Scenario("Hard A",
            new AtLeastNRooms(8),
            new AllRoomsConnected()),
        new Scenario("Hard B",
            new AtMostNRooms(9),
            new ExactlyNSuites(3),
            new AllSuitesAtLeast(2)),
        new Scenario("Hard C",
            new AtLeastNRooms(6),
            new ExactlyNSuites(6)),
        new Scenario("Hard D",
            new AtLeastNRooms(8),
            new MaxDegreeAtLeast(3),
            new AtLeastNSuites(5)),
    };

    public static Scenario RollForFloor(int floor)
    {
        var pool = PickPool(floor);
        return pool[Random.Range(0, pool.Count)];
    }

    static List<Scenario> PickPool(int floor)
    {
        if (floor <= 2) return Easy;
        if (floor <= 5) return Random.value < 0.5f ? Easy : Medium;
        if (floor <= 9)
        {
            float r = Random.value;
            if (r < 0.25f) return Easy;
            if (r < 0.75f) return Medium;
            return Hard;
        }
        return Random.value < 0.5f ? Medium : Hard;
    }
}
