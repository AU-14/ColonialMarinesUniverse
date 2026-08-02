using Content.Server.Humanoid;
using Content.Shared.CCVar;
using Content.Shared._CMU14.Yautja;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.DetailExaminable;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Preferences;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Server._CMU14.Yautja;

public sealed partial class YautjaProfileApplySystem : EntitySystem
{
    [Dependency] private HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private SharedContainerSystem _containers = default!;
    [Dependency] private ClothingSystem _clothing = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private SharedItemSystem _items = default!;
    [Dependency] private MetaDataSystem _meta = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;

    private static readonly Dictionary<string, string> PostVendProfileSlots = new()
    {
        ["CMUYautjaClanArmor"] = "outerClothing",
        ["CMUYautjaClanArmorScalable"] = "outerClothing",
        ["CMUYautjaHeavyClanArmor"] = "outerClothing",
        ["CMUYautjaMask"] = "mask",
        ["CMUYautjaMaskScalable"] = "mask",
        ["CMUYautjaMaskAccessory01Ebony"] = "mask",
        ["CMUYautjaClanGreaves"] = "shoes",
        ["CMUYautjaClanGreavesScalable"] = "shoes",
        ["CMUYautjaCapeFull"] = "back",
        ["CMUYautjaCapeCeremonial"] = "back",
        ["CMUYautjaCapeThird"] = "back",
        ["CMUYautjaCapeHalf"] = "back",
        ["CMUYautjaCapeQuarter"] = "back",
        ["CMUYautjaCapePoncho"] = "back",
        ["CMUYautjaCapeDamaged"] = "back",
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<YautjaAppliedProfileComponent, RMCAutomatedVendedUserEvent>(OnAutomatedVendorVended);
    }

    public void ApplyProfile(
        EntityUid uid,
        YautjaCharacterProfile yautjaProfile,
        YautjaRank? authoritativeRank = null,
        YautjaProfileCapabilities? authoritativeCapabilities = null,
        bool equipProfileGear = true)
    {
        if (!TryComp(uid, out HumanoidAppearanceComponent? humanoid))
            return;

        // A client-supplied profile is never an authority for clan rank. Ordinary
        // spawns stay Blooded until the server passes the persisted rank explicitly.
        var authoritativeBaseRank = authoritativeCapabilities?.Rank ??
                                    YautjaRankManager.CanonicalHunterSpawnRank(
                                        authoritativeRank ?? YautjaRank.Blooded);
        var capabilities = authoritativeCapabilities ?? new YautjaProfileCapabilities(
            authoritativeBaseRank,
            YautjaRankResolver.CanUseUnique(authoritativeBaseRank),
            false);
        var profile = yautjaProfile.SanitizeForCapabilities(capabilities);
        var rank = capabilities.ForStatus(profile.Status).Rank;
        EnsureComp<YautjaAppliedProfileComponent>(uid).Profile = profile.Clone();

        var yautja = EnsureComp<YautjaComponent>(uid);
        yautja.ClanRank = rank;
        Dirty(uid, yautja);

        var humanoidProfile = HumanoidCharacterProfile.DefaultWithSpecies("Yautja")
            .WithName(profile.Name)
            .WithAge(profile.Age)
            .WithSex(profile.Sex)
            .WithGender(profile.Gender)
            .WithCharacterAppearance(profile.Appearance);

        _humanoid.LoadProfile(uid, humanoidProfile, humanoid);
        _meta.SetEntityName(uid, profile.Name);

        EntityUid? mask = null;
        EntityUid? bracer = null;
        EntityUid? cape = null;
        if (equipProfileGear)
        {
            ReplaceEquipped(uid, "outerClothing", profile.ArmorPrototype);
            mask = ReplaceEquipped(uid, "mask", profile.MaskPrototype);
            ReplaceEquipped(uid, "shoes", profile.GreavesPrototype);
            bracer = ReplaceEquipped(uid, "gloves", profile.BracerPrototype);
            cape = ReplaceEquipped(uid, "back", profile.CapePrototype);
        }
        else if (_inventory.TryGetSlotEntity(uid, "gloves", out var equippedBracer))
        {
            bracer = equippedBracer;
        }

        if (mask != null)
            ApplyMaskAccessory(mask.Value, profile);

        if (bracer != null)
            ApplyBracerSettings(bracer.Value, profile);

        if (cape != null)
            ApplyCapeColor(cape.Value, profile);

        ApplyFlavorText(uid, profile);
    }

