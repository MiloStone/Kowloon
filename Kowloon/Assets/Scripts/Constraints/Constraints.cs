using System.Collections.Generic;
using System.Linq;

public class AtLeastNRooms : Constraint
{
    public readonly int N;
    public AtLeastNRooms(int n) { N = n; }
    public override bool   IsSatisfied(ConnectivityGraph g) => g.NodeCount >= N;
    public override string Describe() => $"At least {N} rooms";
}

public class AtMostNRooms : Constraint
{
    public readonly int N;
    public AtMostNRooms(int n) { N = n; }
    public override bool   IsSatisfied(ConnectivityGraph g) => g.NodeCount <= N;
    public override string Describe() => $"At most {N} rooms";
}

public class AllRoomsConnected : Constraint
{
    public override bool   IsSatisfied(ConnectivityGraph g) => g.ComponentSizes().Count <= 1;
    public override string Describe() => "All rooms connected";
}

public class NoIsolatedRooms : Constraint
{
    public override bool   IsSatisfied(ConnectivityGraph g) => !g.ComponentSizes().Any(s => s == 1);
    public override string Describe() => "No isolated rooms";
}

public class AtLeastNIsolatedRooms : Constraint
{
    public readonly int N;
    public AtLeastNIsolatedRooms(int n) { N = n; }
    public override bool   IsSatisfied(ConnectivityGraph g) => g.ComponentSizes().Count(s => s == 1) >= N;
    public override string Describe() => $"At least {N} isolated room{(N == 1 ? "" : "s")}";
}

public class AllSuitesAtLeast : Constraint
{
    public readonly int N;
    public AllSuitesAtLeast(int n) { N = n; }
    public override bool   IsSatisfied(ConnectivityGraph g) => g.ComponentSizes().All(s => s >= N);
    public override string Describe() => $"Every suite has at least {N} rooms";
}

public class NoDeadEnds : Constraint
{
    public override bool IsSatisfied(ConnectivityGraph g)
    {
        foreach (var n in g.Nodes)
            if (g.Degree(n) < 2) return false;
        return true;
    }
    public override string Describe() => "No dead ends (every room has 2+ doors connected)";
}

public class MaxDegreeAtLeast : Constraint
{
    public readonly int N;
    public MaxDegreeAtLeast(int n) { N = n; }
    public override bool IsSatisfied(ConnectivityGraph g)
    {
        foreach (var n in g.Nodes)
            if (g.Degree(n) >= N) return true;
        return false;
    }
    public override string Describe() => $"At least one room with {N}+ connections";
}

/// <summary>
/// Requires multiple distinct suites of given minimum sizes. Evaluation pairs
/// each required size (smallest first) with the smallest unused component that
/// satisfies it; succeeds iff every required size finds a match.
/// e.g. RequiredSizes = [3, 5, 5] needs 3 distinct suites: one ≥3 and two ≥5.
/// </summary>
public class MinimumSizeSuites : Constraint
{
    public readonly int[] RequiredSizes;
    public MinimumSizeSuites(params int[] sizes) { RequiredSizes = sizes; }

    public override bool IsSatisfied(ConnectivityGraph g)
    {
        var components = g.ComponentSizes();
        components.Sort();
        var required = RequiredSizes.ToList();
        required.Sort();

        var used = new bool[components.Count];
        foreach (var need in required)
        {
            int matched = -1;
            for (int i = 0; i < components.Count; i++)
            {
                if (used[i]) continue;
                if (components[i] >= need) { matched = i; break; }
            }
            if (matched < 0) return false;
            used[matched] = true;
        }
        return true;
    }

    public override string Describe()
    {
        var sorted = RequiredSizes.OrderByDescending(s => s).Select(s => s.ToString());
        return $"Distinct suites of size: {string.Join(", ", sorted)}";
    }
}
