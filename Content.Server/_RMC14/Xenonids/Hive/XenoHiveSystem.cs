using Content.Server._RMC14.Announce;
using Content.Server.GameTicking;
using Content.Server.Popups;
using Content.Shared._RMC14.Admin;
using Content.Shared._RMC14.CCVar;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.Marines;
using Content.Shared._RMC14.Synth;
using Content.Shared._RMC14.Xenonids;
using Content.Shared._RMC14.Xenonids.Hive;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using System.Data;
using Content.Server.Destructible;
using Content.Shared._RMC14.Explosion;
using Content.Shared._RMC14.Sprite;
using Content.Shared._RMC14.Xenonids.Construction;
using Content.Shared.Coordinates;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Robust.Shared.Serialization.Manager;
using IConfigurationManager = Robust.Shared.Configuration.IConfigurationManager;
using Content.Shared._CMU14.Xenomorphs.Pathogen;

namespace Content.Server._RMC14.Xenonids.Hive;

public sealed partial class XenoHiveSystem : SharedXenoHiveSystem
{
    [Dependency] private AudioSystem _audio = default!;
    [Dependency] private IComponentFactory _compFactory = default!;
    [Dependency] private IConfigurationManager _config = default!;
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private MetaDataSystem _metaData = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private SharedCMChatSystem _rmcChat = default!;
    [Dependency] private SharedRMCSpriteSystem _rmcSprite = default!;
    [Dependency] private ISerializationManager _serialization = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private XenoAnnounceSystem _xenoAnnounce = default!;

    private readonly List<string> _announce = [];
    private readonly EntProtoId _defaultHive = "CMXenoHive";

    private TimeSpan _lateJoinsPerBurrowedLarvaEarlyThreshold;
    private float _lateJoinsPerBurrowedLarvaEarly;
    private float _lateJoinsPerBurrowedLarva;

