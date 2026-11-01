using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Shared.Access.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Round.Antags.GovForVeteran;

public sealed partial class GovForVeteranRuleSystem : GameRuleSystem<GovForVeteranRuleComponent>
{
    private const string BackSlot = "back";

    public static readonly EntProtoId VeteranSpecifier = "GovForVeteran";
    public static readonly EntProtoId KitBox = "CMUVeteranKitBox";

    /// <summary>
    ///     The ordinary civilian satchel from the colonist loadout, used only when a job spawns bagless.
    /// </summary>
    public static readonly EntProtoId FallbackSatchel = "CMSatchel";

    public static readonly EntProtoId Dogtag = "CMUVeteranDogtag";

    public const string DogtagRank = "Sergeant Major";

    /// <summary>
    ///     The name that earns the dogtag.
    /// </summary>
    public const string DogtagNameFragment = "a.w";

    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedStorageSystem _storage = default!;

    private EntityQuery<StorageComponent> _storageQuery;

    public override void Initialize()
    {
        base.Initialize();

        _storageQuery = GetEntityQuery<StorageComponent>();

        SubscribeLocalEvent<GovForVeteranRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
    }

    private void OnAntagSelected(Entity<GovForVeteranRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Def.ID != VeteranSpecifier.Id)
            return;

        PackKit(args.EntityUid, args.Session);
    }

    private void PackKit(EntityUid mob, ICommonSession? session)
    {
        if (TerminatingOrDeleted(mob))
            return;

        var coordinates = Transform(mob).Coordinates;

        if (!_inventory.TryGetSlotEntity(mob, BackSlot, out var bag) ||
            !_storageQuery.TryComp(bag, out var storage))
        {
            var satchel = Spawn(FallbackSatchel, coordinates);
            if (!_storageQuery.TryComp(satchel, out storage) ||
                !_inventory.TryEquip(mob, satchel, BackSlot, silent: true, force: true))
            {
                Del(satchel);

                // No bag at all: leave the case at his feet rather than deleting his gear.
                Spawn(KitBox, coordinates);
                Log.Warning($"Could not give {ToPrettyString(mob)} a bag for the veteran kit; the kit was dropped instead.");
                return;
            }

            bag = satchel;
        }

        var box = Spawn(KitBox, coordinates);
        TryAddDogtag(mob, session, box);

        if (_storage.Insert(bag.Value, box, out _, storageComp: storage, playSound: false))
            return;

        // The bag is already full: keep the kit on him without deleting or duplicating anything.
        if (!_hands.TryPickupAnyHand(mob, box, checkActionBlocker: false))
            Log.Warning($"Could not fit the veteran kit into {ToPrettyString(bag.Value)} for {ToPrettyString(mob)}; the kit was dropped at their feet.");
    }

    private void TryAddDogtag(EntityUid mob, ICommonSession? session, EntityUid box)
    {
        var characterName = TryComp<MetaDataComponent>(mob, out var meta) ? meta.EntityName : string.Empty;
        var accountName = session?.Name ?? string.Empty;

        if (!characterName.Contains(DogtagNameFragment, StringComparison.OrdinalIgnoreCase) &&
            !accountName.Contains(DogtagNameFragment, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var dogtag = Spawn(Dogtag, Transform(box).Coordinates);
        if (TryComp<IdCardComponent>(dogtag, out var id))
        {
            id.FullName = characterName;
            id.LocalizedJobTitle = DogtagRank;
            Dirty(dogtag, id);
        }

        if (TryComp<StorageComponent>(box, out var boxStorage) &&
            _storage.Insert(box, dogtag, out _, storageComp: boxStorage, playSound: false))
        {
            return;
        }

        // No room in the case: keep the tags on him rather than deleting them.
        if (!_hands.TryPickupAnyHand(mob, dogtag, checkActionBlocker: false))
            Log.Warning($"Could not fit the veteran dogtag into {ToPrettyString(box)}; it was dropped beside it.");
    }
}
