using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CSVM.Tests;

/// <summary>
/// <c>AnimRuntime.HandledEventKinds</c> against the case labels of its <c>Dispatch</c>, read from
/// the source because the runtime is a Godot node a unit test cannot construct. A kind missing
/// from the set is reported as unhandled by the node lab while the runtime acts on it.
/// </summary>
public class AnimRuntimeEventKindsTests
{
    private const string AllNames = "AnimDefinition.AllNamesKind";

    private static readonly string Source = File.ReadAllText(
        Path.Combine(TestData.RepoRoot, "CSVM", "src", "Mech3", "AnimRuntime.cs"));

    [Fact]
    public void TheHandledSetNamesExactlyTheKindsDispatchHasACaseFor()
    {
        var handled = SetLiterals("HandledEventKinds");
        var cases = DispatchCases();

        // The able-to-fail control: a parse that found nothing would compare two empty sets.
        Assert.True(cases.Count > 20, $"only {cases.Count} Dispatch cases parsed");
        Assert.Equal(cases.OrderBy(k => k), handled.OrderBy(k => k));
    }

    [Fact]
    public void EveryPartiallyHandledKindIsAlsoHandled()
    {
        var partial = SetLiterals("PartialEventKinds");
        Assert.NotEmpty(partial);
        Assert.Empty(partial.Except(SetLiterals("HandledEventKinds")));
    }

    private static HashSet<string> SetLiterals(string field)
    {
        var block = Regex.Match(Source, field + @" = new HashSet<string>\(StringComparer\.Ordinal\)\s*\{(?<body>.*?)\};",
            RegexOptions.Singleline);
        Assert.True(block.Success, $"{field}'s initializer not found");
        var body = block.Groups["body"].Value;
        var kinds = Regex.Matches(body, "\"(\\w+)\"").Select(m => m.Groups[1].Value).ToHashSet();
        if (body.Contains(AllNames))
            kinds.Add(AllNames);
        return kinds;
    }

    private static HashSet<string> DispatchCases()
    {
        // The method runs from its signature to the first closing brace at member indentation.
        var method = Regex.Match(Source, @"private bool Dispatch\(AnimEvent ev.*?\n    \}\r?\n",
            RegexOptions.Singleline);
        Assert.True(method.Success, "Dispatch not found");
        return Regex.Matches(method.Value, @"^            case (?:""(\w+)""|(AnimDefinition\.AllNamesKind)):",
                RegexOptions.Multiline)
            .Select(m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value)
            .ToHashSet();
    }
}
