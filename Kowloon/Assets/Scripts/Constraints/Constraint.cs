using System.Collections.Generic;

/// <summary>
/// A floor-completion requirement. Evaluated against the floor's connectivity
/// graph; the stair tile only unlocks when every constraint in the active
/// Scenario reports satisfied.
///
/// GetChecklistItems returns one entry per UI line. Most constraints emit one
/// item; constraints like MinimumSizeSuites([3,3]) emit multiple, each
/// independently matched (greedy smallest-first) so the UI can show partial
/// progress (e.g. one ✓ + one ✗ when only one of two suites is large enough).
/// </summary>
public abstract class Constraint
{
    public abstract bool                       IsSatisfied(ConnectivityGraph graph);
    public abstract IEnumerable<ChecklistItem> GetChecklistItems(ConnectivityGraph graph);
    public abstract string                     Describe();
}

public struct ChecklistItem
{
    public string Text;
    public bool   Satisfied;
    public ChecklistItem(string text, bool satisfied) { Text = text; Satisfied = satisfied; }
}
