using Content.Server.CMU14.Roles;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.CMU14.Radio;

public sealed partial class AccessoryHeadsetSystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedUniformAccessorySystem _accessories = default!;
    private static readonly EntProtoId CellEarpiece = "AU14CLFHeadset";
    private static readonly EntProtoId MedicalEarpiece = "CMUInsurgentMedicalEarpiece";

    private void InitializeInsurgentIssue()
    {
        SubscribeLocalEvent<InventoryComponent, CMURoundJobProfileAppliedEvent>(OnInsurgentJobEquipped);
        SubscribeLocalEvent<CMUPendingEarpieceComponent, DidEquipEvent>(OnPendingUniformEquipped);
    }

    private void OnInsurgentJobEquipped(Entity<InventoryComponent> ent, ref CMURoundJobProfileAppliedEvent args)
    {
        if (args.Job.RoundForce != "CLF")
            return;

        var prototype = args.Job.RoundRole is "CLFPhysician" or "CLFSurvSurgeon" ? MedicalEarpiece : CellEarpiece;
        _inventory.TryGetSlotEntity(ent.Owner, "jumpsuit", out var uniform);
        if (uniform is { } worn && HasIssuedEarpiece(worn, prototype))
            return;

        var earpiece = SpawnAtPosition(prototype, Transform(ent).Coordinates);
        if (uniform is { } jumpsuit && _accessories.TryInsertUniformAccessory(earpiece, jumpsuit, ent))
            return;

        // Some ghost roles receive their clothing from a preset after their job gear.
        if (_inventory.TryEquip(ent.Owner, earpiece, "pocket1", silent: true) ||
            _inventory.TryEquip(ent.Owner, earpiece, "pocket2", silent: true) ||
            _hands.TryPickupAnyHand(ent.Owner, earpiece, checkActionBlocker: false))
            EnsureComp<CMUPendingEarpieceComponent>(ent).Item = earpiece;
    }

    private bool HasIssuedEarpiece(EntityUid uniform, EntProtoId prototype)
    {
        if (!TryComp<UniformAccessoryHolderComponent>(uniform, out var holder) ||
            !_container.TryGetContainer(uniform, holder.ContainerId, out var accessories))
            return false;

        foreach (var item in accessories.ContainedEntities)
        {
            if (MetaData(item).EntityPrototype?.ID == prototype.Id)
                return true;
        }
        return false;
    }

    private void OnPendingUniformEquipped(Entity<CMUPendingEarpieceComponent> ent, ref DidEquipEvent args)
    {
        if ((args.SlotFlags & SlotFlags.INNERCLOTHING) == 0)
            return;

        var item = ent.Comp.Item;
        if (!TryComp<TransformComponent>(item, out var transform) || transform.ParentUid != ent.Owner)
        {
            RemComp<CMUPendingEarpieceComponent>(ent);
            return;
        }

        if (MetaData(item).EntityPrototype is { } proto && HasIssuedEarpiece(args.Equipment, proto.ID))
            QueueDel(item);
        else if (!_accessories.TryInsertUniformAccessory(item, args.Equipment, ent))
            return;

        RemComp<CMUPendingEarpieceComponent>(ent);
    }
}

[RegisterComponent]
public sealed partial class CMUPendingEarpieceComponent : Component
{
    public EntityUid Item;
}
