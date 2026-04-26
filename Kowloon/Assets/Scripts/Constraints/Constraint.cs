/// <summary>
/// A floor-completion requirement. Evaluated against the floor's connectivity
/// graph; the stair tile only unlocks when every constraint in the active
/// Scenario reports satisfied.
/// </summary>
public abstract class Constraint
{
    public abstract bool   IsSatisfied(ConnectivityGraph graph);
    public abstract string Describe();
}
