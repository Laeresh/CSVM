using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Mech3;
using Godot;

namespace CSVM.Tooling;

/// <summary>
/// The synthetic run's anim and effect records. The reader-form destructibles and the two puffer
/// readers are <see cref="SyntheticZrdr"/>'s. The compiled anim archive
/// (<see cref="Anims"/>) and the template roots it stages (<see cref="Gamez"/>) are built in
/// memory. No session loader reads them; a suite asks for them by name. Engine-free.
/// </summary>
public static class SyntheticEffects
{
    /// <summary>The compiled archive's label, a name no install carries.</summary>
    public const string Folder = "probe_effects";


    /// <summary>The template gamez, built in memory: one node per template, depth first, children by
    /// flat index. Each node that names a box gets its mesh in the order met.</summary>
    public static GameZ Gamez()
    {
        var gz = new GameZ();
        foreach (var (r, g, b) in new[] { (230, 150, 60), (90, 90, 96), (200, 210, 230), (120, 96, 70), (40, 36, 32) })
        {
            gz.Materials.Add(SyntheticPlane.Colored(r, g, b));
        }

        GameZNode Add(GameZNode? parent, string name, Vector3 at = default, float[]? box = null, int material = 0)
        {
            var node = SyntheticPlane.AddNode(gz, "Object3d", name, -1, at);
            parent?.Children.Add(node.Index);
            if (box != null)
            {
                node.MeshIndex = SyntheticPlane.AddBox(gz, box[..3], box[3..], material);
            }

            return node;
        }

        float[] small = { -0.3f, -0.3f, -0.3f, 0.3f, 0.3f, 0.3f };
        Add(Add(null, "he_ring"), "he_ringer", box: new[] { -3.5f, 0.0f, -3.5f, 3.5f, 0.25f, 3.5f });
        Add(Add(null, "he_ring1"), "he_ringer1", box: new[] { -2.5f, -2.5f, -0.2f, 2.5f, 2.5f, 0.2f });
        var trails = Add(null, "he_trails");
        Add(trails, "fly_trail1", new Vector3(1.0f, 0.5f, 0.0f), small, 1);
        Add(trails, "fly_trail2", new Vector3(-1.0f, 0.5f, 0.0f), small, 1);
        Add(trails, "fly_trail3", new Vector3(0.0f, 0.5f, 1.0f), small, 1);
        Add(trails, "fly_trail4", new Vector3(0.0f, 0.5f, -1.0f), small, 1);
        Add(trails, "fly_trail5", new Vector3(0.0f, 1.2f, 0.0f), small, 1);
        Add(Add(null, "ballflare.flt"), "flare_disc", box: new[] { -0.5f, -0.5f, -0.05f, 0.5f, 0.5f, 0.05f }, material: 2);
        Add(Add(null, "dum_gunhit"), "gunhit_chunk", box: new[] { -0.4f, 0.0f, -0.4f, 0.4f, 0.6f, 0.4f }, material: 1);
        var flakes = Add(null, "planeflakes");
        Add(flakes, "flake1", new Vector3(0.3f, 0.0f, 0.0f), new[] { -0.15f, -0.02f, -0.1f, 0.15f, 0.02f, 0.1f }, 1);
        Add(flakes, "flake2", new Vector3(-0.3f, 0.1f, 0.0f), new[] { -0.12f, -0.02f, -0.12f, 0.12f, 0.02f, 0.12f }, 1);
        Add(flakes, "flake3", new Vector3(0.0f, -0.1f, 0.2f), new[] { -0.1f, -0.02f, -0.15f, 0.1f, 0.02f, 0.15f }, 1);
        Add(Add(null, "sonic_effect"), "sonic_emit1", new Vector3(0.0f, 0.5f, 0.0f));
        Add(Add(null, "sonic_ring1"), "sonic_ring", box: new[] { -2.0f, 0.0f, -2.0f, 2.0f, 0.15f, 2.0f }, material: 2);
        Add(Add(null, "sonic_ring2"), "sonic_ring", box: new[] { -1.5f, 0.0f, -1.5f, 1.5f, 0.15f, 1.5f }, material: 2);
        var torpedo = Add(null, "probe_torpbody");
        Add(torpedo, "atbody", box: new[] { -0.3f, -0.3f, -1.6f, 0.3f, 0.3f, 1.6f }, material: 1);
        Add(torpedo, "rightwing", new Vector3(0.6f, 0.0f, 0.4f), new[] { -0.3f, -0.03f, -0.2f, 0.3f, 0.03f, 0.2f }, 1);
        Add(torpedo, "leftwing", new Vector3(-0.6f, 0.0f, 0.4f), new[] { -0.3f, -0.03f, -0.2f, 0.3f, 0.03f, 0.2f }, 1);
        Add(torpedo, "atprop", new Vector3(0.0f, 0.0f, 1.7f), new[] { -0.35f, -0.35f, -0.02f, 0.35f, 0.35f, 0.02f }, 4);
        var splash = Add(null, "huge_splash_model");
        Add(splash, "splash_polys", box: new[] { -3.0f, 0.0f, -3.0f, 3.0f, 2.0f, 3.0f }, material: 2);
        Add(splash, "sp_1", new Vector3(0.0f, 0.5f, 0.0f));
        Add(splash, "ripple1", box: new[] { -4.0f, 0.0f, -4.0f, 4.0f, 0.05f, 4.0f }, material: 2);
        Add(null, "m_bld_healthy", box: new[] { -5.0f, 0.0f, -5.0f, 5.0f, 6.0f, 5.0f }, material: 3);
        Add(null, "carnage_trails");
        Add(null, "large_fire");
        Add(null, "white_water_impact");
        var shed = Add(null, "probe_shed", new Vector3(40.0f, 0.0f, -30.0f));
        Add(shed, "healthy", box: new[] { -4.0f, 0.0f, -3.0f, 4.0f, 5.0f, 3.0f }, material: 3);
        Add(shed, "destroyed", box: new[] { -4.5f, 0.0f, -3.5f, 4.5f, 1.2f, 3.5f }, material: 4);
        var tank = Add(null, "probe_tank", new Vector3(-40.0f, 0.0f, 30.0f));
        Add(tank, "healthy", box: new[] { -2.5f, 0.0f, -2.5f, 2.5f, 7.0f, 2.5f }, material: 3);
        Add(tank, "destroyed", box: new[] { -3.0f, 0.0f, -3.0f, 3.0f, 1.0f, 3.0f }, material: 4);
        return gz;
    }

