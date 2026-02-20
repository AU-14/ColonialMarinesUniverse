using Content.Server.GameTicking.Events;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Humanoid.Components;
using Content.Server.Spawners.Components;
using Content.Shared._RMC14.Intel.Tech;
using Content.Shared.AU14.Threats;
using Content.Shared.AU14.Util;
using Content.Shared.Humanoid.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._RMC14.Intel.Tech;

public sealed class ServerTechSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly Content.Server.AU14.ThirdParty.AuThirdPartySystem _thirdParty = default!;
    private static readonly EntProtoId CombatTechProto = "RMCRandomHumanoidFoxtrotCombatTech";
    private static readonly EntProtoId FireteamLeaderProto = "RMCRandomHumanoidFoxtrotFireteamLeader";
    private static readonly EntProtoId HospitalCorpsmanProto = "RMCRandomHumanoidFoxtrotHospitalCorpsman";
    private static readonly EntProtoId RiflemanProto = "RMCRandomHumanoidFoxtrotRifleman";
    private static readonly EntProtoId SmartGunOperatorProto = "RMCRandomHumanoidFoxtrotSmartGunOperator";
    private static readonly EntProtoId SquadLeaderProto = "RMCRandomHumanoidFoxtrotSquadLeader";
    private static readonly EntProtoId WeaponsSpecialistProto = "RMCRandomHumanoidFoxtrotWeaponsSpecialist";

    private bool _cryoMarinesPurchased = false;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TechCryoMarinesEvent>(OnTechCryoMarines);
        SubscribeLocalEvent<TechCryoSpecEvent>(OnTechCryoSpec);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStart);
        SubscribeLocalEvent<TechPartySpawnEvent>(OnTechPartySpawn);
    }

    private void OnRoundStart(RoundStartingEvent ev)
    {
        _cryoMarinesPurchased = false;
    }

    private void OnTechCryoMarines(TechCryoMarinesEvent ev)
    {
        // TODO RMC14 this should spawn you as your character but with random name
        SpawnCryo(_cryoMarinesPurchased ? FireteamLeaderProto : SquadLeaderProto, 1);
        SpawnCryo(CombatTechProto, 1);
        SpawnCryo(HospitalCorpsmanProto, 1);
        SpawnCryo(RiflemanProto, 2);

        _cryoMarinesPurchased = true;
    }

    private void OnTechCryoSpec(TechCryoSpecEvent ev)
    {
        SpawnCryo(WeaponsSpecialistProto, 1);
    }

    private void OnTechPartySpawn(TechPartySpawnEvent ev)
    {
        if (string.IsNullOrEmpty(ev.ThirdPartyId))
        {
            Logger.Warning("[ServerTechSystem] Received TechPartySpawnEvent with null/empty ThirdPartyId; ignoring.");
            return;
        }
        TechSystem.ExecuteTechPartySpawn(_proto, ev.ThirdPartyId, proto =>
        {
            if (!_proto.TryIndex(proto.PartySpawn, out var spawnProto))
            {
                Logger.Warning($"[ServerTechSystem] PartySpawn prototype '{proto.PartySpawn}' not found for third party '{proto.ID}'.");
                return;
            }

            // Call the server system to spawn the third party using resolved spawnProto.
            _thirdParty.SpawnThirdParty(proto, spawnProto, false);
        });
    }

    private void SpawnCryo(EntProtoId spawnerId, uint amount)
    {
        if (!_proto.TryIndex(spawnerId, out var spawner) ||
            !spawner.TryGetComponent<RandomHumanoidSpawnerComponent>(out var human, _componentFactory) ||
            human.SettingsPrototypeId is null ||
            !_proto.TryIndex<RandomHumanoidSettingsPrototype>(human.SettingsPrototypeId, out var settings) ||
            settings.Components is null ||
            !settings.Components.TryGetComponent("GhostRole", out var ghostI))
            return;

        var ghost = (GhostRoleComponent)ghostI;

        var spawners = AllEntityQuery<SpawnPointComponent>();
        List<EntityUid> valid = [];
        while (spawners.MoveNext(out var uid, out var comp))
            if (comp.Job == ghost.JobProto)
                valid.Add(uid);

        if (valid.Count == 0)
            return;

        for (var i = 0; i < amount; i++)
        {
            var choice = _random.Pick(valid);
            Spawn(spawnerId, _transform.GetMapCoordinates(choice));
        }
    }


}
