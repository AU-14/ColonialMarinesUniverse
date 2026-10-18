using Content.Shared.Body.Part;

namespace Content.Shared.CMU14.Medical.Core;

/// <summary>
/// Player-facing body part names for injury messages: "left arm", "right foot", "chest".
/// </summary>
public static class CMUBodyPartNames
{
    public static string Describe(BodyPartType type, BodyPartSymmetry symmetry)
    {
        var typeName = type switch
        {
            BodyPartType.Torso => "torso",
            BodyPartType.Head => "head",
            BodyPartType.Arm => "arm",
            BodyPartType.Hand => "hand",
            BodyPartType.Leg => "leg",
            BodyPartType.Foot => "foot",
            BodyPartType.Tail => "tail",
            _ => "other",
        };

        var side = symmetry switch
        {
            BodyPartSymmetry.Left => "left",
            BodyPartSymmetry.Right => "right",
            _ => "none",
        };

        return Loc.GetString("cmu-body-part-name", ("type", typeName), ("side", side));
    }
}