    private void OnAutomatedVendorVended(Entity<YautjaAppliedProfileComponent> ent, ref RMCAutomatedVendedUserEvent args)
    {
        if (CompOrNull<MetaDataComponent>(args.Item)?.EntityPrototype?.ID is not { } vended ||
            !PostVendProfileSlots.TryGetValue(vended, out var slot))
        {
            return;
        }

        var profile = ent.Comp.Profile;

        switch (vended)
        {
            case "CMUYautjaMaskAccessory01Ebony":
                if (profile.MaskAccessoryPrototype is not { } prototype ||
                    !_prototypes.HasIndex<EntityPrototype>(prototype))
                {
                    Del(args.Item);
                    return;
                }

                if (_inventory.TryGetSlotEntity(ent, slot, out var mask))
                    ApplyMaskAccessory(mask.Value, profile);

                Del(args.Item);
                return;
            case "CMUYautjaClanArmor":
                ReplaceVended(ent, slot, profile.ArmorPrototype, args.Item);
                return;
            case "CMUYautjaClanArmorScalable":
                ApplyVendedProfileVisuals(ent, slot, profile.ArmorPrototype, args.Item);
                return;
            case "CMUYautjaHeavyClanArmor":
                ApplyVendedProfileVisuals(ent, slot, HeavyArmorPrototype(profile.ArmorMaterial), args.Item);
                return;
            case "CMUYautjaMask":
                ReplaceVended(ent, slot, profile.MaskPrototype, args.Item);
                return;
            case "CMUYautjaMaskScalable":
                ApplyVendedProfileVisuals(ent, slot, profile.MaskPrototype, args.Item);
                return;
            case "CMUYautjaClanGreaves":
                ReplaceVended(ent, slot, profile.GreavesPrototype, args.Item);
                return;
            case "CMUYautjaClanGreavesScalable":
                ApplyVendedProfileVisuals(ent, slot, profile.GreavesPrototype, args.Item);
                return;
            case "CMUYautjaCapeFull":
            case "CMUYautjaCapeCeremonial":
            case "CMUYautjaCapeThird":
            case "CMUYautjaCapeHalf":
            case "CMUYautjaCapeQuarter":
            case "CMUYautjaCapePoncho":
            case "CMUYautjaCapeDamaged":
                ApplyVendedCapeColor(ent, slot, profile, args.Item);
                return;
            default:
                return;
        }
    }

    private static string HeavyArmorPrototype(YautjaGearMaterial material)
    {
        return material switch
        {
            YautjaGearMaterial.Bronze => "CMUYautjaHeavyClanArmorBronze",
            YautjaGearMaterial.Silver => "CMUYautjaHeavyClanArmorSilver",
            YautjaGearMaterial.Crimson => "CMUYautjaHeavyClanArmorCrimson",
            YautjaGearMaterial.Bone => "CMUYautjaHeavyClanArmorBone",
            _ => "CMUYautjaHeavyClanArmor",
        };
    }

    private EntityUid? ReplaceEquipped(EntityUid uid, string slot, string prototype)
    {
        if (!_prototypes.HasIndex<EntityPrototype>(prototype))
            return null;

        if (_inventory.TryGetSlotEntity(uid, slot, out var equipped))
            Del(equipped.Value);

        var item = Spawn(prototype, Transform(uid).Coordinates);
        if (_inventory.TryEquip(uid, item, slot, silent: true, force: true))
            return item;

        Del(item);
        return null;
    }

    private void ReplaceVended(EntityUid uid, string slot, string prototype, EntityUid vended)
    {
        if (!_prototypes.HasIndex<EntityPrototype>(prototype))
            return;

        ReplaceEquipped(uid, slot, prototype);

        if (!Deleted(vended))
            Del(vended);
    }

