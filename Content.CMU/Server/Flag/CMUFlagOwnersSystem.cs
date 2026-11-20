using Content.Server.CMU14.Round;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._RMC14.Weapons.Ranged.IFF;
using Content.Shared.CMU14.Allegiance;
using Content.Shared.CMU14.Flag;
using Content.Shared.CMU14.util;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Flag;

/// <summary>
///     Resolves which sides a flag belongs to this round from the platoons each side fields, and which jobs a
///     third party's ghost role mobs hand out.
/// </summary>
public sealed class CMUFlagOwnersSystem : EntitySystem
{
    [Dependency] private IComponentFactory _factory = default!;
    [Dependency] private PlatoonSpawnRuleSystem _platoons = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    /// <summary>
    ///     INSFOR is not a platoon: every insurgent, recruit and reinforcement carries the CLF IFF instead.
    /// </summary>
    private static readonly ProtoId<AllegiancePrototype> ClfAllegiance = "CLF";

    private static readonly EntProtoId<IFFFactionComponent> ClfIff = "FactionCLF";
    private static readonly EntProtoId<IFFFactionComponent> GovforIff = "GOVFOR";
    private static readonly EntProtoId<IFFFactionComponent> OpforIff = "OPFOR";

    public override void Initialize()
    {
        SubscribeLocalEvent<CMUGetFlagOwnersEvent>(OnGetFlagOwners);
        SubscribeLocalEvent<CMUGetGhostRoleJobEvent>(OnGetGhostRoleJob);
    }

    private void OnGetGhostRoleJob(ref CMUGetGhostRoleJobEvent args)
    {
        if (_prototypes.TryIndex<EntityPrototype>(args.Mob, out var mob) &&
            mob.TryGetComponent(out GhostRoleComponent? ghostRole, _factory) &&
            ghostRole.JobProto is { } job)
        {
            args.Job = job.Id;
        }
    }

    private void OnGetFlagOwners(ref CMUGetFlagOwnersEvent args)
    {
        if (args.Allegiances.Contains(ClfAllegiance))
        {
            args.Owners.Add(ClfIff);
            args.Handled = true;
        }

        var govfor = _platoons.SelectedGovforPlatoon;
        var opfor = _platoons.SelectedOpforPlatoon;
        if (govfor == null && opfor == null)
            return;

        args.Handled = true;
        if (Fields(govfor, args.Allegiances))
            args.Owners.Add(GovforIff);

        if (Fields(opfor, args.Allegiances))
            args.Owners.Add(OpforIff);
    }

    private static bool Fields(PlatoonPrototype? platoon, IReadOnlySet<ProtoId<AllegiancePrototype>> allegiances)
    {
        return platoon?.Allegiance is { } allegiance && allegiances.Contains(allegiance);
    }
}
