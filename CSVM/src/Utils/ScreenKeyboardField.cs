using System;

namespace CSVM.Utils;

/// <summary>One text field the keyboard can be raised for. Its <paramref name="Text"/> reads what
/// the field holds now, masked where the field masks it. Its <paramref name="Echoed"/> says whether
/// the echo strip repeats it above the keyboard.</summary>
public sealed record ScreenKeyboardField(string Owner, string Id, string Label, Func<string> Text, bool Echoed = true);
