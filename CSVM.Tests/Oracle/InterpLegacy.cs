using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CSVM.Mech3;

namespace CSVM.Tests.Oracle;

/// <summary>The engine's former read of the boot scripts, from the extraction's
/// <c>interp.json</c>, kept as the oracle <see cref="InterpScripts.Read"/> is compared against.
/// Test-only.</summary>
public static class InterpLegacy
{
    /// <summary>The scripts of an extraction's <c>interp.json</c>: each object's <c>name</c> and
    /// <c>lines</c>, in file order.</summary>
    public static InterpScripts Read(string path)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
        var scripts = new List<InterpScript>();
        foreach (var script in doc.RootElement.EnumerateArray())
        {
            var lines = new List<string>();
            foreach (var line in script.GetProperty("lines").EnumerateArray())
            {
                lines.Add(line.GetString() ?? "");
            }

            scripts.Add(new InterpScript(script.GetProperty("name").GetString() ?? "", lines));
        }

        return new InterpScripts(scripts);
    }
}
