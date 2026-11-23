using System;
using System.Collections.Generic;
using System.Linq;
using CSVM.Flight.Hangar;
using CSVM.Flight.Weapons;
using CSVM.Session.Career;

namespace CSVM.UI.Menu;

/// <summary>
/// The career's menu state and operations, engine-free and presentation-neutral: the saved
/// careers, the one open, and every action a career screen offers. Each action that changes the
/// career saves it, so a quit never loses a day. The career's own rules are
/// <see cref="CareerState"/>'s; this feature only opens, starts, saves and deletes careers and hands
/// the open one's operations to a screen.
/// </summary>
public sealed class CareerFeature : IMenuFeature
{
    /// <summary>The longest gang name, which is also the save file's name.</summary>
    public const int NameLimit = 24;

    private readonly Func<CareerStore> _store;
    private readonly Func<StockLoadouts?> _stock;
    private readonly Func<ulong> _newSeed;

    /// <summary>A feature over the career store <paramref name="store"/>. Starting planes take their
    /// stock guns off <paramref name="stock"/> when it answers; <paramref name="newSeed"/> draws a new
    /// career's seed, the clock's by default.</summary>
    public CareerFeature(Func<CareerStore> store, Func<StockLoadouts?>? stock = null, Func<ulong>? newSeed = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _stock = stock ?? (() => null);
        _newSeed = newSeed ?? (() => (ulong)DateTime.UtcNow.Ticks);
    }

    /// <summary>The career open, or null.</summary>
    public CareerState? Current { get; private set; }

    /// <summary>The saved careers' names, sorted.</summary>
    public IReadOnlyList<string> Careers => _store().List();

    /// <summary>Why <paramref name="name"/> cannot start a career, or null when it can.</summary>
    public string? NameRefusal(string name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return "Name the gang first";
        }

        if (trimmed.Length > NameLimit)
        {
            return "That name is too long";
        }

        string key = CustomPlaneStore.FileKey(trimmed);
        return Careers.Any(c => string.Equals(CustomPlaneStore.FileKey(c), key, StringComparison.OrdinalIgnoreCase))
            ? "A gang of that name already flies"
            : null;
    }

    /// <summary>Starts and saves a new career named <paramref name="name"/> and opens it. Refused,
    /// with nothing written, when <see cref="NameRefusal"/> answers.</summary>
    public bool Start(string name)
    {
        if (NameRefusal(name) != null)
        {
            return false;
        }

        var tunes = CareerTunes.Default;
        var starters = tunes.StartingAirframes.Select((airframe, i) => Starter(airframe, i));
        Current = CareerState.New(name.Trim(), _newSeed(), starters, tunes);
        Save();
        return true;
    }

    /// <summary>Opens the saved career <paramref name="name"/>; false when it does not read.</summary>
    public bool Open(string name)
    {
        Current = _store().Load(name);
        return Current != null;
    }

    /// <summary>Deletes the saved career <paramref name="name"/>, closing it if it is open.</summary>
    public void Delete(string name)
    {
        if (Current != null && string.Equals(CustomPlaneStore.FileKey(Current.Name), CustomPlaneStore.FileKey(name), StringComparison.OrdinalIgnoreCase))
        {
            Current = null;
        }

        _store().Delete(name);
    }

    /// <summary>Closes the open career, which is already saved.</summary>
    public void Close() => Current = null;

    /// <summary>Lets one day pass, for a gang with nothing better to do.</summary>
    public void Wait() => Change(c => c.AdvanceDays(1));

    /// <summary>Takes offer <paramref name="id"/> as the active contract.</summary>
    public bool Accept(int id) => Change(c => c.Accept(id) != null);

    /// <summary>Drops the active contract, which pays nothing.</summary>
    public void Abandon() => Change(c => c.AbandonContract());

    /// <summary>Repairs plane <paramref name="index"/> fully, as <see cref="CareerState.Repair"/> rules.</summary>
    public bool Repair(int index) => Change(c => index >= 0 && index < c.Planes.Count && c.Repair(index));

    /// <summary>Buys <paramref name="lots"/> lots of <paramref name="line"/> into the hold.</summary>
    public bool BuyRounds(AmmoLine line, int lots) =>
        Change(c => c.Shelf.BuyRounds(line, lots, c.Day, c.Hold, c.Ledger, c.Tunes));

    /// <summary>Buys <paramref name="count"/> rockets of <paramref name="weaponId"/> into the hold.</summary>
    public bool BuyRockets(string weaponId, int count) =>
        Change(c => c.Shelf.BuyRockets(weaponId, count, c.Day, c.Hold, c.Ledger, c.Tunes));

    /// <summary>Writes the open career to its save.</summary>
    public void Save()
    {
        if (Current != null)
        {
            _store().Save(Current);
        }
    }

    /// <inheritdoc/>
    public void Discard()
    {
    }

    private bool Change(Func<CareerState, bool> action)
    {
        if (Current == null || !action(Current))
        {
            return false;
        }

        Save();
        return true;
    }

    private void Change(Action<CareerState> action)
    {
        if (Current != null)
        {
            action(Current);
            Save();
        }
    }

    // A starting plane: the airframe's stock armour spread evenly over its zones and the middle
    // engine. Its guns and name come from the stock loadouts where they answer.
    private CustomPlaneDef Starter(int airframe, int index)
    {
        int presses = HangarEconomy.StockArmourUnits[airframe] / HangarEconomy.ArmourUnitsPerStep / 4;
        var fit = _stock()?.ForModel(StockAirframes.Node(airframe));
        string type = fit is { Display.Length: > 0 } ? fit.Display : "Plane";
        var def = new CustomPlaneDef
        {
            Name = FormattableString.Invariant($"{type} {index + 1}"),
            Airframe = airframe,
            Engine = 1,
            ArmourNose = presses,
            ArmourTail = presses,
            ArmourLeftWing = presses,
            ArmourRightWing = presses,
        };
        HangarFeature.LoadStockWeapons(def, fit);
        return def;
    }
}