    private const int InvinciblePer = 10;
    private readonly List<Entity<InvincibleHiveStructureComponent>> _invincibles = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);

        SubscribeLocalEvent<HijackBurrowedSurgeComponent, ComponentStartup>(OnBurrowedSurgeStartup);
        SubscribeLocalEvent<HijackBurrowedSurgeComponent, ComponentShutdown>(OnBurrowedSurgeShutdown);

        SubscribeLocalEvent<InvincibleHiveStructureComponent, MapInitEvent>(OnInvincibleMapInit);
        SubscribeLocalEvent<CMUPathogenHiveMemberComponent, MapInitEvent>(OnPathogenSpawn);

        Subs.CVar(_config,
            RMCCVars.RMCLateJoinsPerBurrowedLarvaEarlyThresholdMinutes,
            v => _lateJoinsPerBurrowedLarvaEarlyThreshold = TimeSpan.FromMinutes(v),
            true);
        Subs.CVar(_config, RMCCVars.RMCLateJoinsPerBurrowedLarvaEarly, v => _lateJoinsPerBurrowedLarvaEarly = v, true);
        Subs.CVar(_config, RMCCVars.RMCLateJoinsPerBurrowedLarva, v => _lateJoinsPerBurrowedLarva = v, true);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent ev)
    {
        if (!ev.LateJoin || !HasComp<MarineComponent>(ev.Mob))
            return;

        if (HasComp<RMCAdminSpawnedComponent>(ev.Mob))
            return;

        if (ev.JobId is not { } jobId ||
            !_prototypes.TryIndex(jobId, out JobPrototype? job) ||
            job.RoleWeight < 0)
        {
            return;
        }

        var time = _timing.CurTime;
        var lateJoinsPer = time < _lateJoinsPerBurrowedLarvaEarlyThreshold
            ? _lateJoinsPerBurrowedLarvaEarly
            : _lateJoinsPerBurrowedLarva;

        var hives = EntityQueryEnumerator<HiveComponent>();
        while (hives.MoveNext(out var uid, out var hive))
        {
            if (!hive.LateJoinGainLarva)
                continue;

            hive.LateJoinMarines += job.RoleWeight;
            if (hive.LateJoinMarines < lateJoinsPer)
                continue;

            hive.LateJoinMarines -= lateJoinsPer;
            ChangeBurrowedLarva((uid, hive), 1);
        }
    }

    private void OnBurrowedSurgeStartup(Entity<HijackBurrowedSurgeComponent> hive, ref ComponentStartup args)
    {
        _xenoAnnounce.AnnounceToHive(EntityUid.Invalid, hive, Loc.GetString("rmc-xeno-burrowed-surge-start"));
    }

    private void OnBurrowedSurgeShutdown(Entity<HijackBurrowedSurgeComponent> hive, ref ComponentShutdown args)
    {
        _xenoAnnounce.AnnounceToHive(EntityUid.Invalid, hive, Loc.GetString("rmc-xeno-burrowed-surge-end"));
    }

    private void OnInvincibleMapInit(Entity<InvincibleHiveStructureComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.ReplaceAt = _timing.CurTime + ent.Comp.Duration;
        Dirty(ent);

        RemComp<DamageableComponent>(ent);
        RemComp<DestructibleComponent>(ent);
        RemComp<RMCWallExplosionDeletableComponent>(ent);
        RemComp<XenoConstructionRequiresSupportComponent>(ent);

        if (ent.Comp.BlockerId != null)
            ent.Comp.Blocker = Spawn(ent.Comp.BlockerId, ent.Owner.ToCoordinates());

        _rmcSprite.SetColor(ent.Owner, ent.Comp.Color);
    }

    private void UpdateInvincible()
    {
        if (_invincibles.Count == 0)
        {
            var time = _timing.CurTime;
            var query = EntityQueryEnumerator<InvincibleHiveStructureComponent>();
            while (query.MoveNext(out var uid, out var comp))
            {
                if (time < comp.ReplaceAt)
                    continue;

                _invincibles.Add((uid, comp));
            }
        }

        try
        {
            var i = 0;
            for (var j = _invincibles.Count - 1; j >= 0; j--)
            {
                if (i++ > InvinciblePer)
                    break;

                var ent = _invincibles[j];
                _invincibles.RemoveAt(j);

                if (TerminatingOrDeleted(ent))
                    continue;

                RemCompDeferred<InvincibleHiveStructureComponent>(ent);
                QueueDel(ent.Comp.Blocker);

                _rmcSprite.SetColor(ent.Owner, Color.White);

                if (!_prototypes.TryIndex(ent.Comp.Replace, out var replace))
                    continue;

                _metaData.SetEntityName(ent, replace.Name);

                if (replace.TryComp(out DamageableComponent? damageable, _compFactory))
                    AddComp(ent, _serialization.CreateCopy(damageable, notNullableOverride: true), true);

                if (replace.TryComp(out DestructibleComponent? destructible, _compFactory))
                    AddComp(ent, _serialization.CreateCopy(destructible, notNullableOverride: true), true);

                if (replace.TryComp(out RMCWallExplosionDeletableComponent? wallDeletable, _compFactory))
                    AddComp(ent, _serialization.CreateCopy(wallDeletable, notNullableOverride: true), true);

                if (replace.TryComp(out XenoConstructionRequiresSupportComponent? requiresSupport, _compFactory))
                    AddComp(ent, _serialization.CreateCopy(requiresSupport, notNullableOverride: true), true);
            }
        }
        catch (Exception e)
        {
            Log.Error($"Error processing {nameof(InvincibleHiveStructureComponent)}:\n{e}");

            // Clear on exceptions so we aren't stuck processing the same broken entity
            _invincibles.Clear();
        }
    }

    public override void Update(float frameTime)
    {
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        UpdateHives();
        UpdateBurrowedSurge();
        UpdateInvincible();
    }

    /// <summary>
    /// Create a new hive with a name.
    /// </summary> CreateHive() - CMU14
    public EntityUid CreateHive(string name, EntProtoId? proto = null)
    {
        var ent = Spawn(proto ?? _defaultHive);
        _metaData.SetEntityName(ent, name);
        return ent;
    }

    public void EvoScreech(HiveComponent hive) // CMU14 method
    {
        if (hive.CurrentQueen is not { } queen)
            return;

        var map = _transform.GetMapId(queen);
        var mapFilter = Filter.BroadcastMap(map);

        foreach (var session in mapFilter.Recipients)
        {
            if (session.AttachedEntity is not { } recipient)
                continue;

            if (HasComp<XenoComponent>(recipient))
                continue;

            if (_auRoundSystem.SelectedThreat?.hiveevolution == true)
            {
                var popupText = Loc.GetString(HasComp<SynthComponent>(recipient)
                    ? "rmc-hive-supports-castes-synth"
                    : "rmc-hive-supports-castes-human");

                popupText = $"[bold][font size=24][color=red]{popupText}[/color][/font][/bold]";

                _audio.PlayEntity(hive.MarineAnnounceSound, recipient, recipient);
                _rmcChat.ChatMessageToOne(ChatChannel.Radio, popupText, popupText, default, false, session.Channel);
            }
        }
    }

    // private void UpdateHives() { } CMU14
    // private void UpdateBurrowedSurge() { } CMU14

    /// <summary>
    /// When CMUPathogenHive itself finishes MapInit, retroactively assign any Pathogen members
    /// that spawned before the hive entity existed (e.g. map-placed entities).
    /// </summary> OnPathogenSpawn() - CMU14
    private void OnPathogenSpawn(Entity<CMUPathogenHiveMemberComponent> ent, ref MapInitEvent args)
        => TryAssignPathogenHive(ent.Owner);

    private void TryAssignPathogenHive(EntityUid uid) // CMU14 method
    {
        if (TerminatingOrDeleted(uid))
            return;

        var hives = EntityQueryEnumerator<HiveComponent, MetaDataComponent>();
        while (hives.MoveNext(out var hiveUid, out _, out var meta))
        {
            if (meta.EntityPrototype?.ID != "CMUPathogenHive")
                continue;

            Log.Debug($"TryAssignPathogenHive: assigning {ToPrettyString(uid)} to Pathogen hive {ToPrettyString(hiveUid)}");
            SetHive(uid, hiveUid);
            return;
        }

        Log.Debug($"TryAssignPathogenHive: CMUPathogenHive not found for {ToPrettyString(uid)}, retrying next tick");
        Timer.Spawn(0, () => TryAssignPathogenHive(uid)); // TODO: fix this race condition proper
    }
}
