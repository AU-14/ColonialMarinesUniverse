using Content.Server.CMU14.Threats.Rules;
using Content.Server.CMU14.CharacterDescription;
using Content.Server.Humanoid;
using Content.Server.Mind;
using Content.Server.Roles.Jobs;
using Content.Shared._RMC14.Chemistry.Reagent;
using Content.Shared.CMU14.CharacterDescription;
using Content.Shared.CMU14.FactionRoster;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Components;
using Content.Shared.Paper;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server.CMU14.FactionRoster;

public sealed partial class FactionRosterConsoleSystem : EntitySystem
{
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private JobSystem _jobs = default!;
    [Dependency] private MindSystem _minds = default!;
    [Dependency] private Robust.Shared.Prototypes.IPrototypeManager _prototypeManager = default!;
    [Dependency] private RMCReagentSystem _reagentSystem = default!;
    [Dependency] private HumanoidOrganAppearanceSystem _humanoidAppearance = default!;
    [Dependency] private PaperSystem _paper = default!;
    [Dependency] private MetaDataSystem _meta = default!;

    private const string ColonyFactionId = "AUColonist";
    private const string ClfFactionId = "CLF";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FactionRosterConsoleComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<FactionRosterConsoleComponent, FactionRosterConsoleRefreshBuiMsg>(OnRefresh);
        SubscribeLocalEvent<FactionRosterConsoleComponent, FactionRosterConsolePrintBuiMsg>(OnPrint);
    }

    private void OnUiOpened(Entity<FactionRosterConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnRefresh(Entity<FactionRosterConsoleComponent> ent, ref FactionRosterConsoleRefreshBuiMsg args)
    {
        UpdateUi(ent);
    }

    /// <summary>Prints someone's personnel file on paper at the console, if they're still on its roster.</summary>
    private void OnPrint(Entity<FactionRosterConsoleComponent> ent, ref FactionRosterConsolePrintBuiMsg args)
    {
        var name = args.Name;
        var entry = BuildRoster(ent.Comp.Faction.Id).Find(e => e.Name == name);
        if (entry == null)
            return;

        var paper = Spawn(PersonnelPaper, Transform(ent).Coordinates);
        _meta.SetEntityName(paper, Loc.GetString("faction-roster-print-name", ("name", entry.Name)));
        _paper.SetContent(paper, BuildPrintout(entry));
    }

    private const string PersonnelPaper = "CMPaper";

    private string BuildPrintout(FactionRosterEntry entry)
    {
        string Or(string? value) => string.IsNullOrWhiteSpace(value)
            ? Loc.GetString("faction-roster-dossier-not-recorded")
            : FormattedMessage.EscapeText(value.Trim());

        return Loc.GetString("faction-roster-print-content",
            ("name", FormattedMessage.EscapeText(entry.Name)),
            ("job", FormattedMessage.EscapeText(entry.Job)),
            ("age", entry.Age > 0 ? entry.Age.ToString() : Loc.GetString("faction-roster-dossier-not-recorded")),
            ("height", Or(entry.Height)),
            ("weight", entry.Weight > 0 ? entry.Weight.ToString() : Loc.GetString("faction-roster-dossier-not-recorded")),
            ("build", Or(entry.Build)),
            ("skin", Or(entry.SkinToneName)),
            ("hair", Or(entry.HairColorName)),
            ("eyes", Or(entry.EyeColorName)),
            ("allegiance", Or(entry.Allegiance)),
            ("origin", Or(entry.Origin)),
            ("religion", Or(entry.Religion)),
            ("description", Or(entry.FullDescription)),
            ("medical", Or(entry.MedicalRecord)),
            ("criminal", Or(entry.CriminalRecord)),
            ("general", Or(entry.GeneralRecord)));
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

    /// <summary>The dossier for <paramref name="uid"/>; also used by the criminal records console's personnel file.</summary>
    public FactionRosterEntry BuildEntry(EntityUid uid, EntityUid mindId, bool disguiseAsColonist)
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

                if (!desc.HideMetaInformation && _prototypeManager.TryIndex(desc.Religion, out var religionProto))
                    entry.Religion = Loc.GetString(religionProto.Name);

                if (desc.Allegiance is { } allegiance && _prototypeManager.TryIndex(allegiance, out var allegianceProto))
                    entry.Allegiance = Loc.GetString(allegianceProto.Name);
            }
        }

        if (_humanoidAppearance.TryGetColors(uid, out var skinColor, out var eyeColor))
        {
            entry.SkinToneName = NamedColorHelper.NearestColorName(skinColor);
            entry.EyeColorName = NamedColorHelper.NearestColorName(eyeColor);

            if (_humanoidAppearance.TryGetMarkings(uid, HumanoidVisualLayers.Hair, out _, out _, out var hairMarkings) &&
                hairMarkings.Count > 0 && hairMarkings[0].MarkingColors.Count > 0)
            {
                entry.HairColorName = NamedColorHelper.NearestColorName(hairMarkings[0].MarkingColors[0]);
            }
        }

        return entry;
    }
}
