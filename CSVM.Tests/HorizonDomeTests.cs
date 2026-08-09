using System;
using System.Collections.Generic;
using CSVM.Mech3;
using Godot;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// The skydome the camera's own weather state draws (<c>PLAN-weather-decompile-match</c> B14).
///
/// <para>The original draws the dome of the zone it is IN: below the cloud deck a deck chapter's
/// camera is in state 1, its <c>zone_id 2</c> dome is culled and <c>horizon/zone1</c>'s own
/// geometry is both the sky and the ceiling. Two halves are pinned here — the rule that decides
/// how many domes a world builds (<see cref="WorldBuilder.DomeZonesToBuild"/>), and what each
/// chapter's zone-1 dome actually IS, read off the extraction.</para>
///
/// <para>⚠ The second half exists because the plan's own premise died on it. B14 was written
/// expecting C1's below-deck ceiling at <b>+396.4 m</b> — the <c>bbox_mid.y</c> of
/// <c>h_zone1scroll</c>'s model, which A1 read as a "cap centre". It is the midpoint of a bbox
/// spanning −2000…+2792.8 and there is no geometry within 2 km of it: the mesh's actual flat
/// ceiling cap is the 12-gon at <b>+2792.8</b>. These cases assert the caps so the retired number
/// cannot come back.</para>
/// </summary>
public class HorizonDomeTests
{
    /// <summary>How many domes each chapter builds, and which zones, against the real horizon
    /// census. The four deck chapters and C5 build two; the three whose <c>zone2</c> is a bare
    /// marker build one, exactly as before this item.</summary>
    public static TheoryData<string, string, string> ChapterDomeZones => new()
    {
        { "C1", "zone2", "zone2, zone1" },
        { "C1B", "zone1", "zone1" },
        { "C1C", "zone2", "zone2, zone1" },
        { "C2", "zone1", "zone1" },
        { "C2B", "zone2", "zone2, zone1" },
        { "C3", "zone1", "zone1" },
        { "C4", "zone2", "zone2, zone1" },
        { "C5", "zone1", "zone1, zone3" },
    };

    /// <summary>Each chapter's ZONE-1 dome: the node(s) it is made of, the flat horizontal ceiling
    /// cap's altitude in dome-local metres, and the cap's own outer radius. Read off
    /// <c>models.json</c>; the elevation the cap's rim subtends from the camera
    /// (<c>atan(capY / capRadius)</c>) is the only part of this the render can show, because the
    /// dome is centred on the camera and scaled uniformly about it.</summary>
    public static TheoryData<string, string, int, float, float> ZoneOneCeilings => new()
    {
        // C1 is the only chapter whose zone-1 dome carries a TEXTURE at all (sky2.tif on the
        // vault) and the only one that scrolls it — and the only one built from two nodes.
        { "C1", "zone1", 2, 2792.8f, 2608.7f },
        // C1C and C2B share one untextured FOG_COLOR shell (identical vertex data, different
        // model index): a four-sided prism, cap at +2374.7.
        { "C1C", "zone1", 1, 2374.7f, 1448.2f },
        { "C2B", "zone1", 1, 2374.7f, 1448.2f },
        // C4's sole zone-1 node is CONFUSINGLY NAMED h_zone2scroll (a reused name, not a scroll:
        // its model's texture_scroll is 0) — an octagonal FOG_COLOR shell capped at +982.
        { "C4", "zone1", 1, 982.0f, 6400.0f },
        // C5's state-3 dome, the same shape as C4's, painted ZONE3's own [16,16,16].
        { "C5", "zone3", 1, 982.0f, 10137.1f },
    };

    [Fact]
    public void TheActiveZoneIsAlwaysBuiltAndComesFirst()
    {
        var built = WorldBuilder.DomeZonesToBuild(Zones(("zone1", 2, 1), ("zone2", 4, 2)), "zone2");
        Assert.Equal(new[] { "zone2", "zone1" }, built);
    }

    [Fact]
    public void AZoneWithNoGeometryIsNeverBuiltBeside()
    {
        // C1B/C2/C3: the bare zone2 marker would add a dome of zero meshes and arm the gate, which
        // would then hide the ONE real dome at state 2 — a frame with no sky in it.
        Assert.Equal(new[] { "zone1" }, WorldBuilder.DomeZonesToBuild(Zones(("zone1", 4, 1), ("zone2", 0, 2)), "zone1"));
    }

