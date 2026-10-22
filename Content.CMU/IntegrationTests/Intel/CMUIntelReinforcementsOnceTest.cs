using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Intel;
using Content.Shared._RMC14.Intel.Tech;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.CMU14.Intel;

/// <summary>
///     Each intel list may sell a given reinforcement team once: no option that calls in a third party can be bought
///     again, and no list offers the same third party through two different options. The CLF's insurgent
///     reinforcements and jeep crews are exceptions, capped at a few purchases.
/// </summary>
[TestFixture]
public sealed class CMUIntelReinforcementsOnceTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false };

    private static readonly HashSet<string> RepeatableReinforcements = new() { "cmu-tech-clf-reinforcements", "cmu-tech-clf-jeep" };

    [Test]
    public async Task ReinforcementsCanOnlyBeOrderedOnce()
    {
        await Server.WaitAssertion(() =>
        {
            var factory = SEntMan.ComponentFactory;
            foreach (var entity in SProtoMan.EnumeratePrototypes<EntityPrototype>())
            {
                if (!entity.TryGetComponent<IntelTechTreeComponent>(out var tree, factory))
                    continue;

                var sold = new Dictionary<string, string>();
                foreach (var option in tree.Tree.Options.SelectMany(tier => tier))
                {
                    var parties = option.Events.OfType<TechPartySpawnEvent>().ToList();
                    if (parties.Count == 0)
                        continue;

                    if (RepeatableReinforcements.Contains(option.Name))
                    {
                        Assert.That(option.MaxPurchases, Is.GreaterThan(0),
                            $"{entity.ID} tech '{option.Name}' may repeat, but must have a purchase cap");
                    }
                    else
                    {
                        Assert.That(option.Repurchasable, Is.False,
                            $"{entity.ID} tech '{option.Name}' calls in reinforcements but can be bought more than once");
                    }

                    foreach (var party in parties)
                    {
                        Assert.That(sold.TryAdd(party.ThirdPartyId, option.Name), Is.True,
                            $"{entity.ID} sells third party '{party.ThirdPartyId}' through both '{option.Name}' and " +
                            $"'{sold.GetValueOrDefault(party.ThirdPartyId)}'");
                    }
                }
            }
        });
    }
}
