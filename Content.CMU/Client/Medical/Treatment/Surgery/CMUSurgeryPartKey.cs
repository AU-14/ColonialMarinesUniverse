using System.Collections.Generic;
using Content.Shared._CMU14.Medical.Treatment.Surgery;
using Content.Shared.Body.Part;

namespace Content.Client._CMU14.Medical.Treatment.Surgery;

internal readonly record struct CMUSurgeryPartKey(NetEntity Part, BodyPartType Type, BodyPartSymmetry Symmetry)
{
    public CMUSurgeryPartKey(CMUSurgeryPartEntry part) : this(part.Part, part.Type, part.Symmetry)
    {
    }

    public bool Matches(CMUSurgeryPartEntry part)
    {
        return part.Part == Part
            && part.Type == Type
            && part.Symmetry == Symmetry;
    }

    public static bool Contains(IReadOnlyList<CMUSurgeryPartEntry> parts, CMUSurgeryPartKey key)
    {
        foreach (var part in parts)
        {
            if (key.Matches(part))
                return true;
        }

        return false;
    }

    public static bool TryFind(IReadOnlyList<CMUSurgeryPartEntry> parts, CMUSurgeryPartKey key, out CMUSurgeryPartEntry part)
    {
        foreach (var candidate in parts)
        {
            if (!key.Matches(candidate))
                continue;

            part = candidate;
            return true;
        }

        part = default!;
        return false;
    }
}
