using Content.Server.CMU14.Systems;
using Content.Server.CMU14.Round.Antags.ColonyBounty;
using Content.Server.Humanoid;
using Content.Server.Popups;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.CMU14.Round.Antags.Replicant;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Round.Antags.Replicant;

/// <summary>
/// The replicant antag: spawns covert, picks a colonist to replace from the whole crew,
/// channels the copy anywhere it feels safe, and forges a transponder to pass as them.
/// The wanted alias keeps the pre-copy description, so it goes stale the moment they change.
/// </summary>
public sealed partial class ReplicantSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly HumanoidOrganAppearanceSystem _organs = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly WantedSystem _wanted = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ReplicantComponent, ComponentStartup>(OnReplicantSpawned);
        SubscribeLocalEvent<ReplicantComponent, ReplicantTransformActionEvent>(OnAssumeIdentity);
        SubscribeLocalEvent<ReplicantComponent, ReplicantPickedMessage>(OnTargetPicked);
        SubscribeLocalEvent<ReplicantComponent, ReplicantTransformDoAfterEvent>(OnTransformDoAfter);
    }

    private void OnReplicantSpawned(Entity<ReplicantComponent> ent, ref ComponentStartup args)
    {
        _actions.AddAction(ent, ref ent.Comp.Action, "ActionReplicantAssumeIdentity");

        _wanted.SendFaxToGroup(
            ColonyCmbFax.MarshalBureauFaxGroup,
            "Synth Infiltration Alert",
            ColonyCmbFax.Build("Synth Infiltration Alert",
                "A synthetic unit of unknown provenance is believed to have reached your colony " +
                "with falsified credentials. Intelligence suggests it is capable of assuming the " +
                "likeness of colonists. A bounty has been posted; consult your Records Console " +
                "for the case file."),
            "paper_stamp-cmb",
            new List<StampDisplayInfo>
            {
                new() { StampedColor = Color.FromHex("#b0901b"), StampedName = "CMB" },
            }, ColonyCmbFax.CmbPaperPrototype);
    }

    private void OnAssumeIdentity(Entity<ReplicantComponent> ent, ref ReplicantTransformActionEvent args)
    {
        if (ent.Comp.Transformed)
        {
            _popup.PopupEntity(Loc.GetString("replicant-already-transformed"), ent, args.Performer);
            return;
        }

        if (!TryComp(args.Performer, out ActorComponent? actor))
            return;

        var targets = new List<ReplicantTargetInfo>();
        var enumerator = EntityManager.AllEntityQueryEnumerator<HumanoidProfileComponent, MobStateComponent>();
        while (enumerator.MoveNext(out var uid, out _, out _))
        {
            if (uid == ent.Owner || !_mobState.IsAlive(uid))
                continue;

            targets.Add(new ReplicantTargetInfo(GetNetEntity(uid), MetaData(uid).EntityName));
        }

        targets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        // Force close first to reset BUI state, then reopen
        _ui.CloseUi(ent.Owner, ReplicantUiKey.Key);
        _ui.SetUiState(ent.Owner, ReplicantUiKey.Key, new ReplicantPickerState(targets));
        _ui.OpenUi(ent.Owner, ReplicantUiKey.Key, actor.PlayerSession);
    }

    private void OnTargetPicked(Entity<ReplicantComponent> ent, ref ReplicantPickedMessage args)
    {
        if (ent.Comp.Transformed)
            return;

        var target = GetEntity(args.Target);
        if (!Exists(target) || !_mobState.IsAlive(target))
        {
            _popup.PopupEntity(Loc.GetString("replicant-target-lost"), ent, ent);
            return;
        }

        _ui.CloseUi(ent.Owner, ReplicantUiKey.Key);

        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.ChannelDelay, new ReplicantTransformDoAfterEvent(), ent)
        {
            BreakOnMove = true,
            DuplicateCondition = DuplicateConditions.SameEvent,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
        {
            ent.Comp.PendingTarget = args.Target;
            _popup.PopupEntity(Loc.GetString("replicant-transform-start"), ent, ent);
        }
    }

    private void OnTransformDoAfter(Entity<ReplicantComponent> ent, ref ReplicantTransformDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || ent.Comp.Transformed)
            return;

        args.Handled = true;

        if (ent.Comp.PendingTarget is not { } netTarget)
            return;

        var target = GetEntity(netTarget);
        ent.Comp.PendingTarget = null;
        if (!Exists(target) || !_mobState.IsAlive(target))
        {
            _popup.PopupEntity(Loc.GetString("replicant-target-lost"), ent, ent);
            return;
        }

        if (_organs.TryGetAppearance(target, out var skin, out var eyes, out var markings))
        {
            _organs.TrySetColors(ent, skin, eyes);
            foreach (var (organ, layers) in markings)
            {
                foreach (var (layer, list) in layers)
                    _organs.SetMarkings(ent, organ, layer, list);
            }
        }

        var name = MetaData(target).EntityName;
        _meta.SetEntityName(ent, name);

        ent.Comp.Transformed = true;
        ent.Comp.TargetName = name;
        Dirty(ent);

        SyncTransponder(ent, target, name);
        _popup.PopupEntity(Loc.GetString("replicant-transform-finish", ("name", name)), ent, ent);
    }

    private void SyncTransponder(EntityUid replicant, EntityUid target, string name)
    {
        var query = EntityQueryEnumerator<ForgedTransponderComponent>();
        while (query.MoveNext(out var card, out _))
        {
            var current = card;
            while (EntityManager.TryGetComponent(current, out TransformComponent? xform) && xform.ParentUid.IsValid())
            {
                current = xform.ParentUid;
                if (current != replicant)
                    continue;

                // The forgery must survive an examine, so it mimics the original's actual badge
                // TODO: copy sprite appearance too (RSI path + state)
                _meta.SetEntityName(card, name);
                if (_idCard.TryFindIdCard(target, out var idCard))
                    _meta.SetEntityDescription(card, MetaData(idCard).EntityDescription);
                return;
            }
        }
    }
}

/// <summary>
/// A programmable fake ID. Copies the assumed identity's name and a real badge's description
/// on transform; grants no access, the real thing has to be taken off the original.
/// </summary>
[RegisterComponent]
public sealed partial class ForgedTransponderComponent : Component;
