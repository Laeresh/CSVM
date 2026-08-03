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
/// The weapon lab's panel (<c>--weapon-lab</c>, key <b>B</b>): the configurator for the held
/// aircraft's <b>live</b> loadout. It owns no weapon of its own and fires nothing — every stepper
/// writes into the <see cref="Flight.FlightController"/>'s bound <see cref="Loadout"/>, and the
/// aircraft's own trigger then fires exactly what free flight fires (decision 3).
///
/// <para>Weapons are split into two banks matching the game: <b>guns</b> arm the plane's named
/// <b>gun groups</b>, <b>hardpoint</b> weapons (rockets / ordnance) arm its <b>pylons</b>. Picking a
/// gun assigns it to the selected group and refills that group's ammo; picking a hardpoint weapon
/// re-arms every pylon and <b>rebuilds the mounted ordnance models</b>, so the wings show the new
/// type. The mount stepper drives the controller's own gun/pylon selectors
/// (<see cref="Flight.FlightController.SelectGunGroup"/> / <see cref="Flight.FlightController.SelectPylon"/>),
/// so the trigger fires from the mount the panel names. "reset to stock" puts the fit the session
/// launched with back.</para>
///
/// <para>In a lab session the bound loadout is <see cref="Loadout.ForRig"/>'s — every firepoint and
/// every pylon on the airframe — so any of the 48 weapons reaches any mount without editing
/// <c>stock_loadouts.json</c>. "copy CLI args" writes the arguments that reproduce the current
/// selection, as <see cref="LiveryLab"/> does.</para>
///
/// <para>The parked host (<c>--weapon-test</c>) builds the same node without a controller: no live
/// loadout to drive, so the panel's edits are inert and only <see cref="RunSelfTest"/> — the
/// 48-weapon mount-and-fire pass check, which spawns into the caller's pool directly — runs.</para>
/// </summary>
public sealed partial class WeaponLab : Node3D
{
    private const int Guns = 0;
    private const int Hardpoints = 1;

    private readonly Node3D _plane;
    private readonly string _planeModel;
    private readonly WeaponDefs _weapons;

    private readonly List<WeaponDef> _all;        // every weapon, for the self-test
    private readonly List<WeaponDef> _gunWeapons = new();
    private readonly List<WeaponDef> _rocketWeapons = new();
    private readonly List<Mount> _gunMounts = new();
    private readonly List<Mount> _pylonMounts = new();

    // The flight session's held aircraft this panel drives, or null for the parked
    // (--weapon-test) host. Set means there IS a live loadout to write into.
    private readonly Flight.FlightController? _host;
    private readonly Loadout? _loadout;

    // The self-test's spawn target — the caller's pool. Never cleared and never given a listener:
    // in a flight session it is the whole session's pool.
    private readonly ProjectilePool? _pool;

    // The fit the session launched with (stock, or whatever --rocket/--loadout made it), captured
    // before the panel touches anything — what "reset to stock" restores.
    private readonly Dictionary<GunGroup, WeaponDef> _launchGuns = new();
    private WeaponDef? _launchOrdnance;

    private int _bank = Guns;
    private int _weaponIndex;       // into the current bank's weapon list
    private int _mountIndex;        // into the current bank's mount list

    private bool _panel;
    private bool _autoFire;
    private int _cycleTick;

    private CanvasLayer _ui = null!;
    private Label _bankLabel = null!;
    private Label _weaponLabel = null!;
    private Label _weaponDetail = null!;
    private Label _mountLabel = null!;
    private Label _ammoLabel = null!;
    private Label _cliLabel = null!;
    private CheckButton _autoFireToggle = null!;
    private CheckButton _infiniteAmmoToggle = null!;
    private bool _suppress; // set while rewriting widgets from a state change

    public WeaponLab(Node3D plane, WeaponDefs weapons, Loadout? loadout, string planeModel,
        Flight.FlightController? host = null, ProjectilePool? pool = null)
    {
        _plane = plane;
        _weapons = weapons;
        _planeModel = planeModel;
        _host = host;
        _loadout = loadout;
        _pool = pool;
        _all = new List<WeaponDef>(weapons.All);
        foreach (var w in _all)
        {
            (w.IsGun ? _gunWeapons : _rocketWeapons).Add(w);
        }
        BuildMounts(loadout);
        RememberLaunchFit(loadout);
        Name = "weapon_lab";
    }

