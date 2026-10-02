using System;
using System.Collections.Generic;
using CSVM.Utils;

namespace CSVM.Testing;

/// <summary>
/// A suite's stand-in for Steam's on-screen keyboard: <see cref="ScreenKeyboard"/> reads as
/// available and every URL it would have opened is recorded instead. Disposing it lowers whatever
/// is still up and puts the detected state back, so the next suite runs on this machine's own.
/// </summary>
internal sealed class ScreenKeyboardRecorder : IDisposable
{
    private readonly bool _available = ScreenKeyboard.Available;
    private readonly Action<string> _shell = ScreenKeyboard.Shell;

    public ScreenKeyboardRecorder()
    {
        ScreenKeyboard.Shell = Urls.Add;
        ScreenKeyboard.Available = true;
    }

    /// <summary>Gets the URLs handed to the system, oldest first.</summary>
    public List<string> Urls { get; } = new();

    /// <summary>Gets the recorded URLs as one line, for a check's message.</summary>
    public string Said => Urls.Count == 0 ? "none" : string.Join(", ", Urls);

    public void Dispose()
    {
        if (ScreenKeyboard.Shown is { } shown)
        {
            ScreenKeyboard.Hide(shown.Owner);
        }

        ScreenKeyboard.Shell = _shell;
        ScreenKeyboard.Available = _available;
    }
}