    [Fact]
    public void ZonesTheGateCannotTellApartAreNeverBuiltTogether()
    {
        // The gate passes -1 and 0 at EVERY state (ZoneGate.Draws), so a second dome carrying one
        // would draw on top of the first for ever. Same for a duplicate id.
        Assert.Equal(new[] { "zone2" }, WorldBuilder.DomeZonesToBuild(Zones(("zone2", 4, 2), ("zone1", 2, -1)), "zone2"));
        Assert.Equal(new[] { "zone2" }, WorldBuilder.DomeZonesToBuild(Zones(("zone2", 4, 2), ("zone1", 2, 2)), "zone2"));
        // ...including when it is the ACTIVE zone that is ungateable: nothing may be added beside
        // a dome that draws at every state.
        Assert.Equal(new[] { "zone2" }, WorldBuilder.DomeZonesToBuild(Zones(("zone2", 4, -1), ("zone1", 2, 1)), "zone2"));
    }

    [Fact]
    public void AZoneNameTheHorizonDoesNotHaveBuildsAlone()
    {
        // A legacy/absent census, or the --sky-zone of a chapter that numbers zones differently:
        // BuildHorizon's own fallback owns that case and nothing may be added beside it.
        Assert.Equal(new[] { "zone2" }, WorldBuilder.DomeZonesToBuild(Zones(("zone3", 1, 3), ("zone1", 2, 1)), "zone2"));
        Assert.Equal(new[] { "zone2" }, WorldBuilder.DomeZonesToBuild(Zones(), "zone2"));
    }

