using System.Linq;
using Content.Shared._RMC14.Marines.Skills;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.UniformAccessories;
using Content.Shared._RMC14.Xenonids;
using Content.Shared.ActionBlocker;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RMC14.Medical.Scanner;

public sealed partial class RMCStethoscopeSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private SkillsSystem _skills = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private SharedContainerSystem _containerSystem = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private INetManager _net = default!;
    [Dependency] private ActionBlockerSystem _blocker = default!;
    [Dependency] private SharedInteractionSystem _interaction = default!;

    private static readonly EntProtoId<SkillDefinitionComponent> MedicalSkill = "RMCSkillMedical";
    private const string NeckSlot = "neck";
    private static readonly string[] AccessorySlots = ["jumpsuit", "outerClothing"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GetVerbsEvent<ExamineVerb>>(OnGlobalStethoscopeExamineVerb, after: new[] { typeof(SharedPopupSystem) });
        SubscribeLocalEvent<RMCStethoscopeComponent, AfterInteractEvent>(OnStethoAfterInteract);
    }

    private void OnStethoAfterInteract(EntityUid uid, RMCStethoscopeComponent comp, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Used != uid || args.Target is not { } target)
            return;
        args.Handled = TryExamine(args.User, target, (uid, comp), fromVerb: false);
    }

    /// <summary>One server-owned output route for held tools and worn-tool verbs.</summary>
    public bool TryExamine(EntityUid user, EntityUid patient, Entity<RMCStethoscopeComponent> tool, bool fromVerb)
    {
        if (!CanExamine(user, patient, tool, fromVerb))
            return false;

        // The server emits the popup/tooltip. A predicted interaction must not
        // also disclose the fallback aggregate readout on the client.
        if (_net.IsClient)
            return true;

        var request = new RMCStethoscopeExamineRequest(user, patient, tool, fromVerb);
        RaiseLocalEvent(ref request);
        if (!request.Handled && CanExamine(user, patient, tool, fromVerb))
            ShowResult(user, patient, GetStethoscopeResults(patient, user), fromVerb);
        return true;
    }

    public void ShowResult(EntityUid user, EntityUid patient, FormattedMessage result, bool fromVerb)
    {
        if (_net.IsClient)
            return;
        if (fromVerb)
            _examine.SendExamineTooltip(user, patient, result, getVerbs: false, centerAtCursor: false);
        else
            _popup.PopupClient(result.ToString(), patient, user);
    }

    public bool CanExamine(EntityUid user, EntityUid patient, Entity<RMCStethoscopeComponent> tool, bool fromVerb)
    {
        return IsLive(user) && IsLive(patient) && IsCurrentTool(user, tool, fromVerb) &&
               _blocker.CanInteract(user, patient) &&
               (fromVerb || _blocker.CanUseHeldEntity(user, tool)) &&
               _interaction.InRangeAndAccessible(user, patient) &&
               // Permission/range callbacks may delete entities or change the tool.
               IsLive(user) && IsLive(patient) && IsCurrentTool(user, tool, fromVerb);
    }

    private bool IsLive(EntityUid uid) => !TerminatingOrDeleted(uid) && !EntityManager.IsQueuedForDeletion(uid);

    private bool IsCurrentTool(EntityUid user, Entity<RMCStethoscopeComponent> tool, bool fromVerb)
    {
        return IsLive(tool) && tool.Comp.LifeStage < ComponentLifeStage.Stopping &&
               TryComp<RMCStethoscopeComponent>(tool, out var current) && ReferenceEquals(current, tool.Comp) &&
               IsAvailable(user, tool, fromVerb);
    }

    private bool IsAvailable(EntityUid user, EntityUid tool, bool fromVerb)
    {
        if (_hands.TryGetActiveItem(user, out var held) && held == tool)
            return true;
        if (!fromVerb)
            return false;
        if (_inventorySystem.TryGetSlotEntity(user, NeckSlot, out var neck) && neck == tool)
            return true;
        foreach (var slot in AccessorySlots)
        {
            if (_inventorySystem.TryGetSlotEntity(user, slot, out var clothing) && IsLive(clothing.Value) &&
                TryComp<UniformAccessoryHolderComponent>(clothing.Value, out var holder) &&
                _containerSystem.TryGetContainer(clothing.Value, holder.ContainerId, out var container) &&
                container.ContainedEntities.Contains(tool))
                return true;
        }
        return false;
    }

    private void OnGlobalStethoscopeExamineVerb(GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || HasComp<XenoComponent>(args.Target))
            return;
        if (!HasStethoscope(args.User, out var stethoscope))
            return;
        var tool = new Entity<RMCStethoscopeComponent>(stethoscope, Comp<RMCStethoscopeComponent>(stethoscope));
        args.Verbs.Add(new ExamineVerb
        {
            Act = () => TryExamine(args.User, args.Target, tool, fromVerb: true),
            Text = Loc.GetString("rmc-stethoscope-verb-text"),
            Message = Loc.GetString("rmc-stethoscope-verb-message"),
            Category = VerbCategory.Examine,
            // IconEntity also binds the network verb identity to the exact tool;
            // a stale menu cannot silently select a replacement stethoscope.
            IconEntity = GetNetEntity(stethoscope),
        });
    }

    private bool HasStethoscope(EntityUid user, out EntityUid stethoscope)
    {
        stethoscope = EntityUid.Invalid;
        if (_hands.TryGetActiveItem(user, out var held) &&
            IsLive(held.Value) && HasComp<RMCStethoscopeComponent>(held.Value))
        {
            stethoscope = held.Value;
            return true;
        }

        if (_inventorySystem.TryGetSlotEntity(user, NeckSlot, out var neck) &&
            IsLive(neck.Value) && HasComp<RMCStethoscopeComponent>(neck.Value))
        {
            stethoscope = neck.Value;
            return true;
        }

        foreach (var slot in AccessorySlots)
        {
            if (!_inventorySystem.TryGetSlotEntity(user, slot, out var slotEntity))
                continue;
            if (!TryComp<UniformAccessoryHolderComponent>(slotEntity.Value, out var accessoryHolder))
                continue;
            if (!_containerSystem.TryGetContainer(slotEntity.Value, accessoryHolder.ContainerId, out var container))
                continue;
            foreach (var accessory in container.ContainedEntities)
            {
                if (!IsLive(accessory) || !HasComp<RMCStethoscopeComponent>(accessory))
                    continue;
                stethoscope = accessory;
                return true;
            }
        }

        return false;
    }

    private FormattedMessage GetStethoscopeResults(EntityUid target, EntityUid? user = null)
    {
        var msg = new FormattedMessage();
        if (user != null && !_skills.HasSkill(user.Value, MedicalSkill, 2))
        {
            msg.AddMarkupOrThrow(Loc.GetString("rmc-stethoscope-unskilled"));
            return msg;
        }

        if (_mobState.IsDead(target))
        {
            msg.AddMarkupOrThrow(Loc.GetString("rmc-stethoscope-dead"));
            return msg;
        }

        if (HasComp<SynthComponent>(target))
        {
            msg.AddMarkupOrThrow(Loc.GetString("rmc-stethoscope-synth"));
            return msg;
        }

        var totalHealth = GetPercentHealth(target) switch
        {
            null => "rmc-stethoscope-nothing",
            >= 87.5f => "rmc-stethoscope-normal",
            >= 62.5f => "rmc-stethoscope-raggedy",
            >= 37.5f => "rmc-stethoscope-hyper",
            >= 0.1f => "rmc-stethoscope-irregular",
            _ => "rmc-stethoscope-dead"
        };

        var locString = totalHealth is "rmc-stethoscope-nothing" or "rmc-stethoscope-hyper" or "rmc-stethoscope-dead"
            ? Loc.GetString(totalHealth)
            : Loc.GetString(totalHealth, ("target", target));

        msg.AddMarkupOrThrow(locString);
        return msg;
    }

    private float? GetPercentHealth(EntityUid target)
    {
        if (!TryComp<DamageableComponent>(target, out var damageable) ||
            !TryComp<MobThresholdsComponent>(target, out var thresholds))
        {
            return null;
        }

        var totalDamage = _damageable.GetAllDamage((target, damageable)).GetTotal().Float();
        var maxHealthThreshold = thresholds.Thresholds.Count > 0
            ? (float)thresholds.Thresholds.Keys.Max()
            : 100f;
        var damagePercent = totalDamage / maxHealthThreshold * 100.0f;
        var healthPercent = 100.0f - MathF.Min(damagePercent, 100.0f);
        return MathF.Max(healthPercent, 0.0f);
    }
}