    /// <summary><c>--weapon-lab</c>: open the panel at launch.</summary>
    public bool DebugShow { get; init; }

    /// <summary><c>--weapon-lab=&lt;wep_id&gt;</c>: the weapon mounted at launch (sets the bank).</summary>
    public string? InitialWeapon { get; init; }

    /// <summary><c>--weapon-mount=&lt;name&gt;</c>: the mount selected at launch.</summary>
    public string? InitialMount { get; init; }

    /// <summary><c>--weapon-fire</c>: hold the aircraft's trigger from launch — the bank decides
    /// which one (guns or rockets).</summary>
    public bool AutoFireAtStart { get; init; }

    /// <summary><c>--weapon-cycle=N</c>: step the current bank's weapon list one entry every N
    /// physics frames — the scripted twin of holding down the panel's <c>&gt;</c> button, so the
    /// mount/ordnance-rebuild path can be walked headlessly under <c>--log=weapons</c>. 0 = off.</summary>
    public int CycleFrames { get; init; }

    private List<WeaponDef> BankWeapons => _bank == Guns ? _gunWeapons : _rocketWeapons;
    private List<Mount> BankMounts => _bank == Guns ? _gunMounts : _pylonMounts;
    private WeaponDef? SelectedWeapon => _weaponIndex < BankWeapons.Count ? BankWeapons[_weaponIndex] : null;
    private Mount? SelectedMount => _mountIndex < BankMounts.Count ? BankMounts[_mountIndex] : null;

    public override void _Ready()
    {
        BuildUi();
        ResolveInitialSelection();
        _panel = DebugShow;
        ApplyMount();
        ApplyWeapon();
        SetAutoFire(AutoFireAtStart);
        SyncState();
    }

    public override void _PhysicsProcess(double delta)
    {
        // The only thing this node does per frame — and only when asked to (--weapon-cycle).
        // Firing belongs to the aircraft's trigger; there is deliberately no second spawn path here.
        if (CycleFrames <= 0 || _host == null || ++_cycleTick < CycleFrames)
        {
            return;
        }
        _cycleTick = 0;
        StepWeapon(1);
    }

    /// <summary>Mounts and fires every one of the 48 weapons once — each from a mount of its own
    /// class (a gun from the gun groups, a hardpoint weapon from the pylons) — catching any that
    /// throw. Returns the report (the caller also writes it).</summary>
    public string RunSelfTest() => SelfTest().Report;

