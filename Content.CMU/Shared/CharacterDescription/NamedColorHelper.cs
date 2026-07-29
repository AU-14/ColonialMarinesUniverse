using System.Linq;
using System.Text;
using Robust.Shared.Maths;

namespace Content.Shared._CMU14.CharacterDescription;

public static class NamedColorHelper
{
    private static readonly (string Name, Color Color)[] NamedColors = BuildPalette();

    private static (string Name, Color Color)[] BuildPalette()
    {
        return Color.GetAllDefaultColors()
            .Where(pair => pair.Key != "transparent")
            .Select(pair => (Name: Capitalize(pair.Key), pair.Value))
            .ToArray();
    }

    private static string Capitalize(string name)
    {
        if (name.Length == 0)
            return name;

        var builder = new StringBuilder(name.Length);
        builder.Append(char.ToUpperInvariant(name[0]));
        builder.Append(name, 1, name.Length - 1);
        return builder.ToString();
    }

    public static string NearestColorName(Color color)
    {
        var bestName = "Unknown";
        var bestDistance = float.MaxValue;

        foreach (var (name, named) in NamedColors)
        {
            var dr = color.R - named.R;
            var dg = color.G - named.G;
            var db = color.B - named.B;
            var distance = dr * dr + dg * dg + db * db;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestName = name;
            }
        }

        return bestName;
    }
}
