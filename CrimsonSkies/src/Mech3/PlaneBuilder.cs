using System;
using System.Collections.Generic;
using Godot;

namespace CrimsonSkies.Mech3;

/// <summary>
/// Builds a renderable Godot node tree for one aircraft out of a planes.zbd GameZ model.
/// Exterior view only: picks the nearest LOD, skips cockpit/damage/destroyed variants.
/// </summary>
public sealed class PlaneBuilder
{
    // Subtrees that make no sense in a static exterior view. Cockpit interiors are
    // separate (differently-scaled) models; damage/destroyed are alternate states;
    // prop1/prop2/nitroprop are spinning-propeller animation frames (staticprop stays).
    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "cockpit1", "cockpit2", "destroyed", "shadow", "player_damage_on", "blood_hook",
        "prop1", "prop1b", "prop2", "prop2b", "nitroprop1", "nitroprop2",
    };

    private readonly GameZ _gamez;
    private readonly SceneBuilder _scene;

    public int MeshInstanceCount => _scene.MeshInstanceCount;

    public PlaneBuilder(GameZ gamez, TextureArchive textures)
    {
        _gamez = gamez;
        _scene = new SceneBuilder(gamez, textures);
    }

    /// <summary>Builds the subtree rooted at the named node (e.g. "player_bhawk").</summary>
    public Node3D Build(string rootName)
    {
        var root = _gamez.FindByName(rootName)
            ?? throw new ArgumentException($"node '{rootName}' not found in GameZ data");
        return _scene.BuildSubtree(root, n => SkipNames.Contains(n.Name))!;
    }
}
