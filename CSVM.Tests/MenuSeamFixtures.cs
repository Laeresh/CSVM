using System;
using System.Collections.Generic;
using CSVM;
using CSVM.UI.Menu;

namespace CSVM.Tests;

/// <summary>A minimal shared feature: pick a chapter and a plane, then build the typed exit.
/// Deliberately schema-free, so the two fake presentations must drive its semantic operations
/// rather than render a common widget description.</summary>
internal sealed class FakeSortieFeature : IMenuFeature
{
    public string? Chapter { get; private set; }

    public string? Plane { get; private set; }

    public int Discards { get; private set; }

    public bool CanLaunch => Chapter != null && Plane != null;

    public void SelectChapter(string code) => Chapter = code;

    public void SelectPlane(string node) => Plane = node;

    public LaunchExit BuildExit() =>
        new(Chapter!, new[] { new MenuSeatChoice(Plane!, Array.Empty<int>()) }, MenuMode.Free);

    public void Discard()
    {
        Chapter = null;
        Plane = null;
        Discards++;
    }
}

/// <summary>Records every cue, narration and mix-preview request, so a test can see what a
/// presentation asked the shared service for.</summary>
internal sealed class RecordingAudio : IMenuAudio
{
    public List<string> Cues { get; } = new();

    public List<(CSVM.Utils.AudioLevels Levels, MenuMixLevel Moved)> Mixes { get; } = new();

    public int MixEnds { get; private set; }

    public string? Narration { get; private set; }

    public void Cue(MenuCue cue) => Cues.Add(cue.Name);

    public void BeginNarration(string wavName) => Narration = wavName;

    public void EndNarration() => Narration = null;

    public void PreviewMix(CSVM.Utils.AudioLevels levels, MenuMixLevel moved) => Mixes.Add((levels, moved));

    public void EndMixPreview() => MixEnds++;
}

/// <summary>One seat fed from a queue of scripted frames; an empty queue reads idle.</summary>
internal sealed class ScriptedMenuSeat : IMenuInputSource
{
    private readonly Queue<MenuCommands> _frames = new();

    public string DeviceLabel => "scripted";

    public bool CapturingText { get; set; }

    public int Primes { get; private set; }

    public void Enqueue(MenuCommands frame) => _frames.Enqueue(frame);

    public MenuCommands Poll(float dt) => _frames.Count > 0 ? _frames.Dequeue() : MenuCommands.None;

    public void Prime() => Primes++;
}

/// <summary>The host stand-in: one feature set, recording audio, scripted seats, and a list of
/// captured exits standing in for the Launcher handoff.</summary>
internal sealed class FakeMenuHost : IMenuHost
{
    public MenuFeatureSet Features { get; } = new();

    public RecordingAudio Recording { get; } = new();

    public IMenuAudio Audio => Recording;

    public List<IMenuInputSource> SeatList { get; } = new();

    public IReadOnlyList<IMenuInputSource> Seats => SeatList;

    public List<MenuExit> Exits { get; } = new();

    public void Exit(MenuExit exit) => Exits.Add(exit);
}

/// <summary>A three-screen cursor-driven graph (chapter, plane, confirm) over the shared
/// feature: the launchscreen's wizard shape in miniature.</summary>
internal sealed class FakeWizardPresentation : IMenuPresentation
{
    private static readonly string[] Chapters = { "C1", "C2" };
    private static readonly string[] Planes = { "player_bhawk", "player_fury" };

    private IMenuHost? _host;
    private FakeSortieFeature? _feature;
    private int _cursor;

    public PresentationId Id => new("fake-wizard");

    public string Screen { get; private set; } = string.Empty;

    public int Hides { get; private set; }

    public void Activate(IMenuHost host, MenuReturnDestination destination)
    {
        _host = host;
        _feature = host.Features.Get<FakeSortieFeature>();
        Screen = destination is DebriefReturn ? "wizard-debrief" : "wizard-chapter";
        _cursor = 0;
    }

    public void Hide() => Hides++;

    public void Tick(float dt)
    {
        var c = _host!.Seats[0].Poll(dt);
        if (c.MoveY != 0)
        {
            _cursor = Math.Clamp(_cursor + c.MoveY, 0, 1);
            _host.Audio.Cue(new MenuCue("wizard-step"));
        }

        if (c.Back)
        {
            if (Screen == "wizard-chapter")
            {
                _host.Exit(new QuitExit());
            }
            else
            {
                Screen = "wizard-chapter";
            }

            return;
        }

        if (!c.Accept)
        {
            return;
        }

        switch (Screen)
        {
            case "wizard-chapter":
                _feature!.SelectChapter(Chapters[_cursor]);
                Screen = "wizard-plane";
                _cursor = 0;
                break;
            case "wizard-plane":
                _feature!.SelectPlane(Planes[_cursor]);
                Screen = "wizard-confirm";
                break;
            case "wizard-confirm":
                if (_feature!.CanLaunch)
                {
                    _host.Exit(_feature.BuildExit());
                }

                break;
        }
    }

    public void Deactivate()
    {
        _host = null;
        _feature = null;
        Screen = string.Empty;
    }
}

/// <summary>A one-screen pointer-driven graph over the same feature: chapter chips in the top
/// band, plane chips in the middle band, the fly action below. A different screen graph from the
/// wizard's on purpose.</summary>
internal sealed class FakePointerPresentation : IMenuPresentation
{
    private IMenuHost? _host;
    private FakeSortieFeature? _feature;

    public PresentationId Id => new("fake-pointer");

    public string Screen { get; private set; } = string.Empty;

    public void Activate(IMenuHost host, MenuReturnDestination destination)
    {
        _host = host;
        _feature = host.Features.Get<FakeSortieFeature>();
        Screen = destination is DebriefReturn ? "page-results" : "page";
    }

    public void Hide()
    {
    }

    public void Tick(float dt)
    {
        var c = _host!.Seats[0].Poll(dt);
        if (c.Pointer is not { Clicked: true } p)
        {
            return;
        }

        if (p.Y < 200f)
        {
            _feature!.SelectChapter(p.X < 400f ? "C1" : "C2");
            _host.Audio.Cue(new MenuCue("page-pick"));
        }
        else if (p.Y < 400f)
        {
            _feature!.SelectPlane(p.X < 400f ? "player_bhawk" : "player_fury");
        }
        else if (_feature!.CanLaunch)
        {
            _host.Exit(_feature.BuildExit());
        }
    }

    public void Deactivate()
    {
        _host = null;
        _feature = null;
        Screen = string.Empty;
    }
}
