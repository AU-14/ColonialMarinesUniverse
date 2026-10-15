using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Religion;

/// <summary>
///     A religion a character can follow, picked in the character editor's background section.
/// </summary>
[Prototype("cmuReligion")]
public sealed partial class CMUReligionPrototype : IPrototype
{
    /// <summary>
    ///     The religion every character starts with, and the one that cannot pray.
    /// </summary>
    public static readonly ProtoId<CMUReligionPrototype> Atheist = "CMUAtheist";

    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    ///     Where it sits in the character editor's list; lower comes first.
    /// </summary>
    [DataField]
    public int Order;
}
