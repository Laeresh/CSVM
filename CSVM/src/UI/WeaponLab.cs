using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CSVM.Flight;
using CSVM.Mech3;
using CSVM.Utils;
using Godot;

namespace CSVM.UI;

/// <summary>
/// The static viewer's weapon lab (<c>--viewer --plane</c>, key <b>W</b>): the fourth lab beside
/// the damage (H), livery (L) and mesh (M) labs. It mounts a weapon and fires it, so the muzzle
/// flash, tracer / rocket body and impact can be inspected before the gun/hardpoint configurator
/// exists.
///
/// <para>Weapons are split into two banks matching the game: <b>guns</b> fire from the plane's
/// named <b>gun groups</b> (from the stock loadout — "Inner Wing Guns", …), <b>hardpoint</b>
/// weapons (rockets / ordnance) fire from its <b>pylons</b>. The bank filters both the weapon list
/// and the mount list, so a gun can only fire from a gun group and a rocket only from a pylon.</para>
///
/// <para>It drives the same <see cref="ProjectilePool"/> flight fires — one owned by the lab — so a
/// round runs the identical ballistics. The viewer has no chapter world, so the pool is built
/// without a scene: rockets fly as streaks (no <c>FLYOUT</c> prototype to instance) and impacts show
/// the stand-in spark for guns / the stand-in explosion burst for hardpoints (no real puffer runtime),
/// and there is no <c>DamageSink</c>. To give a round something to hit — the pool's hits come from a
/// per-step world raycast — the lab parks a stand-in target wall ahead of the nose (a
/// <see cref="StaticBody3D"/> with a tagged surface so the pool's classifier picks the matching
/// <c>IMPACT</c> variant), shown only while the lab is engaged so an unadorned <c>--viewer</c>
/// screenshot stays byte-identical.</para>
///
/// <para>"copy CLI args" writes the arguments that reproduce the current selection, as
/// <see cref="LiveryLab"/> does. <see cref="RunSelfTest"/> mounts and fires every one of the 48
/// entries once and reports any that throw.</para>
/// </summary>
public sealed partial class WeaponLab : Node3D
{
    /// <summary>A place on the parked plane a weapon fires from: a named gun group / a pylon / the
    /// synthetic "all" volley. <see cref="Nodes"/> are the live muzzle <see cref="Node3D"/>s;
    /// <see cref="Cli"/> is the <c>--weapon-mount=</c> token (<c>all</c>, <c>g1</c>, <c>pylon1</c>).</summary>
    private readonly struct Mount
    {
        public readonly string Label;
        public readonly string Cli;
        public readonly IReadOnlyList<Node3D> Nodes;

        public Mount(string label, string cli, IReadOnlyList<Node3D> nodes)
        {
            Label = label;
            Cli = cli;
            Nodes = nodes;
        }
    }

    private const int Guns = 0;
    private const int Hardpoints = 1;

    private const float TargetSize = 60f;         // stand-in wall extent, m — wide enough for the spread cone
    private const float MinTargetDist = 15f;
    private const float MaxTargetDist = 1100f;    // far enough to watch a rocket fly its full course
    private const float DefaultTargetDist = 90f;

    // The three surface classes a viewer target can carry (SceneBuilder.SurfaceMeta absent = default).
    private static readonly string[] SurfaceNames = { "default", "water", "buildings" };

    private readonly Node3D _plane;
    private readonly TextureArchive _textures;
    private readonly Camera3D _camera;
    private readonly string _planeModel;

    private readonly List<WeaponDef> _all;        // every weapon, for the self-test
    private readonly List<WeaponDef> _gunWeapons = new();
    private readonly List<WeaponDef> _rocketWeapons = new();
    private readonly List<Mount> _gunMounts = new();
    private readonly List<Mount> _pylonMounts = new();

    private ProjectilePool _pool = null!;

    /// <summary>The lab's own projectile pool, a child of this node — so a session driving the
    /// sim explicitly can step it right after this node, keeping the tree order.</summary>
    public ProjectilePool Pool => _pool;

    private StaticBody3D _target = null!;
    private MeshInstance3D _targetMesh = null!;
    private StandardMaterial3D _targetMat = null!;

    private int _bank = Guns;
    private int _weaponIndex;       // into the current bank's weapon list
    private int _mountIndex;        // into the current bank's mount list
    private int _surfaceIndex;
    private float _targetDist = DefaultTargetDist;
    private bool _showTarget = true;