    private void ApplyVendedProfileVisuals(EntityUid uid, string slot, string prototype, EntityUid vended)
    {
        if (!_prototypes.TryIndex<EntityPrototype>(prototype, out var visualPrototype))
            return;

        if (!_inventory.TryGetSlotEntity(uid, slot, out var equipped) ||
            equipped.Value != vended)
        {
            if (!_inventory.TryEquip(uid, vended, slot, silent: true, force: true))
                return;
        }

        if (Deleted(vended))
            return;

        CopyProfileVisuals(vended, visualPrototype);
    }

    private void ApplyVendedCapeColor(EntityUid uid, string slot, YautjaCharacterProfile profile, EntityUid vended)
    {
        if (!_inventory.TryGetSlotEntity(uid, slot, out var equipped) ||
            equipped.Value != vended)
        {
            if (!_inventory.TryEquip(uid, vended, slot, silent: true, force: true))
                return;
        }

        ApplyCapeColor(vended, profile);
    }

    private void ApplyCapeColor(EntityUid cape, YautjaCharacterProfile profile)
    {
        var capeComp = EnsureComp<YautjaCapeComponent>(cape);
        capeComp.Color = YautjaCharacterProfile.Default.CapeColor;
        Dirty(cape, capeComp);
    }

    private void CopyProfileVisuals(EntityUid uid, EntityPrototype visualPrototype)
    {
        if (TryComp(uid, out ItemComponent? item) &&
            visualPrototype.TryGetComponent(out ItemComponent? visualItem, EntityManager.ComponentFactory))
        {
            _items.CopyVisuals(uid, visualItem, item);
        }

        if (TryComp(uid, out ClothingComponent? clothing) &&
            visualPrototype.TryGetComponent("Clothing", out ClothingComponent? visualClothing))
        {
            _clothing.CopyVisuals(uid, visualClothing, clothing);
        }
    }

    private void ApplyMaskAccessory(EntityUid mask, YautjaCharacterProfile profile)
    {
        if (profile.MaskAccessoryPrototype is not { } prototype ||
            !_prototypes.HasIndex<EntityPrototype>(prototype) ||
            !TryComp(mask, out YautjaMaskAccessoryHolderComponent? holder))
        {
            return;
        }

        var container = _containers.EnsureContainer<ContainerSlot>(mask, holder.ContainerId);
        if (container.ContainedEntity is { } oldAccessory)
            Del(oldAccessory);

        var accessory = Spawn(prototype, Transform(mask).Coordinates);
        if (!_containers.Insert(accessory, container, force: true))
            Del(accessory);
    }

    private void ApplyBracerSettings(EntityUid bracer, YautjaCharacterProfile profile)
    {
        if (TryComp(bracer, out YautjaBracerComponent? bracerComp))
        {
            bracerComp.TranslatorType = profile.TranslatorType;
            bracerComp.InvisibilitySound = profile.InvisibilitySound;
            bracerComp.OwnerRank = profile.OwnerRank;
            if (profile.InvisibilitySound == YautjaInvisibilitySound.Retro)
            {
                bracerComp.CloakOnSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_cloakon.wav");
                bracerComp.CloakOffSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/Equipment/pred_cloakoff.wav");
            }
            else
            {
                bracerComp.CloakOnSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_cloakon_modern.ogg");
                bracerComp.CloakOffSound = new SoundPathSpecifier("/Audio/_CMU14/Yautja/pred_cloakoff_modern.ogg");
            }

            Dirty(bracer, bracerComp);
        }

        if (!TryComp(bracer, out YautjaGearContainerComponent? gear))
            return;

        gear.GearPrototypes[YautjaGearKind.Caster] = profile.CasterPrototype;
        if (gear.Gear.Remove(YautjaGearKind.Caster, out var oldCaster))
            Del(oldCaster);

        Dirty(bracer, gear);
    }

    private void ApplyFlavorText(EntityUid uid, YautjaCharacterProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.FlavorText) ||
            !_config.GetCVar(CCVars.FlavorText))
        {
            return;
        }

        EnsureComp<DetailExaminableComponent>(uid).Content = profile.FlavorText;
    }
}
