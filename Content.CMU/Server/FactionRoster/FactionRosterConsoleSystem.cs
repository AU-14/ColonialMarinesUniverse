using Content.Server._CMU14.Threats.Rules;
using Content.Server._CMU14.CharacterDescription;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared._CMU14.CharacterDescription;
using Content.Shared._CMU14.FactionRoster;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Robust.Server.GameObjects;

namespace Content.Server._CMU14.FactionRoster;

public sealed class FactionRosterConsoleSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly MindSystem _minds = default!;
    [Dependency] private readonly Robust.Shared.Prototypes.IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly RMCReagentSystem _reagentSystem = default!;

    private const string ColonyFactionId = "AUColonist";
    private const string ClfFactionId = "CLF";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionRosterConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<FactionRosterConsoleComponent, FactionRosterConsoleRefreshBuiMsg>(OnRefresh);
    }

    private void OnUiOpened(Entity<FactionRosterConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnRefresh(Entity<FactionRosterConsoleComponent> ent, ref FactionRosterConsoleRefreshBuiMsg args)
    {
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<FactionRosterConsoleComponent> ent)
    {
        _ui.SetUiState(ent.Owner, FactionRosterConsoleUiKey.Key, new FactionRosterConsoleBuiState(BuildRoster(ent.Comp.Faction.Id)));
    }

    private List<FactionRosterEntry> BuildRoster(string consoleFaction)
    {
        var entries = new List<FactionRosterEntry>();
        var isColonyConsole = string.Equals(consoleFaction, ColonyFactionId, StringComparison.OrdinalIgnoreCase);

        var query = EntityQueryEnumerator<MobStateComponent, NpcFactionMemberComponent>();
        while (query.MoveNext(out var uid, out _, out var faction))
        {
            var isClf = isColonyConsole && ThreatRuleHelper.HasFaction(faction, ClfFactionId);
            if (!ThreatRuleHelper.HasFaction(faction, consoleFaction) && !isClf)
                continue;

            if (!_minds.TryGetMind(uid, out var mindId, out _))
                continue;

            entries.Add(BuildEntry(uid, mindId, isClf));
        }

        entries.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return entries;
    }

    private FactionRosterEntry BuildEntry(EntityUid uid, EntityUid mindId, bool disguiseAsColonist)
    {
        var entry = new FactionRosterEntry
        {
            Name = MetaData(uid).EntityName,
            Job = disguiseAsColonist
                ? Loc.GetString("faction-roster-console-colonist-job")
                : _jobs.MindTryGetJobName(mindId),
        };

        if (TryComp(uid, out CharacterDescriptionComponent? desc))
        {
            entry.ShortExamine = desc.ShortExamine;
            entry.FullDescription = desc.FullDescription;
            entry.MedicalRecord = MedicalRecordBuilder.Build(EntityManager, _reagentSystem, uid, desc);
            entry.CriminalRecord = desc.CriminalRecord;
            entry.GeneralRecord = desc.GeneralRecord;
            entry.Height = desc.Height;
            entry.Weight = desc.Weight;
            entry.Age = desc.Age;
            entry.Build = Loc.GetString($"build-type-{desc.Build.ToString().ToLowerInvariant()}");

            if (!disguiseAsColonist)
            {
                if (desc.Origin is { } origin && _prototypeManager.TryIndex(origin, out var originProto))
                    entry.Origin = Loc.GetString(originProto.Name);

                if (desc.Allegiance is { } allegiance && _prototypeManager.TryIndex(allegiance, out var allegianceProto))
                    entry.Allegiance = Loc.GetString(allegianceProto.Name);
            }
        }

        if (TryComp(uid, out HumanoidAppearanceComponent? humanoid))
        {
            entry.SkinToneName = NamedColorHelper.NearestColorName(humanoid.SkinColor);
            entry.EyeColorName = NamedColorHelper.NearestColorName(humanoid.EyeColor);

            if (humanoid.MarkingSet.TryGetCategory(MarkingCategories.Hair, out var hairMarkings) &&
                hairMarkings.Count > 0 && hairMarkings[0].MarkingColors.Count > 0)
            {
                entry.HairColorName = NamedColorHelper.NearestColorName(hairMarkings[0].MarkingColors[0]);
            }
        }

        return entry;
    }
}
