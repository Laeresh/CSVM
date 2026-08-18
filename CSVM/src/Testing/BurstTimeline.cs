namespace CSVM.Testing;

internal readonly record struct BurstStep(int Index, string Kind, string? Name, float At);

internal readonly record struct BurstFire(float T, string Anim, string Sequence, int Index,
    string Kind, string? Name);

internal sealed record BurstLane(string Sequence, BurstStep[] Steps);
