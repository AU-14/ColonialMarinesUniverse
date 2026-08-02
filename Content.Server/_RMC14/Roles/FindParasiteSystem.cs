using Content.Server._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Areas;
using Content.Shared._RMC14.Roles.FindParasite;
using Content.Shared._RMC14.Xenonids.Construction.EggMorpher;
using Content.Shared._RMC14.Xenonids.Egg;
using Content.Shared._RMC14.Xenonids.Parasite;
using Content.Shared._RMC14.Xenonids.Projectile.Parasite;
using Content.Shared.Coordinates;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Server._RMC14.Roles;

public sealed partial class FindParasiteSystem : EntitySystem
{
    private static readonly TimeSpan UiRefreshInterval = TimeSpan.FromSeconds(1);

    [Dependency] private AreaSystem _areas = default!;
    [Dependency] private EntityManager _entities = default!;
    [Dependency] private MobStateSystem _mob = default!;
    [Dependency] private XenoEggRoleSystem _parasiteRole = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;

    private TimeSpan _nextUiRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FindParasiteComponent, FindParasiteActionEvent>(FindParasites);

        SubscribeLocalEvent<FindParasiteComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<FindParasiteComponent, FollowParasiteSpawnerMessage>(FollowParasiteSpawner);
        SubscribeLocalEvent<FindParasiteComponent, TakeParasiteRoleMessage>(TakeParasiteRole);
    }

    public override void Update(float frameTime)
    {
        if (_timing.CurTime < _nextUiRefresh)
            return;

        _nextUiRefresh = _timing.CurTime + UiRefreshInterval;

        var query = EntityQueryEnumerator<FindParasiteComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (_ui.IsUiOpen(uid, XenoFindParasiteUI.Key))
                UpdateUi((uid, component));
        }
    }

    private void FindParasites(Entity<FindParasiteComponent> parasiteFinderEnt, ref FindParasiteActionEvent args)
    {
        if (args.Handled || !_parasiteRole.UserCheck(parasiteFinderEnt.Owner))
        {
            return;
        }

        _ui.OpenUi(parasiteFinderEnt.Owner, XenoFindParasiteUI.Key, parasiteFinderEnt);
        args.Handled = true;
    }

    private void OnUiOpened(Entity<FindParasiteComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<FindParasiteComponent> parasiteFinderEnt)
    {
        var ent = parasiteFinderEnt.Owner;
        var uiState = new FindParasiteUIState();

        var eggs = EntityQueryEnumerator<XenoEggComponent>();
        var eggMorphers = EntityQueryEnumerator<EggMorpherComponent>();
        var parasiteThrowers = EntityQueryEnumerator<XenoParasiteThrowerComponent>();
        var parasites = EntityQueryEnumerator<ParasiteAIComponent>();

        var spawners = new List<NetEntity>();
        while (eggs.MoveNext(out var eggEnt, out var egg))
        {
            if (egg.State != XenoEggState.Grown || (TryComp<XenoFragileEggComponent>(eggEnt, out var fragile) && fragile.SustainedBy != null))
            {
                continue;
            }

            var netEnt = _entities.GetNetEntity(eggEnt);
            spawners.Add(netEnt);
        }

        while (eggMorphers.MoveNext(out var eggMorpherEnt, out var eggMorpherComp))
        {
            if (eggMorpherComp.CurParasites < eggMorpherComp.ReservedParasites ||
                eggMorpherComp.CurParasites <= 0)
            {
                continue;
            }
            spawners.Add(_entities.GetNetEntity(eggMorpherEnt));
        }

        while (parasiteThrowers.MoveNext(out var throwerEnt, out var parasiteThrower))
        {
            if (parasiteThrower.CurParasites <= parasiteThrower.ReservedParasites ||
                parasiteThrower.CurParasites == 0 ||
                _mob.IsDead(throwerEnt))
            {
                continue;
            }
            spawners.Add(_entities.GetNetEntity(throwerEnt));
        }

        while (parasiteThrowers.MoveNext(out var throwerEnt, out var parasiteThrower))
        {
            if (parasiteThrower.CurParasites <= parasiteThrower.ReservedParasites ||
                parasiteThrower.CurParasites == 0 ||
                _mob.IsDead(throwerEnt))
            {
                continue;
            }
            spawners.Add(_entities.GetNetEntity(throwerEnt));
        }


        while (parasites.MoveNext(out var paraEnt, out var parasite))
        {
            if (!_mob.IsAlive(paraEnt))
            {
                continue;
            }
            spawners.Add(_entities.GetNetEntity(paraEnt));
        }

        foreach (var spawner in spawners)
        {
            var spawnerEnt = _entities.GetEntity(spawner);
            var name = MetaData(spawnerEnt).EntityName;
            var areaName = Loc.GetString("xeno-ui-default-area-name");
            if (_areas.TryGetArea(spawnerEnt.ToCoordinates(), out var area, out _))
                areaName = Name(area.Value);

            name = Loc.GetString("xeno-ui-find-parasite-item",
                    ("itemName", name), ("areaName", areaName));

            uiState.ActiveParasiteSpawners.Add(new(name, spawner));
        }
        _ui.SetUiState(ent, XenoFindParasiteUI.Key, uiState);

    }
    private void FollowParasiteSpawner(Entity<FindParasiteComponent> parasiteFinderEnt, ref FollowParasiteSpawnerMessage args)
    {
        var netEnt = args.Entity;
        var ent = _entities.GetEntity(netEnt);

        var netSpawner = args.Spawner;
        var spawner = _entities.GetEntity(netSpawner);

        var ev = new GetVerbsEvent<AlternativeVerb>(ent, spawner, null, null, true, true, true, new());
        RaiseLocalEvent(ev);

        foreach (var action in ev.Verbs)
        {
            if (action.Text != Loc.GetString("verb-follow-text") || action.Act is null)
            {
                continue;
            }
            action.Act.Invoke();
            break;
        }
    }

    private void TakeParasiteRole(Entity<FindParasiteComponent> parasiteFinderEnt, ref TakeParasiteRoleMessage args)
    {
        var netEnt = args.Entity;
        var ent = _entities.GetEntity(netEnt);

        var netSpawner = args.Spawner;
        var spawner = _entities.GetEntity(netSpawner);

        var ev = new GetVerbsEvent<ActivationVerb>(ent, spawner, null, null, false, false, false, new());
        RaiseLocalEvent(spawner, ev);

        foreach (var action in ev.Verbs)
        {
            if (action.Text != Loc.GetString("rmc-xeno-egg-ghost-verb") || action.Act is null)
            {
                continue;
            }
            action.Act.Invoke();
            break;
        }
    }
}