    [ExtractedDataTheory]
    [MemberData(nameof(ChapterDomeZones))]
    public void EveryChapterBuildsTheDomesItsOwnCensusSupports(
        string chapter, string activeZone, string expectedDomes)
    {
        var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));
        var zones = WorldBuilder.HorizonZonesOf(gamez);
        Assert.Equal(expectedDomes, string.Join(", ", WorldBuilder.DomeZonesToBuild(zones, activeZone)));
    }

    [ExtractedDataTheory]
    [MemberData(nameof(ZoneOneCeilings))]
    public void TheBelowDeckCeilingIsTheZoneDomesOwnFlatCap(
        string chapter, string zone, int expectedNodes, float expectedCapY, float expectedCapRadius)
    {
        // ⚠ The number this replaces is 396.4 (C1) — `bbox_mid.y`, a bbox statistic the plan and
        // docs/formats/weather.md both carried as an "authored cap centre". Asserting the CAP
        // POLYGON instead is what tells the two apart: 2792.8 is where the geometry is.
        var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));
        var horizon = gamez.FindByName("horizon");
        Assert.NotNull(horizon);

        int zoneIndex = -1;
        foreach (int childIndex in horizon!.Children)
        {
            if (gamez.Nodes[childIndex].Name.Equals(zone, StringComparison.OrdinalIgnoreCase))
                zoneIndex = childIndex;
        }

        Assert.True(zoneIndex >= 0, $"{chapter} has no horizon/{zone}");

        int meshedNodes = 0;
        float capY = float.NegativeInfinity;
        float capRadius = 0f;
        foreach (var mesh in MeshesUnder(gamez, zoneIndex))
        {
            meshedNodes++;
            foreach (var poly in mesh.Polygons)
            {
                // A ceiling cap is a horizontal polygon: every vertex at one Y.
                float minY = float.PositiveInfinity, maxY = float.NegativeInfinity, radius = 0f;
                foreach (int vi in poly.VertexIndices)
                {
                    var v = mesh.Vertices[vi];
                    minY = Mathf.Min(minY, v.Y);
                    maxY = Mathf.Max(maxY, v.Y);
                    radius = Mathf.Max(radius, Mathf.Sqrt((v.X * v.X) + (v.Z * v.Z)));
                }

                if (maxY - minY > 0.5f || maxY <= capY)
                    continue;
                capY = maxY;
                capRadius = radius;
            }
        }

        Assert.Equal(expectedNodes, meshedNodes);
        Assert.Equal(expectedCapY, capY, 1);
        Assert.Equal(expectedCapRadius, capRadius, 1);
        // The dome is camera-centred and uniformly scaled about the camera, so the ONE thing a
        // render can measure is the elevation this cap's rim subtends — scale-invariant, and
        // therefore the number B15's vertical-scale audit has to preserve rather than the metres.
        Assert.InRange(Mathf.RadToDeg(Mathf.Atan2(capY, capRadius)), 5f, 60f);
    }

    [ExtractedDataFact]
    public void OnlyC1sZoneOneDomeScrollsAndItScrollsAtTheAuthoredRate()
    {
        // `tex_fx.gw`'s `FindNode h_zone1scroll` + `Object3DSetScroll on 0.07 0.0` is already baked
        // into the shipped model's own texture_scroll field (MissionSetup.ScrollByModel's note), so
        // building the node is the whole implementation — there is no second scroll path to add.
        // The other three deck chapters author no scroll on their zone-1 geometry at all.
        foreach (var (chapter, zone, expected) in new[]
                 {
                     ("C1", "zone1", 0.07f), ("C1C", "zone1", 0f), ("C2B", "zone1", 0f), ("C4", "zone1", 0f),
                 })
        {
            var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));
            var horizon = gamez.FindByName("horizon");
            Assert.NotNull(horizon);
            float fastest = 0f;
            foreach (int childIndex in horizon!.Children)
            {
                if (!gamez.Nodes[childIndex].Name.Equals(zone, StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var mesh in MeshesUnder(gamez, childIndex))
                    fastest = Mathf.Max(fastest, Mathf.Abs(mesh.TextureScroll.X));
            }

            Assert.Equal(expected, fastest, 4);
        }
    }

    [ExtractedDataFact]
    public void OnlyC1sZoneOneCeilingHasARimAnythingCanSee()
    {
        // B15's audit result, pinned from the extraction. The domes are camera-centred, unfogged
        // and uniformly scaled, so the ONLY thing a frame can show is the elevation a feature
        // subtends — and a feature needs two materials to be a feature at all. C1's zone-1 dome
        // is the one that has them: a `sky2.tif` vault under a flat FOG_COLOR cap, so its cap RIM
        // is a visible boundary (B14 measured it at 48–52° against the authored 46.95°). Every
        // other zone-1/zone-3 dome is a SINGLE Colored material with `lighting: false`, i.e. one
        // flat authored colour in every direction: no rim, no observable, and therefore no scale
        // — uniform or split — that a render of those chapters could tell apart.
        foreach (var (chapter, zone, materials, flat) in new[]
                 {
                     ("C1", "zone1", 2, 176f), ("C1C", "zone1", 1, 176f), ("C2B", "zone1", 1, 176f),
                     ("C4", "zone1", 1, 192f), ("C5", "zone3", 1, 16f),
                 })
        {
            var gamez = GameZ.Load(SessionPaths.ChapterGamez(TestData.DataRoot!, chapter));
            var horizon = gamez.FindByName("horizon");
            Assert.NotNull(horizon);
            var used = new SortedSet<int>();
            foreach (int childIndex in horizon!.Children)
            {
                if (!gamez.Nodes[childIndex].Name.Equals(zone, StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var mesh in MeshesUnder(gamez, childIndex))
                    foreach (var poly in mesh.Polygons)
                        if (poly.MaterialIndex >= 0)
                            used.Add(poly.MaterialIndex);
            }

            Assert.Equal(materials, used.Count);
            int textured = 0, colored = 0;
            foreach (int index in used)
            {
                var mat = gamez.Materials[index];
                if (mat.TextureName != null)
                {
                    textured++;
                    Assert.Equal("sky2.tif", mat.TextureName);
                }
                else
                {
                    colored++;
                    Assert.Equal(flat, Mathf.Round(mat.Color.R * 255f));
                }
            }

            // Exactly one flat colour everywhere; the texture is C1's alone.
            Assert.Equal(1, colored);
            Assert.Equal(chapter == "C1" ? 1 : 0, textured);
        }
    }

    private static IReadOnlyList<GameZMesh> MeshesUnder(GameZ gamez, int nodeIndex)
    {
        var meshes = new List<GameZMesh>();
        void Walk(int index)
        {
            var node = gamez.Nodes[index];
            if (node.MeshIndex >= 0 && node.MeshIndex < gamez.Meshes.Count)
                meshes.Add(gamez.Meshes[node.MeshIndex]);
            foreach (int child in node.Children)
                Walk(child);
        }

        Walk(nodeIndex);
        return meshes;
    }

    private static IReadOnlyList<HorizonZone> Zones(params (string Name, int Meshed, int ZoneId)[] zones)
    {
        var list = new List<HorizonZone>();
        foreach (var (name, meshed, zoneId) in zones)
        {
            list.Add(new HorizonZone(name, meshed, zoneId));
        }

        return list;
    }
}