    /// <summary>The compiled anim archive the templates stage, built in memory. Each OnCall
    /// definition is a property tree the model's own parse reads, and no SI script is pooled. A
    /// fresh archive per call, so a suite may edit what it gets.</summary>
    public static AnimArchive Anims()
    {
        var defs = new (string File, Dictionary<string, object?> Def)[]
        {
            ("he_ring-he_ground_effect", Def("he_ring", "he_ground_effect", "he_ring", null, Seq("",
                Active("he_ring", true),
                Call("call_he_ring1", "he_ring", V(0f, 9f, 0f)),
                Call("call_he_trails", "he_ring"),
                FromTo("he_ringer", "scale", V(0.3f, 1f, 0.3f), V(1.6f, 1f, 1.6f), 1f)))),
            ("he_ring1-call_he_ring1", Def("he_ring1", "call_he_ring1", "he_ring1", null, Seq("",
                FromTo("he_ringer1", "scale", V(0.5f, 1f, 0.5f), V(1.8f, 1f, 1.8f), 1.2f)))),
            ("he_trails-call_he_trails", Def("he_trails", "call_he_trails", "he_trails", null, Seq("",
                Fling("fly_trail1", 0f, 359f, 35f, 65f, 9f, 13f, 3.5f),
                Fling("fly_trail2", 0f, 359f, 35f, 65f, 9f, 13f, 3.5f),
                Fling("fly_trail3", 0f, 359f, 35f, 65f, 9f, 13f, 3.5f),
                Fling("fly_trail4", 0f, 359f, 35f, 65f, 9f, 13f, 3.5f),
                Fling("fly_trail5", 0f, 359f, 50f, 80f, 7f, 10f, 3.5f)))),
            ("dum_gunhit-3040ap_gunhit", Def("dum_gunhit", "3040ap_gunhit", "dum_gunhit", null, Seq("",
                Active("dum_gunhit", true),
                FromTo("gunhit_chunk", "scale", V(1f, 1f, 1f), V(1.6f, 1.6f, 1.6f), 0.1f)))),
            ("ballflare.flt-ballflare.flt", Def("ballflare.flt", "ballflare.flt", "ballflare.flt", null, Seq("",
                FromTo("flare_disc", "scale", V(0.5f, 0.5f, 0.5f), V(6f, 6f, 6f), 1.5f)))),
            ("probe_torpbody-probe_torpedo_trail", Def("probe_torpbody", "probe_torpedo_trail", "probe_torpbody",
                Seq("", Active("atbody", true), Active("rightwing", false), Active("leftwing", false), Active("atprop", false)),
                Seq("probe_unfold",
                    Active("rightwing", true, "Animation", 3.5f),
                    Active("leftwing", true, "Animation", 3.5f),
                    Active("atprop", true, "Animation", 4f)))),
            ("player-spinprops", Def("player", "spinprops", null, null, Seq("",
                Active("prop1", true),
                Active("staticprop1", false),
                Spin("prop1", V(0f, 0f, 30f))))),
            ("player-startprops", Def("player", "startprops", null, null, Seq("",
                Ev("Sound", O("name", "snd_propstart", "at_node", "prop1")),
                Active("staticprop1", true),
                Active("prop1", true, "Animation", 1.5f),
                Active("staticprop1", false, "Animation", 1.5f)))),
            ("player-agyro_rotors", Def("player", "agyro_rotors", null,
                Seq("", Active("prop1", true), Active("rotor1", true), Active("staticprop1", false)),
                Seq("", Spin("prop1", V(0f, 0f, 24f)), Spin("rotor1", V(0f, 18f, 0f))))),
            ("player-stopprops", Def("player", "stopprops", null, null, Seq("",
                Active("staticprop1", true),
                Active("prop1", false)))),
            ("player-pdpanel4", Def("player", "pdpanel4", null, null, Seq("", Call("gimmeflakes", "pdp4")))),
            ("player-pdpanel5", Def("player", "pdpanel5", null, null, Seq("", Call("gimmeflakes", "pdp5")))),
            ("player-pdpanel7", Def("player", "pdpanel7", null, null, Seq("",
                Call("gimmeflakes", "pdp7", V(2f, 0f, 0f)),
                Call("gimmeflakes", "pdp7", V(-2f, 0f, 0f), "Animation", 0.2f),
                Call("gimmeflakes", "pdp7", V(-2f, 0f, 0f), "Animation", 0.5f),
                Call("gimmeflakes", "pdp7", V(-2f, 0f, 0f), "Animation", 0.9f)))),
            ("planeflakes-gimmeflakes", Def("planeflakes", "gimmeflakes", "planeflakes", null, Seq("",
                Fling("flake1", 0f, 120f, 20f, 50f, 4f, 7f, 1f),
                Fling("flake2", 120f, 240f, 20f, 50f, 4f, 7f, 1f),
                Fling("flake3", 240f, 359f, 20f, 50f, 4f, 7f, 1f)))),
            ("sonic_effect-sonic_ground_effect", Def("sonic_effect", "sonic_ground_effect", "sonic_effect", null, Seq("",
                Call("ring_up1", "sonic_effect"),
                Call("ring_up2", "sonic_effect", V(0f, 3f, 0f)),
                FromTo("sonic_emit1", "translate", V(0f, 0.5f, 0f), V(0f, 8f, 0f), 1.5f)))),
            ("sonic_ring1-ring_up1", Ring("sonic_ring1", "ring_up1", V(0.4f, 1f, 0.4f), V(3f, 1f, 3f), 1.2f)),
            ("sonic_ring2-ring_up2", Ring("sonic_ring2", "ring_up2", V(0.6f, 1f, 0.6f), V(2.4f, 1f, 2.4f), 0.9f)),
            ("player-player_crash_water", Def("player", "player_crash_water", "player", null, Seq("probe_crash_water",
                Ev("CallAnimation", O("name", "call_crash_trails")),
                Ev("CallAnimation", O("name", "large_10sec_fire")),
                Ev("CallAnimation", O("name", "plane_big_splash", "wait_for_completion", true)),
                Ev("CallAnimation", O("name", "large_steam_spray"))))),
            ("carnage_trails-call_crash_trails", Def("carnage_trails", "call_crash_trails", "carnage_trails", null, Seq("",
                FromTo("piece1", "translate", V(0f, 0f, 0f), V(0f, 2f, 0f), 1f)))),
            ("large_fire-large_10sec_fire", Def("large_fire", "large_10sec_fire", "large_fire", null, Seq("",
                FromTo("piece2", "scale", V(1f, 1f, 1f), V(2f, 2f, 2f), 2f)))),
            ("huge_splash_model-plane_big_splash", Def("huge_splash_model", "plane_big_splash", "huge_splash_model", null, Seq("",
                Puffer("splasher", "sp_1", 0.05f, 4f, R(0.4f, 0.9f), R(1f, 3f), "smoke101"),
                Active("sp_1", false),
                FromTo("splash_polys", "scale", V(0.2f, 0.2f, 0.2f), V(1f, 1f, 1f), 0.5f),
                Ev("PufferState", O("name", "splasher", "active_state", 0f, "at_node", "sp_1")),
                FromTo("ripple1", "scale", V(1f, 1f, 1f), V(1.4f, 1.4f, 1.4f), 2.5f)))),
            ("white_water_impact-large_steam_spray", Def("white_water_impact", "large_steam_spray", "white_water_impact", null, Seq("",
                FromTo("piece3", "scale", V(0.5f, 0.5f, 0.5f), V(1.5f, 1.5f, 1.5f), 1f)))),
            ("m_bld_healthy-m_build01", Def("m_bld_healthy", "m_build01", null, null,
                Seq("",
                    Active("m_bld_healthy", false),
                    Active("m_bld_destroyed", true),
                    Puffer("trailpuffer3", "part3", 0.1f, 1f, R(1f, 1.5f), R(1f, 2f), "smoke102"),
                    Ev("ObjectMotion", O("node", "part3",
                        "translation", O("initial", V(0f, 12f, 5f), "delta", V(0f, 0f, 0f), "rnd_xz", V(0f, 0f, 1f)),
                        "gravity", O("value", -9.8f), "run_time", 2.4f))),
                Seq("sparkout3", Active("part3", false, "Sequence", 1.8f)))),
            ("player-bullet1", Def("player", "bullet1", null, null,
                Seq("",
                    Ev("If", O("condition", O("PlayerFirstPerson", 0f))),
                    O("data", "Else"),
                    Ev("CallAnimation", O("name", "two_bulletholes_a")),
                    O("data", "Endif")),
                Seq("", Active("probe_bul1", true)))),
            ("player-two_bulletholes_a", Def("player", "two_bulletholes_a", null, null, Seq("",
                Active("probe_hole1", true)))),
        };

        var parsed = defs.Select(d => AnimDefinition.Parse(new AnimData(d.Def), d.File)).ToList();
        return AnimArchive.FromDecoded(Folder, parsed, new List<(string, SiScript)>());
    }

