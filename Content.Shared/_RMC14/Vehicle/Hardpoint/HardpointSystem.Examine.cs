using Content.Shared.Containers.ItemSlots;
using Content.Shared.Examine;
using Content.Shared.Verbs;
using Content.Shared._RMC14.Xenonids;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Vehicle;

public sealed partial class HardpointSystem
{
    [Dependency] private ExamineSystemShared _examine = default!;

    private void OnDamageExamineVerb(Entity<HardpointIntegrityComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || HasComp<XenoComponent>(args.User))
            return;

        _examine.AddDetailedExamineVerb(args, ent.Comp, GetDamageExamine(ent),
            Loc.GetString("rmc-vehicle-damage-examine-verb"),
            hoverMessage: Loc.GetString("rmc-vehicle-damage-examine-description"));
    }

    private void OnHardpointExamined(Entity<HardpointIntegrityComponent> ent, ref ExaminedEvent args)
    {
        var (current, max) = GetExamineIntegrity(ent);
        args.PushMarkup(Loc.GetString(GetHardpointConditionString(max > 0f ? current / max : 0f)));
    }

    private (float Current, float Max) GetExamineIntegrity(Entity<HardpointIntegrityComponent> ent)
    {
        if (IsVehicleFrame(ent.Owner) &&
            TryComp(ent.Owner, out HardpointSlotsComponent? slots) &&
            TryComp(ent.Owner, out ItemSlotsComponent? itemSlots) &&
            TryGetVehicleEffectiveIntegrity(ent.Owner, ent.Comp, slots, itemSlots, out var current, out var max))
        {
            return (current, max);
        }

        return (ent.Comp.Integrity, ent.Comp.MaxIntegrity);
    }

    private FormattedMessage GetDamageExamine(Entity<HardpointIntegrityComponent> ent)
    {
        var message = new FormattedMessage();
        var (current, max) = GetExamineIntegrity(ent);
        AppendIntegrity(message, current, max);
        AppendFailures(message, ent.Owner);

        if (TryComp(ent.Owner, out HardpointSlotsComponent? slots) &&
            TryComp(ent.Owner, out ItemSlotsComponent? itemSlots))
        {
            var visited = new HashSet<EntityUid>();
            foreach (var mounted in _topology.GetMountedSlots(ent.Owner, slots, itemSlots))
            {
                if (mounted.Item is not { } item || !visited.Add(item) ||
                    !TryComp(item, out HardpointIntegrityComponent? integrity))
                    continue;

                message.PushNewline();
                message.PushNewline();
                message.AddText(Name(item));
                message.PushNewline();
                AppendIntegrity(message, integrity.Integrity, integrity.MaxIntegrity);
                AppendFailures(message, item);
            }
        }

        if (TryGetArmorExamineModifiers(ent.Owner, out var acid, out var slash, out var bullet, out var explosive, out var blunt))
        {
            message.PushNewline();
            message.AddMarkupOrThrow(Loc.GetString("rmc-hardpoint-armor-modifiers-examine",
                ("acid", FormatModifierValue(acid)), ("slash", FormatModifierValue(slash)),
                ("bullet", FormatModifierValue(bullet)), ("explosive", FormatModifierValue(explosive)),
                ("blunt", FormatModifierValue(blunt))));
        }

        return message;
    }

    private void AppendIntegrity(FormattedMessage message, float current, float max)
    {
        var fraction = max > 0f ? current / max : 0f;
        message.AddMarkupOrThrow(Loc.GetString("rmc-hardpoint-integrity-examine",
            ("color", GetHardpointIntegrityColor(fraction)),
            ("current", (int) MathF.Ceiling(current)), ("max", (int) MathF.Ceiling(max)),
            ("percent", (int) MathF.Round(fraction * 100f))));
    }

    private void AppendFailures(FormattedMessage message, EntityUid uid)
    {
        message.PushNewline();
        if (!TryComp(uid, out VehicleHardpointFailureComponent? failures) || failures.ActiveFailures.Count == 0)
        {
            message.AddText(Loc.GetString("rmc-vehicle-damage-examine-no-faults"));
            return;
        }

        message.AddMarkupOrThrow(Loc.GetString("rmc-vehicle-damage-examine-faults"));
        foreach (var failure in failures.ActiveFailures)
        {
            message.PushNewline();
            message.AddText(Loc.GetString("rmc-vehicle-damage-examine-fault",
                ("fault", GetFailureAlertName(failure)), ("effect", GetFailureEffect(failure))));
        }
    }
}