    /// <summary><see cref="RunSelfTest"/> with its counts kept, so an automated suite asserts on
    /// numbers instead of parsing the report back.</summary>
    public SelfTestResult SelfTest()
    {
        var sb = new StringBuilder();
        var gunMount = _gunMounts.Count > 0 ? _gunMounts[0] : null;
        var pylonMount = _pylonMounts.Count > 0 ? _pylonMounts[0] : null;
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
            if (mount == null || _pool == null)
            {
                skip++;
                sb.AppendLine($"  {w.Id,-7} {Trim(w.Name, 10),-10} {kind,-6} SKIP — "
                              + (mount == null ? "no mount on this plane" : "no projectile pool"));
                continue;
            }
            try
            {
                foreach (var n in mount.Nodes)
                {
                    _pool.Spawn(w, n.GlobalTransform, Vector3.Zero);
                }
                ok++;
                sb.AppendLine($"  {w.Id,-7} {Trim(w.Name, 10),-10} {kind,-6} → {mount.Label,-20} fired {mount.Nodes.Count} round(s) OK");
            }
            catch (Exception e)
            {
                err++;
                sb.AppendLine($"  {w.Id,-7} {Trim(w.Name, 10),-10} ERROR: {e.Message}");
            }
        }
        sb.AppendLine($"weapon-test: {ok}/{_all.Count} fired OK, {err} error(s), {skip} skipped");
        return new SelfTestResult
        {
            Report = sb.ToString(),
            Total = _all.Count,
            Ok = ok,
            Errors = err,
            Skipped = skip,
        };
    }

    // ---- input -------------------------------------------------------------------------------

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        // B, not W: this is a flight session, and W is pitch. The plane is held and reads no stick
        // input, but the panel key must not be one a pilot's hand rests on.
        if (@event is InputEventKey { Echo: false, Pressed: true, Keycode: Key.B })
        {
            _panel = !_panel;
            SyncState();
        }
    }

    // ---- weapon + mount resolution -----------------------------------------------------------

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

    private static string NodeNames(IReadOnlyList<Node3D> nodes)
    {
        var sb = new StringBuilder();
        foreach (var n in nodes)
        {
            if (sb.Length > 0)
            {
                sb.Append(',');
            }
            sb.Append(n.HasMeta(AnimRuntime.NameMeta) ? n.GetMeta(AnimRuntime.NameMeta).AsString() : n.Name);
        }
        return sb.ToString();
    }

    // ---- ui helpers ---------------------------------------------------------------------------

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

    /// <summary>Builds the two mount banks from the bound loadout: one entry per <b>firable</b> gun
    /// group (in <see cref="Loadout.FirableGuns"/> order, which is the order
    /// <see cref="Flight.FlightController.SelectGunGroup"/> indexes) and one per pylon. Falls back
    /// to the raw firepoint/pylon markers when no loadout is bound — a read-only list then, since
    /// there is nothing live to arm.</summary>
    private void BuildMounts(Loadout? loadout)
    {
        if (loadout != null)
        {
            foreach (var g in loadout.FirableGuns)
            {
                _gunMounts.Add(new Mount(g.Mount, $"g{g.Slot}", g.Muzzles) { Group = g });
            }
            foreach (var h in loadout.Hardpoints)
            {
                _pylonMounts.Add(new Mount($"pylon{h.Index}", $"pylon{h.Index}", new[] { h.Pylon }) { Hp = h });
            }
        }
        // No loadout (or a plane the table omits): fall back to the raw marker rig so the panel and
        // the self-test still have somewhere to point.
        if (_gunMounts.Count == 0 || _pylonMounts.Count == 0)
        {
            CollectRawMarkers(_plane, out var fps, out var pylons);
            if (_gunMounts.Count == 0)
            {
                foreach (var (ord, node) in fps)
                {
                    _gunMounts.Add(new Mount($"firepoint{ord}", $"firepoint{ord}", new[] { node }));
                }
            }
            if (_pylonMounts.Count == 0)
            {
                foreach (var (ord, node) in pylons)
                {
                    _pylonMounts.Add(new Mount($"pylon{ord}", $"pylon{ord}", new[] { node }));
                }
            }
        }
    }

    /// <summary>Snapshots the weapon every group and pylon carries before the panel touches
    /// anything — the fit "reset to stock" restores. Not read back out of
    /// <c>stock_loadouts.json</c>: <c>--rocket=</c>/<c>--loadout=</c> are part of how the session
    /// was launched, and the button restores the launch, not the file.</summary>
    private void RememberLaunchFit(Loadout? loadout)
    {
        if (loadout == null)
        {
            return;
        }
        foreach (var g in loadout.Guns)
        {
            _launchGuns[g] = g.Weapon;
        }
        if (loadout.Hardpoints.Count > 0)
        {
            _launchOrdnance = loadout.Hardpoints[0].Weapon;
        }
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
                Log.Warn("ui", $"weapon lab: --weapon-lab names unknown weapon '{InitialWeapon}'");
            }
        }
        else
        {
            SyncWeaponIndexToMount();
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

    // ---- driving the live loadout --------------------------------------------------------------

    /// <summary>Points the controller's own gun/pylon selector at the selected mount, so the
    /// aircraft's trigger fires from the mount the panel names.</summary>
    private void ApplyMount()
    {
        if (_host == null || SelectedMount is not { } mount)
        {
            return;
        }
        if (_bank == Guns)
        {
            _host.SelectGunGroup(_mountIndex);
        }
        else
        {
            _host.SelectPylon(_mountIndex);
        }
        Log.Debug("weapons", $"weapon lab: mount {mount.Label} selected ({NodeNames(mount.Nodes)})");
    }

    /// <summary>Arms the selection: a gun goes onto the selected group alone (with a full clip of
    /// its own <c>CLUSTER_SIZE</c>); a hardpoint weapon re-arms <b>every</b> pylon and rebuilds the
    /// mounted ordnance models, so the wings show the new type.</summary>
    private void ApplyWeapon()
    {
        if (_host == null || _loadout == null || SelectedWeapon is not { } w)
        {
            return;
        }
        if (_bank == Guns)
        {
            if (SelectedMount?.Group is not { } g)
            {
                return;
            }
            g.Weapon = w;
            g.Capacity = w.ClusterSize ?? 0;
            g.Ammo = g.Capacity;
            Log.Debug("weapons", $"weapon lab: gun group {g.Slot} ({g.Mount}) -> {w.Id} ({w.Name}), {g.Ammo} rounds, muzzles {NodeNames(g.Muzzles)}");
        }
        else
        {
            if (_loadout.Hardpoints.Count == 0)
            {
                return;
            }
            Testing.ProbeRunner.ApplyRocketOverride(_loadout, _weapons, w.Id, verbose: false);
            RebuildOrdnance(w);
        }
    }

    /// <summary>Re-hangs the mounted ordnance after a hardpoint swap. The old set comes off the
    /// pylons FIRST (<see cref="PylonOrdnance.Unmount"/> detaches immediately) — rebuilding without
    /// that leaves the previous body under every pylon, so a stepper held down leaks one model per
    /// pylon per swap. A weapon with no <c>FLYOUT</c> model in this chapter's gamez mounts nothing:
    /// <see cref="PylonOrdnance.Build"/> returns null and the wings simply go empty.</summary>
    private void RebuildOrdnance(WeaponDef w)
    {
        if (_host == null || _loadout == null)
        {
            return;
        }
        _host.Ordnance?.Unmount();
        _host.Ordnance = PylonOrdnance.Build(_loadout, _host.Projectiles);
        Log.Debug("weapons", $"weapon lab: hardpoints -> {w.Id} ({w.Name}) flyout='{w.Flyout?.Model ?? "-"}' per_pylon={_loadout.Hardpoints[0].Ammo} mounted={_host.Ordnance?.Count ?? 0} ordnance_nodes={CountOrdnanceNodes()}");
    }

    /// <summary>How many ordnance bodies are actually parented to the rig's pylons right now — the
    /// leak tripwire the swap path is verified with (it must equal the mounted count, whatever the
    /// panel has been stepped through).</summary>
    private int CountOrdnanceNodes()
    {
        int n = 0;
        foreach (var hp in _loadout?.Hardpoints ?? (IReadOnlyList<Hardpoint>)Array.Empty<Hardpoint>())
        {
            foreach (var child in hp.Pylon.GetChildren())
            {
                if (child.Name.ToString().StartsWith("ordnance", StringComparison.Ordinal))
                {
                    n++;
                }
            }
        }
        return n;
    }

    /// <summary>Holds the aircraft's own trigger — the gun one or the rocket one, by bank. The
    /// other is always released, so switching banks moves the held trigger with it.</summary>
    private void SetAutoFire(bool on)
    {
        _autoFire = on;
        if (_host != null)
        {
            _host.AutoFire = on && _bank == Guns;
            _host.AutoFireRockets = on && _bank == Hardpoints;
        }
    }

    /// <summary>Points the weapon stepper at what the selected mount actually carries, so the panel
    /// reads the live loadout rather than its own last click.</summary>
    private void SyncWeaponIndexToMount()
    {
        var carried = _bank == Guns ? SelectedMount?.Group?.Weapon : SelectedMount?.Hp?.Weapon;
        if (carried != null)
        {
            int i = IndexOfWeapon(BankWeapons, carried.Id);
            if (i >= 0)
            {
                _weaponIndex = i;
            }
        }
    }

    /// <summary>Puts the launch fit back on every group and pylon, full clips, and re-hangs the
    /// ordnance models.</summary>
    private void ResetToStock()
    {
        if (_host == null || _loadout == null)
        {
            return;
        }
        foreach (var g in _loadout.Guns)
        {
            if (_launchGuns.TryGetValue(g, out var w))
            {
                g.Weapon = w;
                g.Capacity = w.ClusterSize ?? 0;
                g.Ammo = g.Capacity;
            }
        }
        if (_launchOrdnance is { } ord && _loadout.Hardpoints.Count > 0)
        {
            Testing.ProbeRunner.ApplyRocketOverride(_loadout, _weapons, ord.Id, verbose: false);
            RebuildOrdnance(ord);
        }
        SyncWeaponIndexToMount();
        Log.Info("ui", $"weapon lab: loadout reset to the fit the session launched with");
        SyncState();
    }

    // ---- panel + state -----------------------------------------------------------------------

    /// <summary>Pushes the current state onto the widgets: panel visibility and every
    /// readout/toggle.</summary>
    private void SyncState()
    {
        _suppress = true;
        _ui.Visible = _panel;

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
        _ammoLabel.Text = AmmoLine();
        _autoFireToggle.ButtonPressed = _autoFire;
        _infiniteAmmoToggle.ButtonPressed = _host is { InfiniteAmmo: true };
        _cliLabel.Text = CliArgs();
        _suppress = false;
    }

    /// <summary>The selected mount's live counter. Infinite ammo is called out because it hides a
    /// real behaviour: the counters never drain, so a pylon never empties and its mounted model
    /// never disappears.</summary>
    private string AmmoLine()
    {
        if (SelectedMount is not { } m)
        {
            return "";
        }
        var (ammo, cap) = m.Group is { } g ? (g.Ammo, g.Capacity)
            : m.Hp is { } h ? (h.Ammo, h.Capacity)
            : (0, 0);
        return _host is { InfiniteAmmo: true }
            ? $"ammo: ∞ of {cap} — nothing drains, no model hides"
            : $"ammo: {ammo} / {cap}";
    }

    /// <summary>The arguments that reproduce this selection — the lab's output.</summary>
    private string CliArgs()
    {
        var sb = new StringBuilder();
        sb.Append(_host != null ? "--plane=" : "--viewer --plane=").Append(_planeModel);
        if (SelectedWeapon is { } w)
        {
            sb.Append(" --weapon-lab=").Append(w.Id);
        }
        else if (_host != null)
        {
            sb.Append(" --weapon-lab");
        }
        if (_mountIndex != 0 && SelectedMount is { } mount)
        {
            sb.Append(" --weapon-mount=").Append(mount.Cli);
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
        SyncWeaponIndexToMount();
        ApplyMount();
        SetAutoFire(_autoFire);   // the held trigger follows the bank
        SyncState();
    }

    private void StepWeapon(int delta)
    {
        if (BankWeapons.Count == 0)
        {
            return;
        }
        _weaponIndex = Mathf.PosMod(_weaponIndex + delta, BankWeapons.Count);
        ApplyWeapon();
        SyncState();
    }

    private void StepMount(int delta)
    {
        if (BankMounts.Count == 0)
        {
            return;
        }
        _mountIndex = Mathf.PosMod(_mountIndex + delta, BankMounts.Count);
        SyncWeaponIndexToMount();   // the panel follows what THIS mount carries; it does not re-arm it
        ApplyMount();
        SyncState();
    }

    private void BuildUi()
    {
        _ui = new CanvasLayer { Layer = 1, Visible = false };
        var root = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);

        // Top-right: in flight the bottom-right corner belongs to the gauge cluster (its right-hand
        // dials sit 420 px from the right edge in reference space), and the top-left is the flight
        // HUD's. Top-right is free unless F5 opens the damage lab.
        var panel = new PanelContainer { SelfModulate = new Color(1, 1, 1, 0.85f) };
        panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        panel.GrowHorizontal = Control.GrowDirection.Begin;
        panel.GrowVertical = Control.GrowDirection.End;
        panel.Position = new Vector2(-8, 8);

        var margin = new MarginContainer();
        foreach (var side in new[] { "margin_left", "margin_right", "margin_top", "margin_bottom" })
        {
            margin.AddThemeConstantOverride(side, 10);
        }
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 3);

        box.AddChild(new Label { Text = "WEAPON LAB" });
        box.AddChild(Small("B hides · Space fires the guns · F fires a rocket"));

        // bank stepper (guns arm gun groups, hardpoints arm pylons)
        _bankLabel = new Label { CustomMinimumSize = new Vector2(300, 0), VerticalAlignment = VerticalAlignment.Center };
        var bankRow = new HBoxContainer();
        bankRow.AddChild(StepButton("<", () => StepBank(-1)));
        bankRow.AddChild(_bankLabel);
        bankRow.AddChild(StepButton(">", () => StepBank(1)));
        box.AddChild(bankRow);

        // weapon stepper — each step ARMS the selection
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

        // mount stepper — the group/pylon the trigger fires from
        box.AddChild(Small("mount — the group / pylon the trigger uses"));
        _mountLabel = new Label { CustomMinimumSize = new Vector2(280, 0), VerticalAlignment = VerticalAlignment.Center };
        var mountRow = new HBoxContainer();
        mountRow.AddChild(StepButton("<", () => StepMount(-1)));
        mountRow.AddChild(_mountLabel);
        mountRow.AddChild(StepButton(">", () => StepMount(1)));
        box.AddChild(mountRow);
        _ammoLabel = Small("");
        _ammoLabel.CustomMinimumSize = new Vector2(330, 0);
        _ammoLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        box.AddChild(_ammoLabel);

        box.AddChild(Separator());

        // The lab launches with infinite ammo so a soak run never dries up — which also hides the
        // depletion behaviour entirely (a pylon that never empties never drops its mounted model),
        // so the toggle to turn it off is on the panel rather than only on the command line.
        _infiniteAmmoToggle = new CheckButton { Text = "infinite ammo" };
        _infiniteAmmoToggle.Toggled += on =>
        {
            if (!_suppress && _host != null)
            {
                _host.InfiniteAmmo = on;
                SyncState();
            }
        };
        box.AddChild(_infiniteAmmoToggle);

        _autoFireToggle = new CheckButton { Text = "hold the trigger (auto-fire)" };
        _autoFireToggle.Toggled += on =>
        {
            if (!_suppress)
            {
                SetAutoFire(on);
                SyncState();
            }
        };
        box.AddChild(_autoFireToggle);

        var reset = new Button { Text = "reset to stock" };
        reset.Pressed += ResetToStock;
        box.AddChild(reset);

        box.AddChild(Separator());

        _cliLabel = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(330, 0) };
        _cliLabel.AddThemeFontSizeOverride("font_size", 11);
        box.AddChild(_cliLabel);

        var copy = new Button { Text = "copy CLI args" };
        copy.Pressed += () =>
        {
            var args = CliArgs();
            DisplayServer.ClipboardSet(args);
            Log.Info("ui", $"weapon lab: {args}");
        };
        box.AddChild(copy);

        margin.AddChild(box);
        panel.AddChild(margin);
        root.AddChild(panel);
        _ui.AddChild(root);
        AddChild(_ui);
    }

    /// <summary>The self-test's verdict: the report text plus the counts a suite asserts on.
    /// <see cref="Skipped"/> is called out because it is a success-looking outcome — a weapon with
    /// no mount on this plane never fires and nothing else would notice.</summary>
    public sealed class SelfTestResult
    {
        public required string Report { get; init; }
        public required int Total { get; init; }
        public required int Ok { get; init; }
        public required int Errors { get; init; }
        public required int Skipped { get; init; }
    }

    /// <summary>One selectable place on the airframe: a firable gun group or a pylon.
    /// <see cref="Nodes"/> are its live muzzle / pylon <see cref="Node3D"/>s and <see cref="Cli"/>
    /// the <c>--weapon-mount=</c> token (<c>g1</c>, <c>pylon1</c>). <see cref="Group"/> /
    /// <see cref="Hp"/> are the live loadout entries the panel writes into — both null for the
    /// raw-marker fallback, which has nothing to arm.</summary>
    private sealed class Mount
    {
        public Mount(string label, string cli, IReadOnlyList<Node3D> nodes)
        {
            Label = label;
            Cli = cli;
            Nodes = nodes;
        }

        public string Label { get; }
        public string Cli { get; }
        public IReadOnlyList<Node3D> Nodes { get; }
        public GunGroup? Group { get; init; }
        public Hardpoint? Hp { get; init; }
    }
}
