using Content.Server.AU14.ThirdParty;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared.AU14.Threats;
using Content.Shared.Interaction;
using Content.Shared.Timing;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.AU14.ThirdParty;

public sealed partial class SpawnThirdPartyToolSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private PopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpawnThirdPartyToolComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<SpawnThirdPartyToolComponent, UserActivateInWorldEvent>(OnActivateInWorld);
    }

    private void OnAfterInteract(Entity<SpawnThirdPartyToolComponent> component, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        if (TryUseTool(component, args.User))
            args.Handled = true;
    }

    private void OnActivateInWorld(Entity<SpawnThirdPartyToolComponent> component, ref UserActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (TryUseTool(component, args.User))
            args.Handled = true;
    }

    private bool TryUseTool(Entity<SpawnThirdPartyToolComponent> component, EntityUid user)
    {
        if (TryComp<UseDelayComponent>(component.Owner, out var useDelay)
            && _useDelay.IsDelayed((component.Owner, useDelay)))
        {
            return false;
        }

        if (!_prototype.TryIndex(component.Comp.Party, out AuThirdPartyPrototype? party))
        {
            _popup.PopupEntity($"No third party prototype found with ID: {component.Comp.Party.Id}", component.Owner, user);
            return false;
        }

        if (!_prototype.TryIndex<PartySpawnPrototype>(party.PartySpawn, out var partySpawnProto))
        {
            _popup.PopupEntity($"No PartySpawn prototype found for third party {component.Comp.Party.Id}.", component.Owner, user);
            return false;
        }

        var thirdPartySystem = EntitySystem.Get<AuThirdPartySystem>();
        var spawned = thirdPartySystem.SpawnThirdParty(party, partySpawnProto, false, null, component.Comp.Dropship);

        if (!spawned)
        {
            _popup.PopupEntity($"Failed to spawn third party {component.Comp.Party.Id}.", component.Owner, user);
            return false;
        }

        EntityManager.DeleteEntity(component.Owner);
        _popup.PopupEntity($"Called in third party {component.Comp.Party.Id}.", user, user);
        return true;
    }
}
