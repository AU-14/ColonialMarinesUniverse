using System.Linq;
using Content.Server.GameTicking;
using Content.Shared.AU14;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Humanoid;
using Content.Shared.NPC.Components;

namespace Content.Server.AU14.Threats;

/// <summary>
/// Shared system for handling handcuff events for KillAllClf, KillAllGovfor, and KillAllHuman rules.
/// Prevents duplicate subscription errors.
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
        // Dispatch to KillAllHuman rule for any humanoid handcuff
        if (HasComp<HumanoidAppearanceComponent>(uid) && _gameTicker.IsGameRuleActive<KillAllHumanRuleComponent>())
        {
            var humanSys = EntityManager.System<KillAllHumanRuleSystem>();
            humanSys.OnHandcuffEvent(uid);
        }

        // Check if this entity has a faction for faction-specific rules
        if (!TryComp<NpcFactionMemberComponent>(uid, out var faction))
            return;

        var factionName = "";
        if (faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "clf"))
            factionName = "clf";
        else if (faction.Factions.Any(f => f.ToString().ToLowerInvariant() == "govfor"))
            factionName = "govfor";
        else
            return;

        // Dispatch to the appropriate rule system
        if (factionName == "clf" && _gameTicker.IsGameRuleActive<KillAllClfRuleComponent>())
        {
            var sys = EntityManager.System<KillAllClfRuleSystem>();
            sys.OnHandcuffEvent(uid);
        }
        else if (factionName == "govfor" && _gameTicker.IsGameRuleActive<KillAllGovforRuleComponent>())
        {
            var sys = EntityManager.System<KillAllGovforRuleSystem>();
            sys.OnHandcuffEvent(uid);
        }
    }
}

