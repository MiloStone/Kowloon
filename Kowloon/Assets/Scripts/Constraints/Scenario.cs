using System.Collections.Generic;

/// <summary>
/// A floor's full requirement: every constraint must be satisfied for the
/// stair tile to unlock. Scenarios are pulled from difficulty pools per floor.
/// </summary>
public class Scenario
{
    public readonly string             Name;
    public readonly List<Constraint>   Constraints;

    public Scenario(string name, params Constraint[] constraints)
    {
        Name        = name;
        Constraints = new List<Constraint>(constraints);
    }

    public bool IsSatisfied(ConnectivityGraph graph)
    {
        foreach (var c in Constraints)
            if (!c.IsSatisfied(graph)) return false;
        return true;
    }

    public List<ChecklistItem> BuildChecklist(ConnectivityGraph graph)
    {
        var items = new List<ChecklistItem>();
        foreach (var c in Constraints)
            items.AddRange(c.GetChecklistItems(graph));
        return items;
    }

    public string Describe()
    {
        var lines = new List<string>();
        foreach (var c in Constraints) lines.Add(c.Describe());
        return $"{Name} — [{string.Join("; ", lines)}]";
    }
}
