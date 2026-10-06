using System.Linq;
using Content.Shared.CMU14.Origin;
using Content.Shared.Preferences;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Preferences;

public readonly record struct CMUCharacterNameParts(string First, string Last, string Nickname, bool ArtificialWomb);

public static class CMUCharacterName
{
    public const string ArtificialWombTag = "A.W.";
    public const int MaxNicknameWords = 2;
    public static readonly ProtoId<OriginPrototype> ArtificialWombOrigin = "UAArtificialWomb";

    public static bool IsArtificialWomb(HumanoidCharacterProfile profile)
    {
        return profile.Origin == ArtificialWombOrigin;
    }

    public static CMUCharacterNameParts Parse(string name)
    {
        var tokens = new List<string>(name.Split(' ', StringSplitOptions.RemoveEmptyEntries));

        var artificialWomb = tokens.RemoveAll(IsArtificialWombToken) > 0;

        var nickname = string.Empty;
        var start = tokens.FindIndex(t => t.StartsWith('\''));
        if (start >= 0)
        {
            var end = -1;
            for (var i = start; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (!token.EndsWith('\'') || i == start && token.Length < 2)
                    continue;

                end = i;
                break;
            }

            if (end >= 0)
            {
                nickname = string.Join(' ', tokens.GetRange(start, end - start + 1)).Trim('\'').Trim();
                tokens.RemoveRange(start, end - start + 1);
            }
        }

        var first = tokens.Count > 0 ? tokens[0] : string.Empty;
        var last = tokens.Count > 1 ? string.Join(' ', tokens.GetRange(1, tokens.Count - 1)) : string.Empty;
        return new CMUCharacterNameParts(first, last, nickname, artificialWomb);
    }

    public static string Compose(string first, string last, string nickname, bool artificialWomb)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(first))
            parts.Add(first.Trim());

        if (artificialWomb)
            parts.Add(ArtificialWombTag);

        var cleanNickname = LimitNickname(nickname);
        if (cleanNickname.Length > 0)
            parts.Add($"'{cleanNickname}'");

        if (!string.IsNullOrWhiteSpace(last))
            parts.Add(last.Trim());

        return string.Join(' ', parts);
    }

    public static string LimitNickname(string nickname)
    {
        var words = nickname.Replace("'", string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Take(MaxNicknameWords));
    }

    public static string Normalize(string name, bool artificialWomb, bool synthetic)
    {
        if (!synthetic)
            return ApplyArtificialWomb(name, artificialWomb);

        var first = Parse(name).First;
        return first.Length > 0 ? first : name;
    }

    public static string ApplyArtificialWomb(string name, bool artificialWomb)
    {
        var parts = Parse(name);
        if (parts.ArtificialWomb == artificialWomb && LimitNickname(parts.Nickname) == parts.Nickname)
            return name;

        return Compose(parts.First, parts.Last, parts.Nickname, artificialWomb);
    }

    public static bool HasRequiredParts(string name, bool synthetic)
    {
        var parts = Parse(name);
        if (synthetic)
            return parts.First.Length > 0 && parts.Last.Length == 0 && parts.Nickname.Length == 0 && !parts.ArtificialWomb;

        return parts.First.Length > 0 && parts.Last.Length > 0;
    }

    private static bool IsArtificialWombToken(string token)
    {
        return token.Equals(ArtificialWombTag, StringComparison.OrdinalIgnoreCase) ||
               token.Equals("A.W", StringComparison.OrdinalIgnoreCase);
    }
}
