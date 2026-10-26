// SPDX-License-Identifier: AGPL-3.0-only
using System.Linq;
using Content.Shared.CMU14.Government;
using Robust.Shared.Random;

namespace Content.Server.CMU14.Government;

public sealed partial class ColonyGovernmentSystem
{
    #region The petitions

    private void UpdatePetitions(ColonyGovernmentComponent comp, TimeSpan now)
    {
        foreach (var petition in comp.Petitions.ToList())
        {
            if (now >= petition.Deadline)
                Expire(comp, petition);
        }

        if (now < comp.NextPetition)
            return;

        comp.NextPetition = now + TimeSpan.FromSeconds(_random.Next(360, 600));
        if (comp.Petitions.Count < MaxPetitions)
            NewPetition(comp, now);
    }

    // An ambassador asks: a faction that has nothing waiting, chosen at random, and a petition that fits what it thinks of the colony (the ultimatums come from those who think ill of it).
    private void NewPetition(ColonyGovernmentComponent comp, TimeSpan now)
    {
        var busy = comp.Petitions.Select(p => p.Faction).ToHashSet();
        var factions = _market.KnownFactions().Where(f => !busy.Contains(f)).ToList();
        if (factions.Count == 0)
            return;

        var faction = _random.Pick(factions);
        var attitude = AttitudeOf(comp, faction);

        var candidates = _proto.EnumeratePrototypes<ColonyPetitionPrototype>()
            .Where(p => attitude >= p.MinAttitude && attitude <= p.MaxAttitude)
            .ToList();
        if (candidates.Count == 0)
            return;

        var total = candidates.Sum(p => p.Weight);
        var roll = _random.NextFloat() * total;
        var pick = candidates[^1];
        foreach (var candidate in candidates)
        {
            roll -= candidate.Weight;
            if (roll <= 0f)
            {
                pick = candidate;
                break;
            }
        }

        comp.Petitions.Add(new Petition(comp.NextPetitionId++, pick.ID, faction, now + TimeSpan.FromSeconds(pick.DeadlineSeconds), pick.AcceptCost, pick.AcceptReward));
    }

    // Not answered in time: an ultimatum is paid for as it says; a request is only a little offence.
    private void Expire(ColonyGovernmentComponent comp, Petition petition)
    {
        comp.Petitions.Remove(petition);
        if (!_proto.TryIndex<ColonyPetitionPrototype>(petition.Proto, out var proto))
            return;

        if (proto.Ultimatum)
        {
            ShiftAttitude(comp, petition.Faction, proto.IgnoreAttitude);
        }
        else
        {
            ShiftAttitude(comp, petition.Faction, -3);
        }
    }

    private void OnAnswer(Entity<ColonyGovernmentConsoleComponent> console, ref AnswerPetitionMessage msg)
    {
        if (!TryGovernment(out var government))
            return;

        var comp = government.Comp;
        var petitionId = msg.Petition;
        var petition = comp.Petitions.FirstOrDefault(p => p.Id == petitionId);
        if (petition == null || !_proto.TryIndex<ColonyPetitionPrototype>(petition.Proto, out var proto))
            return;

        switch (msg.Answer)
        {
            case PetitionAnswer.Accept:
                if (petition.Cost > 0 && _budget.GetBudget() < petition.Cost)
                    return;

                if (petition.Cost > 0)
                    _budget.AddToBudget(-petition.Cost);

                if (petition.Reward > 0)
                    _budget.AddToBudget(petition.Reward);

                ShiftAttitude(comp, petition.Faction, proto.AcceptAttitude);
                _market.GrantPoints(petition.Faction, proto.AcceptPoints);
                comp.Petitions.Remove(petition);
                break;

            case PetitionAnswer.Decline:
                ShiftAttitude(comp, petition.Faction, proto.DeclineAttitude);
                comp.Petitions.Remove(petition);
                break;

            case PetitionAnswer.Bargain:
                // One bargain for a petition. How it goes depends on what the faction thinks of the colony; if it goes well the reward is a third bigger and
                // the cost a quarter less, and if not the faction is a little offended.
                if (petition.Bargained)
                    return;

                petition.Bargained = true;
                var chance = Math.Clamp(0.55f + AttitudeOf(comp, petition.Faction) / 250f, 0.1f, 0.85f);
                if (_random.Prob(chance))
                {
                    petition.Reward = (int) MathF.Round(petition.Reward * 1.35f);
                    petition.Cost = (int) MathF.Round(petition.Cost * 0.75f);
                }
                else
                {
                    ShiftAttitude(comp, petition.Faction, -4);
                }

                break;
        }

        Refresh();
    }

    #endregion
}
