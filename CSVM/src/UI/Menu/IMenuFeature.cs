namespace CSVM.UI.Menu;

/// <summary>
/// One shared menu feature: typed state plus the semantic operations that configure, validate,
/// persist and launch one area of play. A feature exposes its own concrete members, not a
/// universal row/button/picture schema; each presentation decides how those operations are
/// offered on screen. A feature never references a presentation, and the dependency test over
/// this namespace (<c>MenuNamespaceDependencyTests</c>) rejects any that does.
/// </summary>
public interface IMenuFeature
{
    /// <summary>Drops unfinished setup and every other transient value, called when the active
    /// presentation changes. Persisted data is not touched and survives the switch.</summary>
    void Discard();
}
