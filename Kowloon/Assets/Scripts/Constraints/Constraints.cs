using System.Collections.Generic;
using System.Linq;

public class AtLeastNRooms : Constraint
{
    public readonly int N;
    public AtLeastNRooms(int n) { N = n; }
    public override bool IsSatisfied(ConnectivityGraph g) => g.NodeCount >= N;
    public override IEnumerable<ChecklistItem> GetChecklistItems(ConnectivityGraph g)
    {
        yield return new ChecklistItem($"At least {N} rooms", IsSatisfied(g));
    }
    public override string Describe() => $"At least {N} rooms";
}

public class AtMostNRooms : Constraint
{
    public readonly int N;
    public AtMostNRooms(int n) { N = n; }
    public override bool IsSatisfied(ConnectivityGraph g) => g.NodeCount <= N;
    public override IEnumerable<ChecklistItem> GetChecklistItems(ConnectivityGraph g)
    {
        yield return new ChecklistItem($"At most {N} rooms", IsSatisfied(g));
    }
    public override string Describe() => $"At most {N} rooms";
}

public class AllRoomsConnected : Constraint
{
    public override bool IsSatisfied(ConnectivityGraph g) => g.ComponentSizes().Count <= 1;
    public override IEnumerable<ChecklistItem> GetChecklistItems(ConnectivityGraph g)
    {
        yield return new ChecklistItem("All rooms connected", IsSatisfied(g));
    }
    public override string Describe() => "All rooms connected";
}

public class NoIsolatedRooms : Constraint
{
    public override bool IsSatisfied(ConnectivityGraph g) => !g.ComponentSizes().Any(s => s == 1);
    public override IEnumerable<ChecklistItem> GetChecklistItems(ConnectivityGraph g)
    {
        yield return new ChecklistItem("No isolated rooms", IsSatisfied(g));
    }
    public override string Describe() => "No isolated rooms";
}

/// <summary>
/// Emits one line per required isolated room. Each line is satisfied iff a
/// distinct size-1 component exists for it (greedy assignment).
/// </summary>
public class AtLeastNIsolatedRooms : Constraint
{
    public readonly int N;
    public AtLeastNIsolatedRooms(int n) { N = n; }
    public override bool IsSatisfied(ConnectivityGraph g) => g.ComponentSizes().Count(s => s == 1) >= N;
    public override IEnumerable<ChecklistItem> GetChecklistItems(ConnectivityGraph g)
    {
        int isolated = g.ComponentSizes().Count(s => s == 1);
        for (int i = 0; i < N; i++)
            yield return new ChecklistItem("Isolated room", i < isolated);
    }
    public override string Describe() => $"At least {N} isolated room{(N == 1 ? "" : "s")}";
}

public class AllSuitesAtLeast : Constraint
{
    public readonly int N;
    public AllSuitesAtLeast(int n) { N = n; }
    public override bool IsSatisfied(ConnectivityGraph g) => g.ComponentSizes().All(s => s >= N);
    public override IEnumerable<ChecklistItem> GetChecklistItems(ConnectivityGraph g)
    {
        yield return new ChecklistItem($"Every suite has at least {N} rooms", IsSatisfied(g));
    }
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
    public override IEnumerable<ChecklistItem> GetChecklistItems(ConnectivityGraph g)
    {
        yield return new ChecklistItem("No dead ends", IsSatisfied(g));
    }
    public override string Describe() => "No dead ends";
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
    public override IEnumerable<ChecklistItem> GetChecklistItems(ConnectivityGraph g)
    {
        yield return new ChecklistItem($"One room with {N}+ connections", IsSatisfied(g));
    }
    public override string Describe() => $"One room with {N}+ connections";
}

/// <summary>
/// Requires multiple distinct suites of given minimum sizes. Emits one line per
/// required size; greedy match pairs each (smallest-first) with the smallest
/// unused component meeting it. e.g. [3,3] with one suite of 4 → first ✓, second ✗.
/// </summary>
public class MinimumSizeSuites : Constraint
{
    public readonly int[] RequiredSizes;
    public MinimumSizeSuites(params int[] sizes) { RequiredSizes = sizes; }

    public override bool IsSatisfied(ConnectivityGraph g) => MatchAll(g, out _);

    public override IEnumerable<ChecklistItem> GetChecklistItems(ConnectivityGraph g)
    {
        // Match in ascending order so each line corresponds to a stable required
        // size; report items back in the original required-sizes order.
        var components = g.ComponentSizes();
        components.Sort();

        int   n           = RequiredSizes.Length;
        int[] sortIdx     = Enumerable.Range(0, n).OrderBy(i => RequiredSizes[i]).ToArray();
        bool[] satByOrig  = new bool[n];
        bool[] used       = new bool[components.Count];

        foreach (int origIdx in sortIdx)
        {
            int need = RequiredSizes[origIdx];
            for (int ci = 0; ci < components.Count; ci++)
            {
                if (used[ci]) continue;
                if (components[ci] >= need) { used[ci] = true; satByOrig[origIdx] = true; break; }
            }
        }

        for (int i = 0; i < n; i++)
            yield return new ChecklistItem($"Suite of {RequiredSizes[i]}+ rooms", satByOrig[i]);
    }

    bool MatchAll(ConnectivityGraph g, out bool[] _)
    {
        _ = null;
        var components = g.ComponentSizes();
        components.Sort();
        var required = RequiredSizes.OrderBy(s => s).ToList();
        var used     = new bool[components.Count];
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
        var sorted = RequiredSizes.OrderByDescending(s => s);
        return $"Distinct suites of size: {string.Join(", ", sorted)}";
    }
}
