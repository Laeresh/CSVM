namespace CSVM.Flight;

/// <summary>Physics constants shared by flight and weapon ballistics that both derive from
/// player.json's global block, kept in one place so they can't drift apart.</summary>
public static class PhysicsConstants
{
    public const float NomGravity = 20.0f; // player.json nom_gravity — the game's arcade gravity, m/s²
}