    // A property tree from key/value pairs; a null value leaves its key out, as the extraction does.
    private static Dictionary<string, object?> O(params object?[] pairs)
    {
        var d = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (int i = 0; i < pairs.Length; i += 2)
        {
            if (pairs[i + 1] != null)
            {
                d[(string)pairs[i]!] = pairs[i + 1];
            }
        }

        return d;
    }

    private static Dictionary<string, object?> V(float x, float y, float z) => O("x", x, "y", y, "z", z);

    private static Dictionary<string, object?> R(float min, float max) => O("min", min, "max", max);

    private static Dictionary<string, object?> Def(string name, string animName, string? root,
        Dictionary<string, object?>? reset, params Dictionary<string, object?>[] sequences) =>
        O("name", name, "anim_name", animName, "anim_root_name", root, "activation", "OnCall",
            "reset_state", reset, "sequences", sequences.Cast<object?>().ToList());

    private static Dictionary<string, object?> Seq(string name, params Dictionary<string, object?>[] events) =>
        O("name", name, "seq_state", "Initial", "events", events.Cast<object?>().ToList());

    private static Dictionary<string, object?> Ev(string kind, Dictionary<string, object?> payload,
        string? offset = null, float time = 0f) =>
        O("start", offset == null ? null : O("offset", offset, "time", time), "data", O(kind, payload));

