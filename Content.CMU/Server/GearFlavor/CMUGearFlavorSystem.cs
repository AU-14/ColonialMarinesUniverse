using Content.Shared._RMC14.Armor;
using Content.Shared._RMC14.Vendors;
using Content.Shared.Clothing.Components;
using Content.Shared.CMU14.GearFlavor;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.GearFlavor;

/// <summary>
///     GOVFOR and OPFOR characters can write a line on their armor and helmet in the character editor.
///     It is written onto the armor and helmet they draw from their vendors and shown when someone
///     examines them wearing it.
/// </summary>
public sealed class CMUGearFlavorSystem : EntitySystem
{
    private static readonly ProtoId<DepartmentPrototype>[] Departments =
    [
        "AU14DepartmentGovernmentForces",
        "AU14DepartmentOpfor",
    ];

    [Dependency] private IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<CMUGearFlavorOwnerComponent, RMCAutomatedVendedUserEvent>(OnVended);
        SubscribeLocalEvent<CMUGearFlavorOwnerComponent, RMCArmorVariantCreatedEvent>(OnArmorVariantCreated);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        var profile = args.Profile;
        if (args.JobId == null ||
            string.IsNullOrEmpty(profile.ArmorFlavorText) && string.IsNullOrEmpty(profile.HelmetFlavorText) ||
            !IsGovforOrOpfor(args.JobId))
        {
            return;
        }

        var owner = EnsureComp<CMUGearFlavorOwnerComponent>(args.Mob);
        owner.Armor = CMUGearFlavorText.Clamp(profile.ArmorFlavorText);
        owner.Helmet = CMUGearFlavorText.Clamp(profile.HelmetFlavorText);
    }

    private bool IsGovforOrOpfor(string job)
    {
        foreach (var departmentId in Departments)
        {
            if (_prototypes.TryIndex(departmentId, out var department) && department.Roles.Contains(job))
                return true;
        }

        return false;
    }

    private void OnVended(Entity<CMUGearFlavorOwnerComponent> ent, ref RMCAutomatedVendedUserEvent args)
    {
        Write(ent.Comp, args.Item);
    }

    // Vended body armor can be swapped for a different variant of itself; the text goes with it.
    private void OnArmorVariantCreated(Entity<CMUGearFlavorOwnerComponent> ent, ref RMCArmorVariantCreatedEvent args)
    {
        Write(ent.Comp, args.New);
    }

    private void Write(CMUGearFlavorOwnerComponent owner, EntityUid item)
    {
        if (!Exists(item) ||
            !HasComp<CMArmorComponent>(item) ||
            !TryComp(item, out ClothingComponent? clothing))
        {
            return;
        }

        string text;
        if ((clothing.Slots & SlotFlags.OUTERCLOTHING) != 0)
            text = owner.Armor;
        else if ((clothing.Slots & SlotFlags.HEAD) != 0)
            text = owner.Helmet;
        else
            return;

        if (string.IsNullOrEmpty(text))
            return;

        var flavor = EnsureComp<CMUGearFlavorTextComponent>(item);
        flavor.Text = text;
        Dirty(item, flavor);
    }
}