    // _panel: is the UI shown. _engaged: is the lab live (target visible, firing processed). W
    // toggles both together; --weapon-fire can start engaged with the panel hidden, for a clean
    // firing screenshot with no overlay.
    private bool _panel;
    private bool _engaged;
    private bool _autoFire;
    private bool _spaceHeld;
    private bool _wasFiring;
    private float _fireAccum;

    private CanvasLayer _ui = null!;
    private Label _bankLabel = null!;
    private Label _weaponLabel = null!;
    private Label _weaponDetail = null!;
    private Label _mountLabel = null!;
    private Label _surfaceLabel = null!;
    private Label _targetLabel = null!;
    private Label _cliLabel = null!;
    private CheckButton _autoFireToggle = null!;
    private CheckButton _targetToggle = null!;
    private bool _suppress; // set while rewriting widgets from a state change

    /// <summary><c>--weapon-lab</c>: open the panel at launch (hidden by default so an unadorned
    /// <c>--viewer</c> screenshot is byte-identical).</summary>
    public bool DebugShow { get; init; }

    /// <summary><c>--weapon-lab=&lt;wep_id&gt;</c>: the weapon selected at launch (sets the bank).</summary>
    public string? InitialWeapon { get; init; }

    /// <summary><c>--weapon-mount=&lt;name&gt;</c>: the mount selected at launch.</summary>
    public string? InitialMount { get; init; }

    /// <summary><c>--weapon-fire</c>: start auto-firing (engages the lab even with the panel hidden,
    /// so <c>--weapon-fire --screenshot</c> captures tracers/impact without the overlay).</summary>
    public bool AutoFireAtStart { get; init; }

    public WeaponLab(Node3D plane, WeaponDefs weapons, Loadout? loadout,
        TextureArchive textures, Camera3D camera, string planeModel)
    {
        _plane = plane;
        _textures = textures;
        _camera = camera;
        _planeModel = planeModel;
        _all = new List<WeaponDef>(weapons.All);
        foreach (var w in _all)
        {
            (w.IsGun ? _gunWeapons : _rocketWeapons).Add(w);
        }
        BuildMounts(loadout);
        Name = "weapon_lab";
        // The pool + target build now (not in _Ready) so RunSelfTest works synchronously right after
        // the lab joins the tree, before _Ready runs; child nodes may be built pre-tree.
        BuildScene();
    }

    public override void _Ready()
    {
        _pool.Listener = _camera;
        BuildUi();
        ResolveInitialSelection();
        _autoFire = AutoFireAtStart;
        _panel = DebugShow;
        _engaged = DebugShow || AutoFireAtStart;
        PlaceTarget();
        SyncState();
    }

    // ---- weapon + mount resolution -----------------------------------------------------------

    private List<WeaponDef> BankWeapons => _bank == Guns ? _gunWeapons : _rocketWeapons;
    private List<Mount> BankMounts => _bank == Guns ? _gunMounts : _pylonMounts;
    private WeaponDef? SelectedWeapon => _weaponIndex < BankWeapons.Count ? BankWeapons[_weaponIndex] : null;

