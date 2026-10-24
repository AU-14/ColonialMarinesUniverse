using Content.Shared.CMU14.Item.Stain;
using Content.Shared.CMU14.Hygiene;
using Content.Shared.Inventory;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Clothing.Components;
using Content.Shared.Forensics;
using Content.Shared.Nutrition;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Forensics.Components;
using Content.Shared.Forensics.Systems;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server.CMU14.Item.Stain;

/// <summary>
/// Clears visual stains when an existing forensic-cleaning do-after completes.
/// </summary>
public sealed partial class CMUItemStainCleaningSystem : EntitySystem
{
    [Dependency] private CMUItemStainSystem _stains = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private CMUResidueSystem _residue = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedSolutionContainerSystem _solutions = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CMUFourUseSoapComponent, AfterInteractEvent>(OnSoapFloorInteract,
            before: new[] { typeof(ForensicsSystem), typeof(IngestionSystem) });
        SubscribeLocalEvent<CMUFourUseSoapComponent, UserActivateInWorldEvent>(OnSoapFloorActivate);
        SubscribeLocalEvent<CMUFourUseSoapComponent, ExaminedEvent>(OnSoapExamine);
        SubscribeLocalEvent<CMUFourUseSoapComponent, IngestedEvent>(OnSoapEaten);
        SubscribeLocalEvent<CleansForensicsComponent, CleanForensicsDoAfterEvent>(OnCleanDoAfter);
    }

    private void OnSoapEaten(Entity<CMUFourUseSoapComponent> ent, ref IngestedEvent args)
    {
        if (args.Split.Volume <= 0 || ent.Comp.UsesRemaining <= 0)
            return;
        ent.Comp.BitesTowardsUse++;
        if (ent.Comp.BitesTowardsUse < 3)
            return;
        ent.Comp.BitesTowardsUse = 0;
        ent.Comp.UsesRemaining--;
        if (ent.Comp.UsesRemaining == 0)
            QueueDel(ent.Owner);
        else
            _appearance.SetData(ent.Owner, CMUSoapVisuals.Uses, ent.Comp.UsesRemaining);
    }

    private void OnSoapExamine(Entity<CMUFourUseSoapComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cmu-soap-uses", ("uses", ent.Comp.UsesRemaining)));
        args.PushMarkup(Loc.GetString("cmu-soap-floor-instructions"));
    }

    private void OnCleanDoAfter(Entity<CleansForensicsComponent> ent, ref CleanForensicsDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return;

        _stains.TryClean(target);
        if (!TryComp<CMUFourUseSoapComponent>(ent, out var soap) || soap.UsesRemaining <= 0)
            return;
        _residue.Clean(target);
        var slots = _inventory.GetSlotEnumerator(target, SlotFlags.WITHOUT_POCKET);
        while (slots.NextItem(out var item))
        {
            if (HasComp<ClothingComponent>(item))
                _stains.TryClean(item);
        }
        ConsumeSoapUse((ent.Owner, soap), args.User);
    }

    private void ConsumeSoapUse(Entity<CMUFourUseSoapComponent> ent, EntityUid user)
    {
        var soap = ent.Comp;
        if (soap.UsesRemaining <= 0)
            return;
        // Keep edible reagents proportional to the fraction of bar used for washing.
        if (_solutions.TryGetSolution(ent.Owner, "soap", out var solution, out var contents))
            _solutions.SplitSolution(solution.Value, contents.Volume / soap.UsesRemaining);
        soap.UsesRemaining--;
        if (soap.UsesRemaining == 0)
        {
            _popup.PopupEntity(Loc.GetString("cmu-soap-finished"), ent, user);
            QueueDel(ent.Owner);
        }
        else
            _appearance.SetData(ent.Owner, CMUSoapVisuals.Uses, soap.UsesRemaining);
    }
}
