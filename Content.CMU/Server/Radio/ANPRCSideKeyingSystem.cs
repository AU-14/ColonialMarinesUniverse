using Content.Shared.CMU14.Callsigns;
using Content.Shared.CMU14.Fighter;
using Content.Shared.CMU14.Radio;
using Content.Shared.Hands;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Radio;

/// <summary>
///     Keys side-neutral issue stock (<see cref="ANPRCSideKeyedComponent"/>) to GOVFOR or OPFOR. The ship
///     it spawns on decides first, which covers vendors and anything mapped aboard; requisitions build
///     their crates in nullspace, so those wait for the first marine to pick them up. The side's variant
///     is copied onto the entity in place rather than swapped in, since a vendor equips the very entity
///     it spawned.
/// </summary>
public sealed class ANPRCSideKeyingSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private FighterIFFSystem _iff = default!;
    [Dependency] private ANPRCCryptoSystem _crypto = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private SharedContainerSystem _container = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ANPRCSideKeyedComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ANPRCSideKeyedComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ANPRCSideKeyedComponent, GotEquippedHandEvent>(OnEquippedHand);
        SubscribeLocalEvent<ANPRCSideKeyedComponent, EntGotInsertedIntoContainerMessage>(OnInserted);
    }

    private void OnMapInit(Entity<ANPRCSideKeyedComponent> ent, ref MapInitEvent args)
    {
        TryKey(ent, _iff.GetSiteFaction(ent));
    }

    private void OnEquipped(Entity<ANPRCSideKeyedComponent> ent, ref GotEquippedEvent args)
    {
        TryKey(ent, _iff.GetOperatorFaction(args.EquipTarget));
    }

    private void OnEquippedHand(Entity<ANPRCSideKeyedComponent> ent, ref GotEquippedHandEvent args)
    {
        TryKey(ent, _iff.GetOperatorFaction(args.User));
    }

    // an unkeyed card loaded into a keyed set takes the set's side
    private void OnInserted(Entity<ANPRCSideKeyedComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ANPRCCryptoSystem.FillSlotId ||
            HasComp<ANPRCSideKeyedComponent>(args.Container.Owner) ||
            !TryComp(args.Container.Owner, out ANPRCRadioComponent? radio))
        {
            return;
        }

        TryKey(ent, radio.OperatorFaction);
    }

    public bool TryKey(Entity<ANPRCSideKeyedComponent> ent, string? faction)
    {
        if (ent.Comp.Keyed ||
            FighterIFFSystem.Normalize(faction) is not { } side ||
            !ent.Comp.Variants.TryGetValue(side, out var variantId) ||
            !_prototype.TryIndex(variantId, out var variant))
        {
            return false;
        }

        ent.Comp.Keyed = true;

        if (TryComp(ent, out ANPRCFillCardComponent? card) &&
            variant.TryGetComponent(out ANPRCFillCardComponent? cardSource, _compFactory))
        {
            _crypto.KeyFillCard((ent, card), cardSource.Faction, cardSource.Designation);
        }

        if (TryComp(ent, out ANPRCRadioComponent? radio) &&
            variant.TryGetComponent(out ANPRCRadioComponent? radioSource, _compFactory))
        {
            radio.OperatorFaction = radioSource.OperatorFaction;
            radio.CallsignPresets = new List<string>(radioSource.CallsignPresets);
            radio.DFReportFactions = new List<string>(radioSource.DFReportFactions);
            radio.StandardNets = new List<ANPRCDefaultSlot>(radioSource.StandardNets);
            Dirty(ent, radio);
        }

        if (TryComp(ent, out RTORelayComponent? relay) &&
            variant.TryGetComponent(out RTORelayComponent? relaySource, _compFactory))
        {
            relay.BridgedChannels = new(relaySource.BridgedChannels);
            Dirty(ent, relay);
        }

        if (TryComp(ent, out AU14CallsignConsoleComponent? directory) &&
            variant.TryGetComponent(out AU14CallsignConsoleComponent? directorySource, _compFactory))
        {
            directory.Faction = directorySource.Faction;
            Dirty(ent, directory);
        }

        _meta.SetEntityName(ent, variant.Name);
        _meta.SetEntityDescription(ent, variant.Description);

        // a set keys the unkeyed card it was issued with, and re-reads its crypto either way
        if (radio != null)
        {
            if (_container.TryGetContainer(ent, ANPRCCryptoSystem.FillSlotId, out var slot))
            {
                foreach (var contained in slot.ContainedEntities)
                {
                    if (TryComp(contained, out ANPRCSideKeyedComponent? cardKey))
                        TryKey((contained, cardKey), side);
                }
            }

            RaiseLocalEvent(ent, new ANPRCCryptoChangedEvent());
        }

        RemCompDeferred<ANPRCSideKeyedComponent>(ent);
        return true;
    }
}
