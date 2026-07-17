using System;
using System.Collections.Generic;
using Content.Shared._AU14.Insurgency;
using Content.Shared._CMU14.Threats.Mobs.CLF;
using Content.Shared.GameTicking;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._AU14.Insurgency.Package;

/// <summary>
///     Grants and dispenses "A Package". On a faction being applied, every member whose job has a
///     role loadout receives a package in hand (or dropped at their feet if both hands are full),
///     with a quiet member-only notification. Using the package unpacks that member's gear.
///
///     Event-driven: grant happens once on the applied event, dispense happens on the item's use.
///     Nothing polls.
/// </summary>
public sealed class APackageSystem : EntitySystem
{
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private SharedJobSystem _job = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private InsurgencyFactionApplySystem _apply = default!;

    // ---------------------------------------------------------------------
    // Tunables. The package prototype and the grant notification sound in one place.
    // ---------------------------------------------------------------------
    private static readonly EntProtoId PackagePrototype = "AU14APackage";
    private static readonly SoundSpecifier GrantSound = new SoundPathSpecifier("/Audio/Effects/rustle1.ogg");

    // Roundstart CLF jobs that receive a package. Excludes reinforcements and tattoo-recruited members,
    // who are not part of the organized cell's loadout plan. One place to change who qualifies.
    private static readonly HashSet<string> PackageJobIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "AU14JobCLFCellLeader",
        "AU14JobCLFGuerilla",
        "AU14JobCLFPhysician",
        "AU14JobCLFSurgeon",
        "AU14JobCLFSapper",
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<InsurgencyFactionAppliedEvent>(OnFactionApplied);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnSpawnComplete);
        SubscribeLocalEvent<APackageComponent, UseInHandEvent>(OnUse);
    }

    // A CLF member who spawns after the faction was already chosen still gets their package, as long as
    // they spawned into a roundstart CLF job (not a reinforcement or a tattoo recruit).
    private void OnSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        var faction = _apply.GetActiveFaction();
        if (faction == null || faction.RoleLoadouts.Count == 0)
            return;

        if (ev.JobId == null || !PackageJobIds.Contains(ev.JobId))
            return;

        if (!HasComp<CLFMemberComponent>(ev.Mob))
            return;

        if (TryResolveContents(ev.Mob, faction.RoleLoadouts, out var contents))
            GrantPackage(ev.Mob, ev.Player, contents);
    }

    private void OnFactionApplied(ref InsurgencyFactionAppliedEvent ev)
    {
        var loadouts = ev.Definition.RoleLoadouts;
        if (loadouts.Count == 0)
            return;

        var query = EntityQueryEnumerator<CLFMemberComponent, ActorComponent>();
        while (query.MoveNext(out var uid, out _, out var actor))
        {
            if (TryResolveContents(uid, loadouts, out var contents))
                GrantPackage(uid, actor.PlayerSession, contents);
        }
    }

    // Finds the loadout whose Role matches the member's job id.
    private bool TryResolveContents(EntityUid member, List<FactionRoleLoadout> loadouts, out List<EntProtoId> contents)
    {
        contents = new List<EntProtoId>();

        if (!_mind.TryGetMind(member, out var mindId, out _))
            return false;

        if (!_job.MindTryGetJobId(mindId, out var job) || job is not { } jobId)
            return false;

        foreach (var loadout in loadouts)
        {
            if (loadout.Contents.Count > 0 &&
                string.Equals(loadout.Role, jobId.Id, StringComparison.OrdinalIgnoreCase))
            {
                contents = loadout.Contents;
                return true;
            }
        }

        return false;
    }

    private void GrantPackage(EntityUid member, ICommonSession session, List<EntProtoId> contents)
    {
        var application = EnsureComp<InsurgencyFactionApplicationComponent>(member);
        if (application.PackageGranted)
            return;

        application.PackageGranted = true;

        var package = Spawn(PackagePrototype, Transform(member).Coordinates);
        var comp = EnsureComp<APackageComponent>(package);
        comp.Contents = new List<EntProtoId>(contents);

        // In hand if there is a free one, otherwise it lands at their feet.
        _hands.PickupOrDrop(member, package);

        _audio.PlayGlobal(GrantSound, session);
        _popup.PopupEntity(Loc.GetString("insfor-a-package-received"), member, member, PopupType.Small);
    }

    private void OnUse(Entity<APackageComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        var coords = Transform(args.User).Coordinates;
        foreach (var proto in ent.Comp.Contents)
        {
            var item = Spawn(proto, coords);
            _hands.PickupOrDrop(args.User, item);
        }

        args.Handled = true;
        QueueDel(ent.Owner);
    }
}
