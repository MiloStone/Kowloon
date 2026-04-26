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

    public void Clear() => _adj.Clear();
}
