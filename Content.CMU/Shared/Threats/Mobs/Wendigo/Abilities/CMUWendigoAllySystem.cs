using Content.Shared.CMU14.Threats.Mobs.Wendigo.Pack;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared.CMU14.Threats.Mobs.Wendigo.Abilities;

/// <summary>
/// Shared ally rule for the Wendigo abilities: pack allies (pack members, a tamed Wendigo and its master)
/// plus any two Wendigo-tagged mobs, so the natural xeno Wendigo and the lab-made ones spare each other.
/// </summary>
public sealed partial class CMUWendigoAllySystem : EntitySystem
{
    [Dependency] private SharedCMUWendigoPackSystem _pack = default!;
    [Dependency] private TagSystem _tags = default!;

    private static readonly ProtoId<TagPrototype> WendigoTag = "Wendigo";

    public bool IsWendigoAlly(EntityUid a, EntityUid b)
    {
        if (_pack.IsAlly(a, b))
            return true;

        return _tags.HasTag(a, WendigoTag) && _tags.HasTag(b, WendigoTag);
    }
}