    /// <summary>Builds the two mount banks from the plane's stock loadout: the named gun groups
    /// (their resolved muzzle firepoints) for guns, the pylons for hardpoints — each with an "all"
    /// volley first. Falls back to the raw firepoint/pylon markers when no loadout is bound.</summary>
    private void BuildMounts(Loadout? loadout)
    {
        if (loadout != null && loadout.Guns.Count > 0)
        {
            var allGuns = new List<Node3D>();
            foreach (var g in loadout.Guns)
            {
                allGuns.AddRange(g.Muzzles);
            }
            if (allGuns.Count > 0)
            {
                _gunMounts.Add(new Mount("all gun groups", "all", allGuns));
            }
            foreach (var g in loadout.Guns)
            {
                string label = g.IsTurret ? $"{g.Mount} (turret)" : g.Mount;
                _gunMounts.Add(new Mount(label, $"g{g.Slot}", g.Muzzles));
            }
        }
        if (loadout != null && loadout.Hardpoints.Count > 0)
        {
            var allPylons = new List<Node3D>();
            foreach (var h in loadout.Hardpoints)
            {
                allPylons.Add(h.Pylon);
            }
            _pylonMounts.Add(new Mount($"all pylons ({allPylons.Count})", "all", allPylons));
            foreach (var h in loadout.Hardpoints)
            {
                _pylonMounts.Add(new Mount($"pylon{h.Index}", $"pylon{h.Index}", new[] { h.Pylon }));
            }
        }
        // No loadout (or a plane the table omits): fall back to the raw marker rig so W still works.
        if (_gunMounts.Count == 0 || _pylonMounts.Count == 0)
        {
            CollectRawMarkers(_plane, out var fps, out var pylons);
            if (_gunMounts.Count == 0 && fps.Count > 0)
            {
                var all = new List<Node3D>();
                foreach (var (_, node) in fps)
                {
                    all.Add(node);
                }
                _gunMounts.Add(new Mount($"all firepoints ({all.Count})", "all", all));
                foreach (var (ord, node) in fps)
                {
                    _gunMounts.Add(new Mount($"firepoint{ord}", $"firepoint{ord}", new[] { node }));
                }
            }
            if (_pylonMounts.Count == 0 && pylons.Count > 0)
            {
                var all = new List<Node3D>();
                foreach (var (_, node) in pylons)
                {
                    all.Add(node);
                }
                _pylonMounts.Add(new Mount($"all pylons ({all.Count})", "all", all));
                foreach (var (ord, node) in pylons)
                {
                    _pylonMounts.Add(new Mount($"pylon{ord}", $"pylon{ord}", new[] { node }));
                }
            }
        }
    }

