using System;

namespace CSVM.Flight.Airframe;

/// <summary>Pure display-name lookups for an aircraft and a scenario. Which plane a human flies is
/// <c>Session.Roster.HumanFieldPlanes</c>'s, since that reads the launch spec.</summary>
public static class PlaneRoster
{
    /// <summary>A readable plane name: the def's own authored <c>title</c> where it has been
    /// resolved ("Medusa Kestrel"), else derived from the vehicle.json def name, the player defs
    /// are "p&lt;name&gt;" (pbloodhawk, ppeacemaker, pfury, …), so strip the leading p and
    /// title-case → "Bloodhawk". Falls back to the node name.</summary>
    public static string PlaneDisplayName(PlaneStats stats)
    {
        if (stats.AiTitle is { Length: > 0 } title)
        {
            return title;
        }

        var d = stats.DefName;
        string name = d.Length > 1 && (d[0] == 'p' || d[0] == 'P') ? d[1..]
            : d.Length > 0 ? d
            : stats.NodeName;
        return Humanize(name);
    }

    /// <summary>"stunt_flying" → "Stunt Flying": underscores to spaces, each word title-cased.</summary>
    public static string Humanize(string s)
    {
        var words = s.Split(new[] { '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
            words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..].ToLowerInvariant();
        return string.Join(' ', words);
    }
}