    private static Dictionary<string, object?> Active(string node, bool state, string? offset = null, float time = 0f) =>
        Ev("ObjectActiveState", O("node", node, "state", state), offset, time);

    private static Dictionary<string, object?> FromTo(string node, string channel,
        Dictionary<string, object?> from, Dictionary<string, object?> to, float runTime) =>
        Ev("ObjectMotionFromTo", O("node", node, channel, O("from", from, "to", to), "run_time", runTime));

    private static Dictionary<string, object?> Call(string name, string atNode,
        Dictionary<string, object?>? position = null, string? offset = null, float time = 0f) =>
        Ev("CallAnimation", O("name", name, "parameters", O("AtNode", O("node", atNode, "position", position))), offset, time);

    // A part thrown at a random heading and pitch under gravity, from the sequence's start.
    private static Dictionary<string, object?> Fling(string node, float xzMin, float xzMax, float yMin, float yMax,
        float speedMin, float speedMax, float runTime) =>
        Ev("ObjectMotion", O("node", node,
            "translation_range", O("xz", R(xzMin, xzMax), "y", R(yMin, yMax), "initial", R(speedMin, speedMax), "delta", R(0f, 0f)),
            "gravity", O("value", -9.8f), "run_time", runTime), "Sequence", 0f);

    private static Dictionary<string, object?> Spin(string node, Dictionary<string, object?> rate) =>
        Ev("ObjectMotion", O("node", node, "xyz_rotation", O("initial", rate, "delta", V(0f, 0f, 0f))));

    private static Dictionary<string, object?> Puffer(string name, string atNode, float interval, float number,
        Dictionary<string, object?> lifetime, Dictionary<string, object?> size, string texture) =>
        Ev("PufferState", O("name", name, "active_state", 1f, "at_node", atNode,
            "interval_garbage", O("interval_value", interval), "number", number,
            "lifetime_range", lifetime, "size_range", size,
            "textures", new List<object?> { O("name", texture) }));

    // A ring that grows, fades out and switches off, reset to shown, opaque and unscaled.
    private static Dictionary<string, object?> Ring(string root, string animName,
        Dictionary<string, object?> from, Dictionary<string, object?> to, float runTime) =>
        Def(root, animName, root,
            Seq("",
                Active("sonic_ring", true),
                Ev("ObjectOpacityState", O("node", "sonic_ring", "state", false)),
                Ev("ObjectScaleState", O("node", "sonic_ring", "state", V(1f, 1f, 1f)))),
            Seq("",
                FromTo("sonic_ring", "scale", from, to, runTime),
                Ev("ObjectOpacityState", O("node", "sonic_ring", "state", true, "opacity", 0f)),
                Active("sonic_ring", false)));
}
