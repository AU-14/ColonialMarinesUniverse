using Content.Shared._RMC14.Marines;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;

namespace Content.Shared.CMU14.ForceOnForce;

public sealed partial class ForceOnForceUniformSystem : EntitySystem
{
    [Dependency] private InventorySystem _inventory = default!;

    public bool IsUnidentified(EntityUid target, EntityUid? viewer)
    {
        return Classify(target, viewer) != ForceOnForceIdentification.None;
    }

    /// <summary>
    ///     Splits living targets into friendly, direct competition and unfamiliar parties.
    ///     War sides are govfor and opfor; a side component counts a third party whose
    ///     allegiance matched a platoon as a member of that side.
    /// </summary>
    public ForceOnForceIdentification Classify(EntityUid target, EntityUid? viewer)
    {
        if (TryComp<MobStateComponent>(target, out var mob) && mob.CurrentState == MobState.Dead)
            return ForceOnForceIdentification.None;

        // Only FoF viewers have a uniform policy. Friendly personnel keep their normal identifiers.
        if (!TryComp<ForceOnForceUniformComponent>(viewer, out var policy) ||
            !TryComp<MarineComponent>(viewer, out var viewingMarine) ||
            !TryComp<MarineComponent>(target, out var targetMarine) ||
            string.IsNullOrEmpty(viewingMarine.Faction) ||
            string.IsNullOrEmpty(targetMarine.Faction))
        {
            return ForceOnForceIdentification.None;
        }

        var viewerSide = WarSide(viewer.Value, viewingMarine);
        var targetSide = WarSide(target, targetMarine);

        if (viewerSide == null)
        {
            // A neutral without a war side sees both armies as the direct competition and
            // cannot tell other unaffiliated parties apart.
            return targetSide != null
                ? ForceOnForceIdentification.Hostile
                : ForceOnForceIdentification.Unknown;
        }

        if (targetSide == viewerSide)
            return ForceOnForceIdentification.None;

        if (targetSide == null)
        {
            // An unaffiliated party stays unknown even in a recognized uniform.
            return RecognizesUniform(policy, target)
                ? ForceOnForceIdentification.None
                : ForceOnForceIdentification.Unknown;
        }

        // The opposing side is the direct competition unless a recognized uniform identifies it.
        return RecognizesUniform(policy, target)
            ? ForceOnForceIdentification.None
            : ForceOnForceIdentification.Hostile;
    }

    private string? WarSide(EntityUid mob, MarineComponent marine)
    {
        var faction = marine.Faction?.ToLowerInvariant();
        if (faction is "govfor" or "opfor")
            return faction;

        return TryComp<ForceOnForceSideComponent>(mob, out var side) && side.Side is "govfor" or "opfor"
            ? side.Side
            : null;
    }

    private bool RecognizesUniform(ForceOnForceUniformComponent policy, EntityUid target)
    {
        if (!_inventory.TryGetSlotEntity(target, "jumpsuit", out var uniform) ||
            MetaData(uniform.Value).EntityPrototype is not { } prototype)
            return false;
        // Uniforms of another faction can inherit a friendly uniform's components.
        // Recognize the issued/catalogued items themselves, not every descendant.
        return policy.Uniforms.Contains(prototype.ID);
    }
}
