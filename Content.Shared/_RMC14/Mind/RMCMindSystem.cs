using Content.Shared._RMC14.GameStates;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Shared._RMC14.Mind;

public sealed partial class RMCMindSystem : EntitySystem
{
    [Dependency] private SharedRMCPvsSystem _rmcPvs = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        // Mind role PVS overrides are server-only; replay player attachments may precede mind startup on the client.
        if (!_net.IsServer)
            return;

        SubscribeLocalEvent<MindContainerComponent, PlayerAttachedEvent>(OnMindContainerPlayedAttached);
    }

    private void OnMindContainerPlayedAttached(Entity<MindContainerComponent> ent, ref PlayerAttachedEvent args)
    {
        if (!TryComp(ent.Comp.Mind, out MindComponent? mind))
            return;

        foreach (var role in mind.MindRoleContainer.ContainedEntities)
        {
            _rmcPvs.AddSessionOverride(role, args.Player);
        }
    }
}
