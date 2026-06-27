using System.Linq;
using Content.Server.GameTicking;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Humanoid;
using Content.Shared.NPC.Components;
using KillAllClfRuleComponent = Content.Shared._CMU14.Threats.Rules.KillAllClfRuleComponent;
using KillAllGovforRuleComponent = Content.Shared._CMU14.Threats.Rules.KillAllGovforRuleComponent;
using KillAllHumanRuleComponent = Content.Shared._CMU14.Threats.Rules.KillAllHumanRuleComponent;

namespace Content.Server._CMU14.Threats.Rules;

/// <summary>
///     Shared system for handling handcuff events for KillAllClf, KillAllGovfor, and KillAllHuman rules.
///     Prevents duplicate subscription errors.
/// </summary>
public sealed partial class KillAllRulesHandcuffSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CuffableComponent, TargetHandcuffedEvent>(OnTargetHandcuffed);
    }

    private void OnTargetHandcuffed(EntityUid uid, CuffableComponent component, ref TargetHandcuffedEvent args)
    {
        if (_gameTicker.IsGameRuleActive<KillAllHumanRuleComponent>() && HasComp<HumanoidAppearanceComponent>(uid))
            EntityManager.System<KillAllHumanRuleSystem>().OnHandcuffEvent(uid);

        if (!TryComp(uid, out NpcFactionMemberComponent? faction))
            return;

        if (_gameTicker.IsGameRuleActive<KillAllClfRuleComponent>()
            && faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "clf"))
            EntityManager.System<KillAllClfRuleSystem>().OnHandcuffEvent(uid);
        else if (_gameTicker.IsGameRuleActive<KillAllGovforRuleComponent>()
            && faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "govfor"))
            EntityManager.System<KillAllGovforRuleSystem>().OnHandcuffEvent(uid);
    }
}
