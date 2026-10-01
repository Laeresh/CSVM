using System;
using System.Collections.Generic;

namespace CSVM.Flight.Hud;

/// <summary>
/// One machine's in-flight chat in a network match, as the original's chat panel holds it. It keeps
/// the last five lines, shown for ten seconds after the newest, and the one entry a pilot types
/// into. It is engine-free, so the suites and the units drive it with no pane.
/// <see cref="ChatPanel"/> draws it in every pane, and <c>Session.World.NetChatLink</c> fills it. The decode, with its addresses, is <c>docs/org/multiplayer-messages.md</c> "In-flight chat".
/// </summary>
public sealed class FlightChat
{
    /// <summary>How many lines the panel keeps, the count its constructor is handed
    /// (<c>FUN_005c5950(5)</c> at <c>0x004a83d6</c>). A sixth pushes the oldest out.</summary>
    public const int Depth = 5;

    /// <summary>How long the panel shows after a line lands or the entry opens, in seconds. Both
    /// hand its timer <c>10.0f</c> (<c>0x004a836e</c>, <c>0x004a858d</c>).</summary>
    public const float ShowSeconds = 10f;

    /// <summary>The most characters a pilot may type. The entry's buffer is 60 bytes
    /// (<c>0x0046d797</c>) and opens holding <see cref="EntryMark"/> and a terminator.</summary>
    public const int MaxTyped = 57;

    /// <summary>What the entry opens holding, and what its sender cuts the typed line after
    /// (<c>0x0063f93c</c>).</summary>
    public const string EntryMark = "> ";

    /// <summary>The most characters of a received line the panel keeps (<c>0x00499b5f</c>).
    /// </summary>
    public const int MaxReceived = 80;

    private readonly List<string> _lines = new();
    private float _shownFor;

    /// <summary>The lines held, oldest first. They stay when the panel hides, and a new line shows
    /// them again with it.</summary>
    public IReadOnlyList<string> Lines => _lines;

    /// <summary>Whether a pilot on this machine is typing a line.</summary>
    public bool Typing { get; private set; }

    /// <summary>Whether the open entry goes to the typist's team rather than to everybody.</summary>
    public bool ToTeam { get; private set; }

    /// <summary>What the typist has typed so far, the entry mark left out.</summary>
    public string Draft { get; private set; } = string.Empty;

    /// <summary>Whether the panel is up: inside the ten seconds, or while a line is being typed.
    /// The original's timer runs on under the entry. Here the typist keeps sight of the panel.</summary>
    public bool Shown => Typing || _shownFor > 0f;

    /// <summary>Lines posted since construction, for a suite that counts what arrived.</summary>
    public int Posted { get; private set; }

    /// <summary>The caption the open entry stands under, "To All:" or "To Team:".</summary>
    public string Prompt { get; private set; } = string.Empty;

    /// <summary>The entry as drawn: the caption, the entry's own mark and the draft.</summary>
    public string EntryLine => $"{Prompt} {EntryMark}{Draft}";

    /// <summary>The line a pilot's own panel shows for what it sent. The original joins the prompt
    /// and <c>" %s"</c> of the text by <c>"%s %s"</c> (<c>0x0046d8db</c>, <c>0x0046d911</c>).</summary>
    public static string Echo(string prompt, string text) => $"{prompt}  {text}";

    /// <summary>A line as every other panel shows it, <c>"%s: %s"</c> (<c>0x00499bf4</c>), the
    /// text cut to <see cref="MaxReceived"/>.</summary>
    public static string Received(string name, string text)
    {
        string kept = text ?? string.Empty;
        return $"{name}: {(kept.Length > MaxReceived ? kept[..MaxReceived] : kept)}";
    }

    /// <summary>Opens an empty entry under <paramref name="prompt"/>, to the team when
    /// <paramref name="toTeam"/>, and shows the panel. A second open while typing keeps the draft
    /// and only changes where it goes.</summary>
    public void Open(bool toTeam, string prompt)
    {
        if (!Typing)
        {
            Draft = string.Empty;
        }

        Typing = true;
        ToTeam = toTeam;
        Prompt = prompt ?? string.Empty;
        _shownFor = ShowSeconds;
    }

    /// <summary>Adds one typed character. False, and nothing added, when no entry is open, the
    /// character is not printable ASCII, or the entry is full.</summary>
    public bool Type(char c)
    {
        if (!Typing || c < ' ' || c > '~' || Draft.Length >= MaxTyped)
        {
            return false;
        }

        Draft += c;
        return true;
    }

    /// <summary>Takes back the last typed character.</summary>
    public void Erase()
    {
        if (Typing && Draft.Length > 0)
        {
            Draft = Draft[..^1];
        }
    }

    /// <summary>Closes the entry and hands back the draft trimmed at both ends. A blank one is null,
    /// and the original closes on it without sending (<c>0x0046d898</c>).</summary>
    public string? Submit()
    {
        if (!Typing)
        {
            return null;
        }

        string line = Draft.Trim();
        Cancel();
        return line.Length > 0 ? line : null;
    }

    /// <summary>Closes the entry and drops the draft.</summary>
    public void Cancel()
    {
        Typing = false;
        Draft = string.Empty;
    }

    /// <summary>Adds one line under the others, dropping the oldest past <see cref="Depth"/>, and
    /// shows the panel for another <see cref="ShowSeconds"/>.</summary>
    public void Post(string line)
    {
        if (_lines.Count >= Depth)
        {
            _lines.RemoveAt(0);
        }

        _lines.Add(line ?? string.Empty);
        _shownFor = ShowSeconds;
        Posted++;
    }

    /// <summary>Runs the panel's timer down by <paramref name="dt"/> seconds.</summary>
    public void Advance(float dt) => _shownFor = Math.Max(0f, _shownFor - Math.Max(0f, dt));
}
