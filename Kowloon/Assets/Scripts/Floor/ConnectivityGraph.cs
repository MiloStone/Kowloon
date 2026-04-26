using System.Collections.Generic;

/// <summary>
/// Per-floor connectivity graph: nodes are placed tiles, edges are pairs of tiles
/// joined by a connected (open) door. Cleared when the floor advances.
/// </summary>
public class ConnectivityGraph
{
    private readonly Dictionary<PlacedTile, HashSet<PlacedTile>> _adj = new();

    public void AddNode(PlacedTile t)
    {
        if (t != null && !_adj.ContainsKey(t)) _adj[t] = new HashSet<PlacedTile>();
    }

    public void Connect(PlacedTile a, PlacedTile b)
    {
        if (a == null || b == null || a == b) return;
        AddNode(a); AddNode(b);
        _adj[a].Add(b);
        _adj[b].Add(a);
    }

    public IReadOnlyCollection<PlacedTile> Neighbours(PlacedTile t) =>
        _adj.TryGetValue(t, out var set)
            ? (IReadOnlyCollection<PlacedTile>)set
            : System.Array.Empty<PlacedTile>();

    public int NodeCount => _adj.Count;

    public IEnumerable<PlacedTile> Nodes => _adj.Keys;

    public int Degree(PlacedTile t) =>
        _adj.TryGetValue(t, out var set) ? set.Count : 0;

    /// <summary>Sizes of each connected component (isolated tile = size 1).</summary>
    public List<int> ComponentSizes()
    {
        var visited = new HashSet<PlacedTile>();
        var sizes   = new List<int>();
        foreach (var start in _adj.Keys)
        {
            if (visited.Contains(start)) continue;
            int   size  = 0;
            var   stack = new Stack<PlacedTile>();
            stack.Push(start);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                if (!visited.Add(n)) continue;
                size++;
                foreach (var nb in _adj[n])
                    if (!visited.Contains(nb)) stack.Push(nb);
            }
            sizes.Add(size);
        }
        return sizes;
    }

    /// <summary>Size of the connected component containing <paramref name="t"/>.</summary>
    public int ComponentSizeContaining(PlacedTile t)
    {
        if (t == null || !_adj.ContainsKey(t)) return 0;
        var visited = new HashSet<PlacedTile>();
        var stack   = new Stack<PlacedTile>();
        stack.Push(t);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (!visited.Add(n)) continue;
            foreach (var nb in _adj[n])
                if (!visited.Contains(nb)) stack.Push(nb);
        }
        return visited.Count;
    }

    public void Clear() => _adj.Clear();
}
