using Content.Server.GameTicking;
using Content.Shared._RMC14.Requisitions.Components;
using Content.Shared._RMC14.Vendors;
using Content.Shared.CMU14.Requisitions;
using Content.Shared.CMU14.util;
using Content.Shared.Storage.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14;

public sealed class DestroyOnPresetSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DestroyOnPresetComponent, MapInitEvent>(OnStartup);

        // anything that would delete itself this round shouldn't be on offer either, or people pay for nothing
        SubscribeLocalEvent<CMAutomatedVendorComponent, ComponentInit>(OnVendorInit);
        SubscribeLocalEvent<CMURequisitionsCatalogBuiltEvent>(OnCatalogBuilt);
    }

    private void OnStartup(EntityUid uid, DestroyOnPresetComponent component, MapInitEvent args)
    {
        var gameTicker = EntityManager.System<GameTicker>();
        var preset = gameTicker.Preset;

        if (preset != null)
        {
            var matches = preset.ID == component.Preset;

            // If inverted is true, delete when it does NOT match. Otherwise delete when it matches.
            if ((matches && !component.Inverted) || (!matches && component.Inverted))
            {
                QueueDel(uid);
            }
        }
    }

    private void OnVendorInit(Entity<CMAutomatedVendorComponent> ent, ref ComponentInit args)
    {
        foreach (var section in ent.Comp.Sections)
        {
            section.Entries.RemoveAll(entry => DestroyedThisRound(entry.Id));
        }
    }

    private void OnCatalogBuilt(ref CMURequisitionsCatalogBuiltEvent args)
    {
        if (!TryComp(args.Computer, out RequisitionsComputerComponent? computer))
            return;

        foreach (var category in computer.Categories)
        {
            category.Entries.RemoveAll(entry =>
            {
                var spawnsAnything = false;
                foreach (var id in entry.Entities)
                {
                    if (!DestroyedThisRound(id))
                        return false;

                    spawnsAnything = true;
                }

                if (_prototype.TryIndex(entry.Crate, out var crate) &&
                    crate.TryGetComponent(out StorageFillComponent? fill, EntityManager.ComponentFactory))
                {
                    foreach (var content in fill.Contents)
                    {
                        if (content.PrototypeId is not { } id)
                            continue;

                        if (!DestroyedThisRound(id))
                            return false;

                        spawnsAnything = true;
                    }
                }

                return spawnsAnything;
            });
        }
    }

    private bool DestroyedThisRound(EntProtoId id)
    {
        if (EntityManager.System<GameTicker>().Preset is not { } preset ||
            !_prototype.TryIndex(id, out var proto) ||
            !proto.TryGetComponent(out DestroyOnPresetComponent? destroy, EntityManager.ComponentFactory))
        {
            return false;
        }

        return (preset.ID == destroy.Preset) != destroy.Inverted;
    }
}