    /// <summary>The raw firepoint/pylon marker nodes, each with its ordinal, sorted — the
    /// no-loadout fallback for a plane the stock table omits.</summary>
    private static void CollectRawMarkers(Node3D plane,
        out List<(int Ord, Node3D Node)> firepoints, out List<(int Ord, Node3D Node)> pylons)
    {
        var fp = new List<(int, Node3D)>();
        var py = new List<(int, Node3D)>();
        void Walk(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                if (child is Node3D n3d && n3d.HasMeta(AnimRuntime.NameMeta)
                    && MarkerRig.Classify(n3d.GetMeta(AnimRuntime.NameMeta).AsString(), out var kind, out int ord))
                {
                    if (kind == MarkerRig.MarkerKind.Firepoint)
                    {
                        fp.Add((ord, n3d));
                    }
                    else if (kind == MarkerRig.MarkerKind.Pylon)
                    {
                        py.Add((ord, n3d));
                    }
                }
                Walk(child);
            }
        }
        Walk(plane);
        fp.Sort(static (a, b) => a.Item1.CompareTo(b.Item1));
        py.Sort(static (a, b) => a.Item1.CompareTo(b.Item1));
        firepoints = fp;
        pylons = py;
    }

    private void ResolveInitialSelection()
    {
        if (InitialWeapon != null)
        {
            int gi = IndexOfWeapon(_gunWeapons, InitialWeapon);
            int ri = IndexOfWeapon(_rocketWeapons, InitialWeapon);
            if (gi >= 0)
            {
                _bank = Guns;
                _weaponIndex = gi;
            }
            else if (ri >= 0)
            {
                _bank = Hardpoints;
                _weaponIndex = ri;
            }
            else
            {
                GD.Print($"weapon lab: --weapon-lab names unknown weapon '{InitialWeapon}'");
            }
        }
        if (InitialMount != null)
        {
            var mounts = BankMounts;
            for (int i = 0; i < mounts.Count; i++)
            {
                if (string.Equals(mounts[i].Cli, InitialMount, StringComparison.OrdinalIgnoreCase))
                {
                    _mountIndex = i;
                    break;
                }
            }
        }
    }

    private static int IndexOfWeapon(List<WeaponDef> list, string id)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return -1;
    }

    // ---- scene (pool + target) ---------------------------------------------------------------

    private void BuildScene()
    {
        // No world scene: rockets fly streak-only, gun impacts show the spark, hardpoint impacts show
        // the pool's explosion stand-in (no real puffer runtime), and there is no DamageSink.
        _pool = new ProjectilePool(_textures, null, null);
        AddChild(_pool);

        var shape = new BoxShape3D { Size = new Vector3(TargetSize, TargetSize, 0.5f) };
        _target = new StaticBody3D { Name = "weapon_target", Visible = false };
        _target.AddChild(new CollisionShape3D { Shape = shape });
        _targetMat = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            AlbedoColor = SurfaceColor(_surfaceIndex),
        };
        _targetMesh = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(TargetSize, TargetSize, 0.5f) },
            MaterialOverride = _targetMat,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        _target.AddChild(_targetMesh);
        AddChild(_target);
        ApplySurfaceTag();
    }

    /// <summary>Parks the target wall <see cref="_targetDist"/> metres ahead of the plane's nose
    /// (plane frame is nose −Z), broad face square to the fire direction. Requires the lab to be in
    /// the tree (reads the plane's global transform).</summary>
    private void PlaceTarget()
    {
        if (!IsInsideTree())
        {
            return;
        }
        var xf = _plane.GlobalTransform;
        var forward = -xf.Basis.Z.Normalized();
        var up = Mathf.Abs(forward.Dot(Vector3.Up)) > 0.99f ? Vector3.Right : Vector3.Up;
        var pos = xf.Origin + forward * _targetDist;
        // LookingAt aims local −Z along the argument; −forward points the box's local +Z back down
        // the fire direction, so its broad face is square to the incoming rounds.
        _target.GlobalTransform = new Transform3D(Basis.LookingAt(-forward, up), pos);
    }

    private void ApplySurfaceTag()
    {
        string surface = SurfaceNames[_surfaceIndex];
        if (surface == "default")
        {
            if (_target.HasMeta(SceneBuilder.SurfaceMeta))
            {
                _target.RemoveMeta(SceneBuilder.SurfaceMeta);
            }
        }
        else
        {
            _target.SetMeta(SceneBuilder.SurfaceMeta, surface);
        }
        _targetMat.AlbedoColor = SurfaceColor(_surfaceIndex);
    }

    private static Color SurfaceColor(int index) => index switch
    {
        1 => new Color(0.3f, 0.55f, 0.85f, 0.4f),  // water
        2 => new Color(0.6f, 0.5f, 0.4f, 0.5f),    // buildings
        _ => new Color(0.6f, 0.6f, 0.62f, 0.4f),   // default
    };

    // ---- firing ------------------------------------------------------------------------------

    public override void _PhysicsProcess(double delta)
    {
        float dt = GameClock.Current?.PhysicsDt(delta) ?? (float)delta;
        if (dt <= 0f)
        {
            return;   // the session drives SimStep itself this frame (see GameClock.PhysicsDt)
        }
        SimStep(dt);
    }

    /// <summary>One auto-fire step: advance the trigger clock and fire whatever volleys it owes.
    /// Public because a non-realtime clock has the session call this instead of the physics tick.</summary>
    public void SimStep(float dt)
    {
        bool firing = _engaged && (_autoFire || _spaceHeld);
        if (!firing || SelectedWeapon is not { } w || BankMounts.Count == 0)
        {
            _wasFiring = false;
            return;
        }
        // A firing rate for the loop: the flagless "fake weapon" specials carry FireRate 0, so hold
        // still pulses them at a visible cadence rather than never firing.
        float rate = w.FireRate > 0f ? w.FireRate : 2f;
        float interval = 1f / rate;
        if (!_wasFiring)
        {
            _wasFiring = true;
            _fireAccum = interval; // the first shot leaves the moment the trigger goes down
        }
        _fireAccum += dt;
        int guard = 0;
        while (_fireAccum >= interval && guard++ < 64)
        {
            _fireAccum -= interval;
            FireVolley();
        }
    }

    /// <summary>Fires one round of the selected weapon from each muzzle of the selected mount, from a
    /// standstill (no inherited velocity — the plane is parked).</summary>
    private void FireVolley()
    {
        if (SelectedWeapon is not { } w || BankMounts.Count == 0)
        {
            return;
        }
        foreach (var n in BankMounts[_mountIndex].Nodes)
        {
            _pool.Spawn(w, n.GlobalTransform, Vector3.Zero);
        }
    }

    /// <summary>Mounts and fires every one of the 48 weapons once — each from a mount of its own
    /// class (a gun from the gun groups, a hardpoint weapon from the pylons) — catching any that
    /// throw. Returns the report (the caller also writes it).</summary>
    public string RunSelfTest()
    {
        var sb = new StringBuilder();
        var gunMount = _gunMounts.Count > 0 ? _gunMounts[0] : (Mount?)null;
        var pylonMount = _pylonMounts.Count > 0 ? _pylonMounts[0] : (Mount?)null;
        sb.AppendLine($"weapon-test: plane {_planeModel}, {_all.Count} weapons "
                      + $"({_gunWeapons.Count} gun, {_rocketWeapons.Count} hardpoint); "
                      + $"gun mount '{gunMount?.Label ?? "none"}', pylon mount '{pylonMount?.Label ?? "none"}'");
        int ok = 0, err = 0, skip = 0;
        foreach (var w in _all)
        {
            // A hardpoint weapon fires from a pylon; a gun from a gun group. Fall back to the other
            // bank's mount only if this plane has none of its own (never happens for the 11).
            var mount = w.IsGun ? (gunMount ?? pylonMount) : (pylonMount ?? gunMount);
            string kind = w.IsRocket ? "rocket" : w.IsGun ? "gun" : "other";
            if (mount is not { } m)
            {
                skip++;
                sb.AppendLine($"  {w.Id,-7} {Trim(w.Name, 10),-10} {kind,-6} SKIP — no mount on this plane");
                continue;
            }
            try
            {
                foreach (var n in m.Nodes)
                {
                    _pool.Spawn(w, n.GlobalTransform, Vector3.Zero);
                }
                ok++;
                sb.AppendLine($"  {w.Id,-7} {Trim(w.Name, 10),-10} {kind,-6} → {m.Label,-20} fired {m.Nodes.Count} round(s) OK");
            }
            catch (Exception e)
            {
                err++;
                sb.AppendLine($"  {w.Id,-7} {Trim(w.Name, 10),-10} ERROR: {e.Message}");
            }
        }
        sb.AppendLine($"weapon-test: {ok}/{_all.Count} fired OK, {err} error(s), {skip} skipped");
        return sb.ToString();
    }

    // ---- input -------------------------------------------------------------------------------

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Echo: false } key)
        {
            return;
        }
        if (key.Keycode == Key.W && key.Pressed)
        {
            SetPanel(!_panel);
        }
        else if (key.Keycode == Key.Space && _panel)
        {
            // Space fires only while the panel is up (interactive); a hidden-panel firing screenshot
            // uses --weapon-fire / the auto-fire toggle instead.
            _spaceHeld = key.Pressed;
        }
    }

    // ---- panel + state -----------------------------------------------------------------------

    private void SetPanel(bool on)
    {
        _panel = on;
        _engaged = on;
        if (!on)
        {
            _spaceHeld = false;
            _pool.Clear(); // no tracers hang in the air once the lab is put away
        }
        SyncState();
    }

    /// <summary>Pushes the current state onto the visuals + widgets: panel visibility, target
    /// visibility, and every readout/toggle.</summary>
    private void SyncState()
    {
        _suppress = true;
        _ui.Visible = _panel;
        _target.Visible = _engaged && _showTarget;

        _bankLabel.Text = _bank == Guns
            ? $"bank: GUNS  ({_gunWeapons.Count})"
            : $"bank: HARDPOINTS  ({_rocketWeapons.Count})";
        if (SelectedWeapon is { } w)
        {
            string kind = w.IsRocket ? "ROCKET" : w.IsGun ? "GUN" : "SPECIAL";
            _weaponLabel.Text = $"{w.Id}  {w.Name}  [{kind}]   [{_weaponIndex + 1}/{BankWeapons.Count}]";
            _weaponDetail.Text = WeaponSummary(w);
        }
        else
        {
            _weaponLabel.Text = "(no weapon)";
            _weaponDetail.Text = "";
        }
        var mounts = BankMounts;
        _mountLabel.Text = mounts.Count > 0
            ? $"{mounts[_mountIndex].Label}   [{_mountIndex + 1}/{mounts.Count}]"
            : (_bank == Guns ? "(no gun groups)" : "(no pylons)");
        _surfaceLabel.Text = $"target surface: {SurfaceNames[_surfaceIndex]}";
        _targetLabel.Text = $"target {(_showTarget ? "on" : "off")} @ {_targetDist:0} m";
        _autoFireToggle.ButtonPressed = _autoFire;
        _targetToggle.ButtonPressed = _showTarget;
        _cliLabel.Text = CliArgs();
        _suppress = false;
    }

    // Decimals are formatted invariantly (a dot, never a locale comma), like the dump tools.
    private static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

    private static string WeaponSummary(WeaponDef w)
    {
        var parts = new List<string>();
        if (w.Caliber is { } cal)
        {
            parts.Add($".{cal}");
        }
        if (w.FireRate > 0f)
        {
            parts.Add($"{F(w.FireRate)}/s");
        }
        if (w.Velocity is { } v)
        {
            parts.Add($"{v:0} m/s");
        }
        if (w.HealthDamage is { } hd)
        {
            parts.Add($"dmg h{F(hd)}/a{F(w.ArmorDamage ?? 0f)}");
        }
        else if (w.Damage is { } dm)
        {
            parts.Add($"dmg {F(dm)}");
        }
        if (w.ClusterSize is { } cs)
        {
            parts.Add($"×{cs}");
        }
        if (w.Range is { } r)
        {
            parts.Add($"range {r:0}");
        }
        foreach (var flag in new[]
                 {
                     w.HighExplosive ? "HE" : null, w.Sonic ? "sonic" : null, w.Flash ? "flash" : null,
                     w.BeeperSeeker ? "seeker" : null, w.IsGuided ? "guided" : null, w.Torpedo ? "torpedo" : null,
                     w.Crater ? "crater" : null, w.Rear ? "rear" : null,
                 })
        {
            if (flag != null)
            {
                parts.Add(flag);
            }
        }
        return string.Join(" · ", parts);
    }

    /// <summary>The arguments that reproduce this selection — the lab's output. The bank is implied
    /// by the weapon's class, and the "all" mount is the default, so it is emitted only when a
    /// specific mount is chosen.</summary>
    private string CliArgs()
    {
        var sb = new StringBuilder();
        sb.Append("--viewer --plane=").Append(_planeModel);
        if (SelectedWeapon is { } w)
        {
            sb.Append(" --weapon-lab=").Append(w.Id);
        }
        var mounts = BankMounts;
        if (mounts.Count > 0 && mounts[_mountIndex].Cli != "all")
        {
            sb.Append(" --weapon-mount=").Append(mounts[_mountIndex].Cli);
        }
        if (_autoFire)
        {
            sb.Append(" --weapon-fire");
        }
        return sb.ToString();
    }

    // ---- edits -------------------------------------------------------------------------------

    private void StepBank(int delta)
    {
        _bank = (_bank + delta + 2) % 2;
        _weaponIndex = 0;
        _mountIndex = 0;
        ResetFire();
        SyncState();
    }

    private void StepWeapon(int delta)
    {
        if (BankWeapons.Count == 0)
        {
            return;
        }
        _weaponIndex = Mathf.PosMod(_weaponIndex + delta, BankWeapons.Count);
        ResetFire(); // the new rate takes effect on the next shot
        SyncState();
    }

    private void StepMount(int delta)
    {
        if (BankMounts.Count == 0)
        {
            return;
        }
        _mountIndex = Mathf.PosMod(_mountIndex + delta, BankMounts.Count);
        SyncState();
    }

    private void StepSurface(int delta)
    {
        _surfaceIndex = Mathf.PosMod(_surfaceIndex + delta, SurfaceNames.Length);
        ApplySurfaceTag();
        SyncState();
    }

    private void SetTargetDist(float metres)
    {
        _targetDist = Mathf.Clamp(metres, MinTargetDist, MaxTargetDist);
        PlaceTarget();
        SyncState();
    }

    private void ResetFire()
    {
        _fireAccum = 0f;
        _wasFiring = false;
    }

    // ---- ui ----------------------------------------------------------------------------------

    private void BuildUi()
    {
        _ui = new CanvasLayer { Layer = 1, Visible = false };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Bottom-right, the one free corner — DamageLab is top-left, LiveryLab top-right, MeshLab
        // bottom-left, so all four can be open at once without overlapping.
        var panel = new PanelContainer { SelfModulate = new Color(1, 1, 1, 0.85f) };
        panel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.GrowVertical = Control.GrowDirection.Begin;
        panel.Position = new Vector2(-8, -8);

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 10);
        }
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);

        box.AddChild(new Label { Text = "WEAPON LAB" });
        box.AddChild(Small("W hides · Space fires · orbit + zoom to watch the impact"));
        box.AddChild(Small("no world: rockets fly as streaks, impacts are stand-ins"));

        // bank stepper (guns fire from gun groups, hardpoints from pylons)
        _bankLabel = new Label { CustomMinimumSize = new Vector2(300, 0), VerticalAlignment = VerticalAlignment.Center };
        var bankRow = new HBoxContainer();
        bankRow.AddChild(StepButton("<", () => StepBank(-1)));
        bankRow.AddChild(_bankLabel);
        bankRow.AddChild(StepButton(">", () => StepBank(1)));
        box.AddChild(bankRow);

        // weapon stepper
        _weaponLabel = new Label { CustomMinimumSize = new Vector2(330, 0), VerticalAlignment = VerticalAlignment.Center };
        var weaponRow = new HBoxContainer();
        weaponRow.AddChild(StepButton("<", () => StepWeapon(-1)));
        weaponRow.AddChild(_weaponLabel);
        weaponRow.AddChild(StepButton(">", () => StepWeapon(1)));
        box.AddChild(weaponRow);
        _weaponDetail = Small("");
        _weaponDetail.CustomMinimumSize = new Vector2(330, 0);
        _weaponDetail.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_weaponDetail);

        box.AddChild(Separator());

        // mount stepper
        box.AddChild(Small("mount"));
        _mountLabel = new Label { CustomMinimumSize = new Vector2(280, 0), VerticalAlignment = VerticalAlignment.Center };
        var mountRow = new HBoxContainer();
        mountRow.AddChild(StepButton("<", () => StepMount(-1)));
        mountRow.AddChild(_mountLabel);
        mountRow.AddChild(StepButton(">", () => StepMount(1)));
        box.AddChild(mountRow);

        box.AddChild(Separator());

        // target controls
        _targetLabel = new Label();
        box.AddChild(_targetLabel);
        var distSlider = new HSlider
        {
            MinValue = MinTargetDist,
            MaxValue = MaxTargetDist,
            Step = 5,
            Value = _targetDist,
            CustomMinimumSize = new Vector2(220, 0),
        };
        distSlider.ValueChanged += v =>
        {
            if (!_suppress)
            {
                SetTargetDist((float)v);
            }
        };
        box.AddChild(distSlider);

        _surfaceLabel = new Label { CustomMinimumSize = new Vector2(200, 0), VerticalAlignment = VerticalAlignment.Center };
        var surfaceRow = new HBoxContainer();
        surfaceRow.AddChild(StepButton("<", () => StepSurface(-1)));
        surfaceRow.AddChild(_surfaceLabel);
        surfaceRow.AddChild(StepButton(">", () => StepSurface(1)));
        box.AddChild(surfaceRow);

        _targetToggle = new CheckButton { Text = "show target" };
        _targetToggle.Toggled += on =>
        {
            if (!_suppress)
            {
                _showTarget = on;
                SyncState();
            }
        };
        box.AddChild(_targetToggle);

        box.AddChild(Separator());

        // fire controls
        _autoFireToggle = new CheckButton { Text = "auto-fire (hold trigger)" };
        _autoFireToggle.Toggled += on =>
        {
            if (!_suppress)
            {
                _autoFire = on;
                SyncState();
            }
        };
        box.AddChild(_autoFireToggle);

        var fire = new Button { Text = "fire once" };
        fire.Pressed += () =>
        {
            if (_engaged)
            {
                FireVolley();
            }
        };
        box.AddChild(fire);

        box.AddChild(Separator());

        _cliLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(330, 0) };
        _cliLabel.AddThemeFontSizeOverride("font_size", 11);
        box.AddChild(_cliLabel);

        var copy = new Button { Text = "copy CLI args" };
        copy.Pressed += () =>
        {
            var args = CliArgs();
            DisplayServer.ClipboardSet(args);
            GD.Print($"[weapon] {args}");
        };
        box.AddChild(copy);

        margin.AddChild(box);
        panel.AddChild(margin);
        root.AddChild(panel);
        _ui.AddChild(root);
        AddChild(_ui);
    }

    private static Button StepButton(string text, Action pressed)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(28, 0) };
        b.Pressed += pressed;
        return b;
    }

    private static HSeparator Separator() => new();

    private static Label Small(string text)
    {
        var label = new Label { Text = text, Modulate = new Color(1, 1, 1, 0.65f) };
        label.AddThemeFontSizeOverride("font_size", 11);
        return label;
    }

    private static string Trim(string s, int n) => s.Length <= n ? s : s[..n];
}
