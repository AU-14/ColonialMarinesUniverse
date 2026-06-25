using Content.Server.Popups;
using Content.Shared._CMU14.Threats;
using Content.Shared.Interaction;
using Content.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Ops.ThirdParty;

public sealed partial class SpawnThirdPartyToolSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private UseDelaySystem _useDelay = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private ThirdPartySystem _thirdPartySystem = default!;

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
        if (TryComp(component.Owner, out UseDelayComponent? useDelay)
            && _useDelay.IsDelayed((component.Owner, useDelay)))
            return false;

        if (!_prototype.TryIndex(component.Comp.Party, out ThirdPartyPrototype? party))
        {
            _popup.PopupEntity($"No third party prototype found with ID: {component.Comp.Party.Id}", component.Owner,
                user);
            return false;
        }

        if (!_prototype.TryIndex(party.PartySpawn, out PartySpawnPrototype? partySpawnProto))
        {
            _popup.PopupEntity($"No PartySpawn prototype found for third party {component.Comp.Party.Id}.",
                component.Owner, user);
            return false;
        }

        bool spawned = _thirdPartySystem.SpawnThirdParty(party, partySpawnProto, false, null, component.Comp.Dropship);

        if (!spawned)
        {
            _popup.PopupEntity($"Failed to spawn third party {component.Comp.Party.Id}.", component.Owner, user);
            return false;
        }

        _popup.PopupEntity($"Called in third party {component.Comp.Party.Id}.", user, user);
        Del(component.Owner);
        return true;
    }
}
