using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.CMU14.Xenomorphs.Pathogen.Overmind;

namespace Content.Shared._RMC14.Dropship;

public abstract partial class SharedDropshipSystem
{
    private bool HasSurvivingBlightCore(EntityUid overmind, EntityUid departingGrid)
    {
        if (!TryComp<HiveMemberComponent>(overmind, out var member) || member.Hive == null)
            return false;

        var cores = EntityQueryEnumerator<CMUBlightCoreComponent, HiveMemberComponent, TransformComponent>();
        while (cores.MoveNext(out var core, out _, out var coreMember, out var transform))
        {
            if (coreMember.Hive == member.Hive && !TerminatingOrDeleted(core) && transform.GridUid != null &&
                (transform.GridUid == departingGrid || !_planet.IsOnPlanetLevel(transform)))
                return true;
        }

        return false;
    }
}
