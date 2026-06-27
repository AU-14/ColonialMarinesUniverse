using System.Linq;
using Content.Server._RMC14.Language.Systems;
using Content.Shared._CMU14.Language;
using Content.Shared._RMC14.Language.Prototypes;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Server._CMU14.Language;

public sealed class FactionLanguageSystem : EntitySystem
{
    [Dependency] private LanguageSystem _language = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<FactionLanguageLeaderComponent, PlayerAttachedEvent>(OnLeaderPlayerAttached);
        SubscribeLocalEvent<FactionLanguageMemberComponent, ComponentStartup>(OnMemberSpawn);
        SubscribeLocalEvent<FactionLanguageLeaderComponent, FactionLanguagePickedMessage>(OnLanguagePicked);
    }

    private void OnLeaderPlayerAttached(Entity<FactionLanguageLeaderComponent> ent, ref PlayerAttachedEvent args)
    {
        Log.Debug($"[FactionLanguage] PlayerAttached: {ToPrettyString(ent.Owner)}, ChosenLanguage={ent.Comp.ChosenLanguage}");
        if (ent.Comp.ChosenLanguage != null)
        {
            // Already picked — just make sure this new controller has the language
            _language.AddLanguage(ent.Owner, ent.Comp.ChosenLanguage.Value);
            _ui.CloseUi(ent.Owner, FactionLanguageUiKey.Key);
            return;
        }

        // Check if another leader already picked for this faction
        var query = EntityQueryEnumerator<FactionLanguageLeaderComponent>();
        while (query.MoveNext(out var otherUid, out var otherLeader))
        {
            if (otherUid == ent.Owner || otherLeader.FactionTag != ent.Comp.FactionTag)
                continue;

            if (otherLeader.ChosenLanguage == null)
                continue;

            ent.Comp.ChosenLanguage = otherLeader.ChosenLanguage;
            _language.AddLanguage(ent.Owner, otherLeader.ChosenLanguage.Value);
            _ui.CloseUi(ent.Owner, FactionLanguageUiKey.Key);
            return;
        }

        var languages = _proto.EnumeratePrototypes<LanguagePrototype>()
            .Where(l => l.ID != "Xeno" && l.ID != "Primitive" && l.ID != "Yautja" && l.ID != "TacticalSignLanguage" && l.ID != "SignLanguage" && l.ID != "Arcturian")
            .Select(l => l.ID)
            .OrderBy(id => id)
            .ToList();

        var state = new FactionLanguagePickerState(languages, ent.Comp.FactionTag);

        // Force close first to reset BUI state, then reopen
        _ui.CloseUi(ent.Owner, FactionLanguageUiKey.Key);
        _ui.SetUiState(ent.Owner, FactionLanguageUiKey.Key, state);
        _ui.OpenUi(ent.Owner, FactionLanguageUiKey.Key, args.Player);
    }

    private void OnLanguagePicked(Entity<FactionLanguageLeaderComponent> ent, ref FactionLanguagePickedMessage args)
    {
        if (ent.Comp.ChosenLanguage != null)
            return;

        ent.Comp.ChosenLanguage = args.Language;
        Dirty(ent);

        _ui.CloseUi(ent.Owner, FactionLanguageUiKey.Key);

        _language.AddLanguage(ent.Owner, args.Language);

        var query = EntityQueryEnumerator<FactionLanguageMemberComponent>();
        while (query.MoveNext(out var memberUid, out var member))
        {
            if (member.FactionTag != ent.Comp.FactionTag || member.LanguageApplied)
                continue;

            _language.AddLanguage(memberUid, args.Language);
            member.LanguageApplied = true;
            Dirty(memberUid, member);
        }
    }

    private void OnMemberSpawn(Entity<FactionLanguageMemberComponent> ent, ref ComponentStartup args)
    {
        if (ent.Comp.LanguageApplied)
            return;

        // Check if a leader for this faction already picked
        var query = EntityQueryEnumerator<FactionLanguageLeaderComponent>();
        while (query.MoveNext(out _, out var leader))
        {
            if (leader.FactionTag != ent.Comp.FactionTag || leader.ChosenLanguage == null)
                continue;

            _language.AddLanguage(ent.Owner, leader.ChosenLanguage.Value);
            ent.Comp.LanguageApplied = true;
            Dirty(ent);
            return;
        }
        // Leader hasn't picked yet — OnLanguagePicked will catch this member
    }

    /// <summary>
    /// Called from TattooSystem after inducting a new member mid-round.
    /// </summary>
    public void ApplyFactionLanguageToNewMember(EntityUid target, string factionTag)
    {
        var member = EnsureComp<FactionLanguageMemberComponent>(target);
        member.FactionTag = factionTag;

        if (member.LanguageApplied)
            return;

        var query = EntityQueryEnumerator<FactionLanguageLeaderComponent>();
        while (query.MoveNext(out _, out var leader))
        {
            if (leader.FactionTag != factionTag || leader.ChosenLanguage == null)
                continue;

            _language.AddLanguage(target, leader.ChosenLanguage.Value);
            member.LanguageApplied = true;
            Dirty(target, member);
            return;
        }
    }
}
