using System.Linq;
using System.Numerics;
using Content.Server.Administration.Managers;
using Content.Server.Administration.Logs;
using Content.Server.EUI;
using Content.Shared.Administration;
using Content.Shared.Database;
using Content.Shared.CMU14.Expeditions;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Verbs;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.CMU14.Expeditions;

public sealed partial class CMUExpeditionAgentSystem
{
    [Dependency] private IAdminManager _squadAdmin = default!;
    [Dependency] private IAdminLogManager _squadLog = default!;
    [Dependency] private EuiManager _squadEui = default!;

    private void InitializeSquadPanel() => SubscribeLocalEvent<CMUExpeditionAgentComponent, GetVerbsEvent<Verb>>(OnSquadPanelVerb);

    private void OnSquadPanelVerb(Entity<CMUExpeditionAgentComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!TryComp<ActorComponent>(args.User, out var actor) || !_squadAdmin.HasAdminFlag(actor.PlayerSession, AdminFlags.Admin))
            return;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString("cmu-squads-title"),
            Act = () => _squadEui.OpenEui(new CMUSquadPanelEui(ent.Comp.SquadRoot is { } root ? GetNetEntity(root) : null), actor.PlayerSession),
        });
    }

    public CMUSquadPanelState SquadPanelState(NetEntity? selected, string status)
    {
        UpdateSquadPlans(_timing.CurTime);
        if (selected == null && _squadPlans.Count > 0)
            selected = GetNetEntity(_squadPlans.First().Key);
        var state = new CMUSquadPanelState
        {
            Selected = selected, Status = status,
            Variants = SquadPresets.Keys.Order().ToList(), Outfits = OutfitNames.Order().ToList(),
            Doctrines = DoctrineNames.Order().ToList(),
            Factions = ProtoMan.EnumeratePrototypes<NpcFactionPrototype>().Select(proto => proto.ID).Order().ToList(),
        };
        foreach (var (root, plan) in _squadPlans.OrderBy(pair => pair.Key.Id).Take(100))
        {
            var member = plan.Members.FirstOrDefault(uid => Exists(uid) && HasComp<CMUExpeditionAgentComponent>(uid));
            if (!Exists(member) || !Exists(root))
                continue;
            var agent = Comp<CMUExpeditionAgentComponent>(member);
            state.Squads.Add(new CMUSquadSummary(GetNetEntity(root), Loc.GetString("cmu-squads-summary",
                ("squad", agent.Squad), ("count", plan.Members.Count), ("map", Transform(member).MapID), ("phase", plan.Phase))));
            if (selected != GetNetEntity(root))
                continue;
            state.Friendlies = agent.FriendlyFactions.Count == 0 ? "default" : string.Join(",", agent.FriendlyFactions);
            state.Targets = agent.TargetFactions.Count == 0 ? "default" : string.Join(",", agent.TargetFactions);
            foreach (var uid in plan.Members.Where(uid => Exists(uid) && HasComp<CMUExpeditionAgentComponent>(uid)).Take(32))
            {
                var a = Comp<CMUExpeditionAgentComponent>(uid);
                Vector2? Position(EntityCoordinates? point) => point is { } p && Exists(p.EntityId) && Transform(p.EntityId).MapID == Transform(uid).MapID
                    ? _transform.ToMapCoordinates(p).Position : null;
                var ammo = _guns.TryGetGun(uid, out var gun) ? WeaponAmmo(gun) : 0;
                var nativeDelay = gun.Owner.IsValid() ? Math.Max(0, (gun.Comp.NextFire - _timing.CurTime).TotalSeconds) : 0;
                var condition = HasComp<ActorComponent>(uid) ? "player-controlled" : _mobs.IsDead(uid) ? "dead" : _mobs.IsCritical(uid) ? "critical" : "active";
                var detail = $"{condition} | Map {Transform(uid).MapID}\n{a.Duty} / {a.Doctrine} / {a.SquadPhase}\n{a.State}: {a.DecisionOwner}\n" +
                    $"Fire: {a.LastFireCheck} | Weapon: {a.WeaponDecision}\n" +
                    $"Native fire wait: {nativeDelay:F2}s | AI aim wait: {Math.Max(0, (a.FireAt - _timing.CurTime).TotalSeconds):F2}s\n" +
                    $"Damage: {a.LastDamage:F0} | Ammo: {ammo} | Stress: {a.Stress:F2}\n" +
                    $"Squad: {a.SquadDecision} | Movement: {a.TrafficDecision} | Door: {a.DoorDecision}\n" +
                    $"Sight: {a.VisionDecision} | Supplies: {a.SupplyDecision}\n" +
                    $"Friendly: {string.Join(",", a.FriendlyFactions)} | Targets: {string.Join(",", a.TargetFactions)}\n" +
                    $"Search: {a.LastRouteMilliseconds:F2} ms / {a.LastRouteCells} cells\n" + string.Join("\n", a.DecisionHistory);
                detail += $"\nSquad update: {_squadPlanMilliseconds:F2} ms | Portal: {a.TravelPortal} | Goal: {a.TravelGoal}";
                state.Members.Add(new CMUSquadMemberView(GetNetEntity(uid), MetaData(uid).EntityName,
                    (int) Transform(uid).MapID, _transform.GetWorldPosition(uid), Position(a.LastSeen),
                    Position(a.SpacingDestination ?? a.CoverDestination ?? a.OrderedDestination), Position(a.CoverAnchor),
                    a.OrderRoute.Concat(a.Route).Take(48).Select(point => Position(point)).Where(point => point != null).Select(point => point!.Value).ToList(),
                    a.BadCover.Where(entry => entry.Until > _timing.CurTime).Select(entry => Position(entry.Point))
                        .Where(point => point != null).Select(point => point!.Value).ToList(), detail));
            }
        }
        return state;
    }

    public string ControlSquad(ICommonSession player, CMUSquadPanelMessage message, ref NetEntity? selected)
    {
        if (!_squadAdmin.HasAdminFlag(player, AdminFlags.Admin) || !Enum.IsDefined(message.Action) ||
            message.Value == null || message.Variant == null || message.Outfit == null || message.Doctrine == null || message.Facing == null ||
            message.Value.Length > 256 || message.Variant.Length > 32 || message.Outfit.Length > 32 || message.Doctrine.Length > 32 ||
            !float.IsFinite(message.X) || !float.IsFinite(message.Y))
            return Loc.GetString("cmu-squads-invalid");
        if (message.Action == CMUSquadPanelAction.Refresh)
            return "";
        if (message.Action == CMUSquadPanelAction.Select)
        {
            if (message.Root is { } choice && _squadPlans.ContainsKey(GetEntity(choice)))
                selected = choice;
            return "";
        }
        EntityCoordinates point = default;
        if (message.Here && player.AttachedEntity is { } observer)
            point = Transform(observer).Coordinates;
        else if (!message.Here && _maps.MapExists(new MapId(message.Map)))
            point = new EntityCoordinates(_maps.GetMap(new MapId(message.Map)), new Vector2(message.X, message.Y));
        if (message.Action == CMUSquadPanelAction.Spawn)
        {
            if (point == default || !Doctrines.ContainsKey(message.Doctrine))
                return Loc.GetString("cmu-squads-invalid");
            var spawned = SpawnSquad(point, message.Count, message.Variant, out var id, message.Outfit);
            if (spawned == 0)
                return Loc.GetString("cmu-expedition-ai-no-space");
            _nextSquadPlan = TimeSpan.Zero;
            UpdateSquadPlans(_timing.CurTime);
            var group = _squadPlans.LastOrDefault(pair => pair.Value.Members.Any(member =>
                Comp<CMUExpeditionAgentComponent>(member).Squad == id && Transform(member).MapID == Transform(point.EntityId).MapID));
            if (group.Value != null)
            {
                selected = GetNetEntity(group.Key);
                foreach (var member in group.Value.Members)
                    SetDoctrine(member, message.Doctrine);
            }
            _squadLog.Add(LogType.AdminCommands, LogImpact.Medium, $"{player.Name} spawned {spawned} expedition agents with squad panel");
            return Loc.GetString("cmu-squads-applied", ("count", spawned));
        }
        if (message.Root is not { } networkRoot || !_squadPlans.TryGetValue(GetEntity(networkRoot), out var plan))
            return Loc.GetString("cmu-expedition-orders-none");
        var factions = message.Value == "default" ? Array.Empty<string>() :
            message.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (message.Action is CMUSquadPanelAction.Friendly or CMUSquadPanelAction.Target &&
            factions.Any(faction => !ProtoMan.HasIndex<NpcFactionPrototype>(faction)))
            return Loc.GetString("cmu-squads-invalid");
        Direction? facing = null;
        if (message.Facing != "auto")
        {
            if (!Enum.TryParse<Direction>(message.Facing, true, out var direction) ||
                direction is not (Direction.North or Direction.South or Direction.East or Direction.West))
                return Loc.GetString("cmu-squads-invalid");
            facing = direction;
        }
        var reserved = new List<EntityCoordinates>();
        var count = 0;
        foreach (var member in plan.Members.ToArray())
        {
            if (!CanOrderSquadMember(member))
                continue;
            var agent = Comp<CMUExpeditionAgentComponent>(member);
            switch (message.Action)
            {
                case CMUSquadPanelAction.Move:
                case CMUSquadPanelAction.Guard:
                case CMUSquadPanelAction.PatrolAdd:
                    if (point == default || !OrderSquadPoint(member, point,
                            message.Action == CMUSquadPanelAction.Move ? "move" : message.Action == CMUSquadPanelAction.Guard ? "guard" : "patrol-add", reserved, facing))
                        continue;
                    break;
                case CMUSquadPanelAction.PatrolStart:
                case CMUSquadPanelAction.PatrolStop:
                    if (!OrderPatrol(member, agent, message.Action == CMUSquadPanelAction.PatrolStart ? "patrol-start" : "patrol-stop"))
                        continue;
                    break;
                case CMUSquadPanelAction.Doctrine:
                    if (!SetDoctrine(member, message.Value))
                        continue;
                    break;
                case CMUSquadPanelAction.Friendly:
                case CMUSquadPanelAction.Target:
                    ResetOrders(member, agent);
                    var set = message.Action == CMUSquadPanelAction.Friendly ? agent.FriendlyFactions : agent.TargetFactions;
                    set.Clear();
                    set.UnionWith(factions);
                    break;
                case CMUSquadPanelAction.Regroup:
                    if (plan.Leader is not { } leader || !OrderSquadPoint(member, Transform(leader).Coordinates, "move", reserved))
                        continue;
                    break;
                case CMUSquadPanelAction.Hold:
                case CMUSquadPanelAction.Resupply:
                    ResetOrders(member, agent);
                    agent.OrderedDestination = null;
                    agent.Patrolling = false;
                    agent.Home = Transform(member).Coordinates;
                    agent.NextSupplyRun = TimeSpan.Zero;
                    agent.NextScavenge = TimeSpan.Zero;
                    break;
                default:
                    continue;
            }
            Decision(agent, "admin-order", message.Action.ToString());
            count++;
        }
        _squadLog.Add(LogType.AdminCommands, LogImpact.Medium,
            $"{player.Name} used expedition squad action {message.Action} on {count} agents ({networkRoot})");
        return Loc.GetString("cmu-squads-applied", ("count", count));
    }
}
