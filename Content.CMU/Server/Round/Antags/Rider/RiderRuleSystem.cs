using Content.Server.Administration.Logs;
using Content.Server.Antag;
using Content.Server.Chat.Systems;
using Content.Server.Mind;
using Content.Shared._RMC14.Chat;
using Content.Shared._RMC14.CrashLand;
using Content.Shared._RMC14.Rules;
using Content.Shared._RMC14.Vents;
using Content.Shared.CMU14.Round.Antags.Rider;
using Content.Shared.Chat;
using Content.Shared.Database;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Round.Antags.Rider;

/// <summary>
/// Marker for the Rider game rule entity.
/// </summary>
[RegisterComponent]
public sealed partial class RiderRuleComponent : Component;

/// <summary>
/// Turns the selected player's crew entity into the hatchling: spawns it at an
/// isolated vent node, moves the mind across, and quietly removes the colonist
/// body they spawned with. The station records keep a missing person.
/// </summary>
public sealed partial class RiderRuleSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    [Dependency] private readonly SharedCMChatSystem _chat = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RMCPlanetSystem _rmcPlanet = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedCrashLandSystem _crashLand = default!;

    private const string HatchlingPrototype = "CMURiderHatchling";

    // Puppeteer targets: real objective items that exist in the world. Whether
    // one spawns this round is the objective's luck, same as any steal objective.
    private static readonly EntProtoId[] PuppeteerPool =
    {
        "AU14ObjectiveItemWYLaptop",
        "AU14ObjectiveItemBlackboxRecorder",
        "AU14ObjectiveItemAIProcessor",
        "AU14ObjectiveItemNuclearLaunchCodes",
    };

    public override void Initialize()
    {
        SubscribeLocalEvent<RiderRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
    }

    private void OnAntagSelected(Entity<RiderRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Def.ID != "Rider")
            return;

        var crew = args.EntityUid;
        var spawnCoords = Transform(crew).Coordinates;

        var vent = FindIsolatedPlanetVent();
        if (vent is { } ventUid)
            spawnCoords = Transform(ventUid).Coordinates;
        else if (_crashLand.TryGetCrashLandLocation(out var crash))
            spawnCoords = crash;

        var hatchling = Spawn(HatchlingPrototype, spawnCoords);

        if (vent is { } pipe
            && TryComp(pipe, out VentCrawlableComponent? pipeComp))
        {
            var pipeContainer = _container.EnsureContainer<Container>(pipe, pipeComp.ContainerId);
            if (_container.Insert(hatchling, pipeContainer))
                EnsureComp<VentCrawlingComponent>(hatchling);
        }

        if (!_mind.TryGetMind(crew, out var mindId, out _))
        {
            Del(hatchling);
            return;
        }

        _mind.TransferTo(mindId, hatchling);
        Del(crew);

        RollFlavor(hatchling, args.Session);

        _adminLogger.Add(LogType.AntagSelection, LogImpact.High,
            $"Rider antag spawned as hatchling {ToPrettyString(hatchling):rider}; crew body removed");
    }

    /// <summary>
    /// Objective flavor, rolled at selection like every other colony antag and
    /// stated in an extra briefing line (§6).
    /// </summary>
    private void RollFlavor(EntityUid hatchling, Robust.Shared.Player.ICommonSession? session)
    {
        var comp = EnsureComp<RiderComponent>(hatchling);
        var roll = _random.NextFloat();
        comp.Flavor = roll < 0.5f
            ? RiderFlavor.Hitchhiker
            : roll < 0.75f
                ? RiderFlavor.Leapfrog
                : RiderFlavor.Puppeteer;

        string text;
        if (comp.Flavor == RiderFlavor.Puppeteer)
        {
            comp.PuppeteerItem = _random.Pick(PuppeteerPool);
            text = Loc.GetString("rider-flavor-puppeteer",
                ("item", _proto.Index(comp.PuppeteerItem.Value).Name));
        }
        else
        {
            text = Loc.GetString(comp.Flavor == RiderFlavor.Leapfrog
                ? "rider-flavor-leapfrog"
                : "rider-flavor-hitchhiker");
        }

        if (session != null)
            _chat.ChatMessageToOne(ChatChannel.Local, text, text, hatchling, false, session.Channel);
    }

    /// <summary>
    /// Picks the planetside vent with the largest distance to the nearest
    /// player: no witnesses at spawn, and emerging from the vent stays the
    /// player's call. Ship and dropship pipes never qualify.
    /// </summary>
    private EntityUid? FindIsolatedPlanetVent()
    {
        EntityUid? best = null;
        var bestDistance = -1f;

        var ventEnumerator = EntityQueryEnumerator<VentCrawlableComponent, TransformComponent>();
        while (ventEnumerator.MoveNext(out var vent, out var ventXform))
        {
            if (!_rmcPlanet.IsOnPlanet(ventXform))
                continue;

            var minDistance = float.MaxValue;

            var playerEnumerator = EntityQueryEnumerator<ActorComponent, TransformComponent>();
            while (playerEnumerator.MoveNext(out var viewer, out _, out var playerXform))
            {
                if (playerXform.MapID != ventXform.MapID)
                    continue;

                var distance = (playerXform.WorldPosition - ventXform.WorldPosition).Length();
                if (distance < minDistance)
                    minDistance = distance;
            }

            if (minDistance > bestDistance)
            {
                bestDistance = minDistance;
                best = ventXform.Owner;
            }
        }

        return best;
    }
}
