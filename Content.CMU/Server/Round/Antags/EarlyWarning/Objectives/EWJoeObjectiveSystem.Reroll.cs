using Content.Shared.CMU14.Round.Antags.EarlyWarning;
using Content.Shared.Database;

namespace Content.Server.CMU14.Round.Antags.EarlyWarning.Objectives;

/// <summary>
/// ARCH-02: the player's fallback for directives the code cannot judge stale (a kill target hiding on the
/// ship, a console behind a locked door). "Request new directive" on the status panel swaps one unfinished,
/// unforced directive for another from the level's pool, once per level, or again after
/// <see cref="EWJoeDirectiveStateComponent.RerollCooldown"/> while the level lasts. A request that finds
/// nothing else on offer leaves the directive and the reroll untouched.
/// </summary>
public sealed partial class EWJoeObjectiveSystem
{
    private void InitializeReroll()
    {
        SubscribeNetworkEvent<EWJoeRequestDirectiveRerollEvent>(OnRerollRequest);
    }

    private void OnRerollRequest(EWJoeRequestDirectiveRerollEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } joe || !TryComp(joe, out EWJoeComponent? comp) || !comp.Running)
            return;

        TryReroll((joe, comp), msg.Slot);
        UpdateStatus((joe, comp));
    }

    /// <summary>Whether the reroll is ready: not yet spent on this level, or its cooldown has run out.</summary>
    public bool RerollReady(Entity<EWJoeComponent> ent)
    {
        var state = EnsureComp<EWJoeDirectiveStateComponent>(ent);
        return state.RerollLevel != ent.Comp.Level || _timing.CurTime >= state.LastReroll + state.RerollCooldown;
    }

    /// <summary>An unfinished directive that is not the level's forced one (the level-4 console gate).</summary>
    public bool IsRerollable(Entity<EWJoeComponent> ent, EWJoeLevelDef def, EntityUid objective)
    {
        if (ent.Comp.CompletedCredit.ContainsKey(objective) || !Exists(objective))
            return false;

        return def.ForcedObjective is not { } forced || MetaData(objective).EntityPrototype?.ID != forced.Id;
    }

    /// <summary>
    /// Swaps the directive at <paramref name="slot"/> of the current offer for another from the level's pool,
    /// never one already on offer nor the same kind. Spends the reroll only when a replacement was found.
    /// </summary>
    public bool TryReroll(Entity<EWJoeComponent> ent, int slot)
    {
        if (_mobState.IsDead(ent) || !TryGetLevel(ent, out var def) || def.ObjectiveGroup is not { } pool)
            return false;

        if (!_mind.TryGetMind(ent, out var mindId, out var mind))
            return false;

        if (!RerollReady(ent))
        {
            _messages.Notify(ent, Loc.GetString("cmu-ew-joe-reroll-not-ready"));
            return false;
        }

        if (slot < 0 || slot >= ent.Comp.CurrentObjectives.Count)
            return false;

        var objective = ent.Comp.CurrentObjectives[slot];
        if (!IsRerollable(ent, def, objective))
        {
            _messages.Notify(ent, Loc.GetString("cmu-ew-joe-reroll-refused"));
            return false;
        }

        // the directive being swapped is on offer, so its kind is excluded with the rest of the offer
        var offered = OfferedPrototypes(ent);
        if (PickFromPool(mindId, mind, pool, offered) is not { } replacement)
        {
            _messages.Notify(ent, Loc.GetString("cmu-ew-joe-reroll-none"));
            return false;
        }

        _adminLogger.Add(LogType.Mind, LogImpact.Low,
            $"Early Warning Joe {ToPrettyString(ent):joe} rerolled {ToPrettyString(objective):old} into {ToPrettyString(replacement):new}");

        // withdrawing deletes the old directive
        Withdraw(ent, mindId, mind, objective);
        ent.Comp.CurrentObjectives.Remove(objective);
        Offer(ent, mindId, mind, replacement, offered);

        var state = EnsureComp<EWJoeDirectiveStateComponent>(ent);
        state.RerollLevel = ent.Comp.Level;
        state.LastReroll = _timing.CurTime;

        _messages.Notify(ent, Loc.GetString("cmu-ew-joe-reroll-done"));
        return true;
    }
}
