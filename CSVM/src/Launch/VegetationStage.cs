using System.Collections.Generic;
using CSVM.Mech3;
using CSVM.Spec;
using CSVM.Utils;
using Godot;

namespace CSVM.Launch;

/// <summary>The vegetation over a built world, one step of the session build kept for the session.
/// It holds the clutter, the canopy clumps and the ground cover, and drives the map edge's copies
/// of them. Every live setting and the graphics switch take the one order in <see cref="Follow"/>.
/// Module entry: docs/architecture/Launch.md on src/Launch/VegetationStage.cs.</summary>
internal sealed class VegetationStage
{
    private readonly SessionSpec _spec;
    private readonly Node3D _plane;
    private readonly TextureArchive? _textures;
    private readonly MapEdgeExtender? _edgeExtender;

    // Set when a world node holding canopy or cover ground is switched (FollowGroundSwitch).
    private bool _groundSwitched;

    /// <summary>The stage over one world's <paramref name="clutter"/>, drawn under
    /// <paramref name="plane"/> from <paramref name="textures"/>. The session built
    /// <paramref name="edgeExtender"/> and keeps it; the stage only follows it.</summary>
    public VegetationStage(SessionSpec spec, Node3D plane, TextureArchive? textures, ClutterBuilder? clutter,
        MapEdgeExtender? edgeExtender)
    {
        _spec = spec;
        _plane = plane;
        _textures = textures;
        Clutter = clutter;
        _edgeExtender = edgeExtender;
        FollowCanopyClumps();
    }

    /// <summary>The world's clutter, null where it has none.</summary>
    public ClutterBuilder? Clutter { get; }

    /// <summary>The canopy clumps standing, with their tiles and rate, null while none stand. Rich on
    /// and the chapter or --canopy-clumps draws them.</summary>
    public Mech3.CanopyClumps.Field? CanopyClumpField { get; private set; }

    /// <summary>The canopy clumps' node over the map, null while none stand.</summary>
    public Node3D? CanopyClumps => CanopyClumpField?.Root;

    /// <summary>The ground cover shells, standing while Rich is on and the Ground Cover word is shells.</summary>
    public Node3D? GroundCover { get; private set; }

    /// <summary>The ground cover cards, standing while Rich is on and the Ground Cover word is cards.</summary>
    public Mech3.GroundCover.Field? GroundCoverCards { get; private set; }

    /// <summary>Cuts the clutter cells again and draws the map edge's copies and the clumps after
    /// it. The live View Distance, Vegetation Quality, 3D Vegetation and Ground Cover words and the
    /// graphics switch all land here. The cells' size and range come from the fade scale the
    /// launcher just wrote.</summary>
    public void Follow()
    {
        Clutter?.Recut();
        _edgeExtender?.FollowClutterFade();
        FollowCanopyClumps();
    }

    /// <summary>Bakes the clutter's Rich variant again from <paramref name="entry"/>, the vegetation
    /// lab's live edit, and draws it with the map edge's copies (<see cref="ClutterBuilder.ApplyVegetation"/>).</summary>
    public void Apply(VegetationEntry entry)
    {
        Clutter?.ApplyVegetation(entry);
        _edgeExtender?.FollowVegetation();
        FollowCanopyClumps(rebake: true);
    }

    /// <summary>Bakes the clutter's Rich variant again under <paramref name="mask"/>, the vegetation lab's
    /// brush stroke, as <see cref="Apply(VegetationEntry)"/> does for an entry.</summary>
    public void Apply(VegetationMask mask)
    {
        Clutter?.ApplyVegetationMask(mask);
        _edgeExtender?.FollowVegetation();
        FollowCanopyClumps(rebake: true);
    }

    /// <summary>Builds the canopy clumps and the ground cover again once a world node holding their ground was
    /// switched since the last build. Called each frame; one rebuild serves every switch a frame makes.</summary>
    public void FollowGroundSwitch()
    {
        if (!_groundSwitched)
            return;
        _groundSwitched = false;
        Clutter?.ForgetCanopy();
        FollowCanopyClumps(rebake: true);
    }

    /// <summary>The ground cover cards' window follows the panes' cameras.</summary>
    public void Update(List<Vector3> focusPoints) => GroundCoverCards?.Update(focusPoints);

    private static void Drop(Node node)
    {
        node.GetParent()?.RemoveChild(node);
        node.QueueFree();
    }

    // The canopy clumps follow Rich: built while it is on and dropped when it is not. The map edge's copies
    // and the cards' handover go with them. A lab edit builds them again from the re-baked canopy mask.
    private void FollowCanopyClumps(bool rebake = false)
    {
        FollowGroundCover(rebake);
        // A script switching an island on or off rebuilds both on the next frame, from the ground then standing.
        if (Clutter?.Vegetation is { } picks)
            Clutter.FollowGround(t => CanopyMask.IsCanopy(picks, t) || picks.CoverFor(t) != null, () => _groundSwitched = true);
        bool wanted = (_spec.CanopyClumps ?? Mech3.CanopyClumps.DefaultOn(_spec.Chapter)) && VegetationQualitySetting.RichOn;
        if (CanopyClumpField != null && (!wanted || rebake))
        {
            Drop(CanopyClumpField.Root);
            CanopyClumpField = null;
            _edgeExtender?.SetClumps(null);
            Mech3.CanopyClumps.Handover(Clutter?.ExportedKinds, false);
        }
        // The crown models over painted canopy follow the 3D Vegetation word on standing clumps, with no rebuild.
        CanopyClumpField?.FollowModels();
        if (!wanted || CanopyClumpField != null || _textures == null
            || Clutter?.ExportedKinds is not { } kinds || Clutter.Vegetation is not { } entry || Clutter.Canopy() is not { } mask)
            return;
        string chapter = _spec.Chapter?.ToUpperInvariant() ?? "";
        CanopyClumpField = Mech3.CanopyClumps.Build(mask, kinds, entry, _textures, card => Mech3.CanopyClumps.TileFor(chapter, card), chapter);
        if (CanopyClumpField == null)
            return;
        _plane.AddChild(CanopyClumpField.Root);
        _edgeExtender?.SetClumps(CanopyClumpField);
    }

    // The ground cover follows Rich and the Ground Cover word as the clumps do, and a lab edit builds it again under
    // its mask. The word draws the cards or the shells over the entry's cover picks, never both.
    private void FollowGroundCover(bool rebake)
    {
        string word = VegetationQualitySetting.RichOn ? GroundCoverSetting.Word : GroundCoverSetting.Off;
        bool cards = word == GroundCoverSetting.Cards, shells = word == GroundCoverSetting.Shells;
        if (GroundCoverCards != null && (!cards || rebake))
        {
            Drop(GroundCoverCards.Root);
            GroundCoverCards = null;
        }
        if (GroundCover != null && (!shells || rebake))
        {
            Drop(GroundCover);
            GroundCover = null;
        }
        if (_textures == null || Clutter?.Vegetation is not { } entry)
            return;
        if (cards && GroundCoverCards == null)
        {
            GroundCoverCards = Mech3.GroundCover.Build(Clutter.Gamez, _textures, Clutter.Mask, entry, _spec.Chapter ?? "", Clutter.SwitchedOff());
            if (GroundCoverCards != null)
                _plane.AddChild(GroundCoverCards.Root);
        }
        if (shells && GroundCover == null)
        {
            GroundCover = GroundCoverShells.Build(Clutter.Gamez, Clutter.Mask, _textures, entry, switchedOff: Clutter.SwitchedOff());
            if (GroundCover != null)
                _plane.AddChild(GroundCover);
        }
    }
}
