using System.Reflection;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Pair;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.NPC;

[TestFixture]
[TestOf(typeof(NpcFactionSystem))]
public sealed class NpcFactionMergeRegressionTest : GameTest
{
    public override PoolSettings PoolSettings => new()
    {
        Destructive = true,
        Dirty = true,
    };

    [TestPrototypes]
    private const string Prototypes = @"
- type: npcFaction
  id: NpcFactionMergeParent

- type: npcFaction
  id: NpcFactionMergeChild
  parent: NpcFactionMergeParent

- type: npcFaction
  id: NpcFactionMergeGrandchild
  parent: NpcFactionMergeChild

- type: npcFaction
  id: NpcFactionMergeEnemy
  hostile:
  - NpcFactionMergeParent
  - NpcFactionMergeEnemy

- type: npcFaction
  id: NpcFactionMergeEnemyChild
  hostile:
  - NpcFactionMergeChild
  - NpcFactionMergeEnemyChild

- type: npcFaction
  id: NpcFactionMergeEnemyGrandchild
  hostile:
  - NpcFactionMergeGrandchild
  - NpcFactionMergeEnemyGrandchild

- type: entity
  id: NpcFactionMergeMember
  components:
  - type: NpcFactionMember
    factions:
    - NpcFactionMergeEnemy
";

    private const string ReloadedEnemy = @"
- type: npcFaction
  id: NpcFactionMergeEnemy
  hostile:
  - NpcFactionMergeChild
  - NpcFactionMergeEnemy
";

    [Test]
    public async Task HostilityExpandsThroughDescendantsAndRefreshesMemberCachesWithoutSelf()
    {
        var map = await Pair.CreateTestMap();
        EntityUid member = default;
        var changed = new Dictionary<Type, HashSet<string>>();

        await Server.WaitAssertion(() =>
        {
            member = SEntMan.SpawnEntity("NpcFactionMergeMember", map.GridCoords);
            var faction = Server.System<NpcFactionSystem>();
            var cached = faction.GetFactions()["NpcFactionMergeEnemy"];
            var component = SEntMan.GetComponent<NpcFactionMemberComponent>(member);
            ProtoId<NpcFactionPrototype>[] expected =
            [
                "NpcFactionMergeParent",
                "NpcFactionMergeChild",
                "NpcFactionMergeGrandchild",
            ];

            Assert.Multiple(() =>
            {
                Assert.That(cached.Hostile, Is.EquivalentTo(expected));
                Assert.That(component.HostileFactions, Is.EquivalentTo(expected));
                Assert.That(cached.Hostile, Does.Not.Contain((ProtoId<NpcFactionPrototype>) "NpcFactionMergeEnemy"));
                Assert.That(component.HostileFactions,
                    Does.Not.Contain((ProtoId<NpcFactionPrototype>) "NpcFactionMergeEnemy"),
                    "a faction hostile to its own subtree must never become hostile to itself");
            });

            var parent = SProtoMan.Index<NpcFactionPrototype>("NpcFactionMergeParent");
            var originalParents = parent.Parents;
            try
            {
                SetParents(parent, ["NpcFactionMergeGrandchild"]);
                InvokePrivate(faction, "RefreshFactions");

                foreach (var enemy in new[]
                         {
                             "NpcFactionMergeEnemy",
                             "NpcFactionMergeEnemyChild",
                             "NpcFactionMergeEnemyGrandchild",
                         })
                {
                    cached = faction.GetFactions()[enemy];
                    Assert.Multiple(() =>
                    {
                        Assert.That(cached.Hostile, Is.EquivalentTo(expected),
                            $"a malformed cycle entered through {enemy} must retain every reachable node");
                        Assert.That(cached.Hostile,
                            Does.Not.Contain((ProtoId<NpcFactionPrototype>) enemy));
                    });
                }

                Assert.That(component.HostileFactions, Is.EquivalentTo(expected));
            }
            finally
            {
                SetParents(parent, originalParents);
                InvokePrivate(faction, "RefreshFactions");
            }

            Server.ProtoMan.LoadString(ReloadedEnemy, overwrite: true, changed: changed);
        });

        await Server.WaitPost(() => Server.ProtoMan.ReloadPrototypes(changed));
        await Server.WaitAssertion(() =>
        {
            var faction = Server.System<NpcFactionSystem>();
            var cached = faction.GetFactions()["NpcFactionMergeEnemy"];
            var component = SEntMan.GetComponent<NpcFactionMemberComponent>(member);
            ProtoId<NpcFactionPrototype>[] reloadedExpected =
            [
                "NpcFactionMergeChild",
                "NpcFactionMergeGrandchild",
            ];

            Assert.Multiple(() =>
            {
                Assert.That(cached.Hostile, Is.EquivalentTo(reloadedExpected));
                Assert.That(component.HostileFactions, Is.EquivalentTo(reloadedExpected),
                    "a real prototype reload must rebuild already-spawned member caches");
                Assert.That(cached.Hostile,
                    Does.Not.Contain((ProtoId<NpcFactionPrototype>) "NpcFactionMergeEnemy"));
                Assert.That(cached.Hostile,
                    Does.Not.Contain((ProtoId<NpcFactionPrototype>) "NpcFactionMergeParent"),
                    "the reloaded direct hostile root must replace the previous parent root");
            });
        });
    }

    [Test]
    public async Task RuntimeBidirectionalAllianceChangesSurviveEveryMemberCacheRefresh()
    {
        var map = await Pair.CreateTestMap();
        EntityUid clf = default;
        EntityUid weyu = default;
        const string left = "CLF";
        const string right = "AUWeYu";

        await Server.WaitAssertion(() =>
        {
            var faction = Server.System<NpcFactionSystem>();
            var initialLeft = Relation(faction, left, right);
            var initialRight = Relation(faction, right, left);

            try
            {
                clf = Spawn(left);
                weyu = Spawn(right);

                faction.RealMakeFriendly(left, right);
                faction.RealMakeFriendly(right, left);
                AssertRelation(faction, clf, weyu, left, right, friendly: true, hostile: false);

                faction.RealMakeNeutral(left, right);
                faction.RealMakeNeutral(right, left);
                AssertRelation(faction, clf, weyu, left, right, friendly: false, hostile: false);

                faction.RealMakeHostile(left, right);
                faction.RealMakeHostile(right, left);
                AssertRelation(faction, clf, weyu, left, right, friendly: false, hostile: true);

                InvokePrivate(faction, "RealRefreshFactions");
                AssertRelation(faction, clf, weyu, left, right, friendly: false, hostile: true);
            }
            finally
            {
                Restore(faction, left, right, initialLeft);
                Restore(faction, right, left, initialRight);
            }
        });
        return;

        EntityUid Spawn(string faction)
        {
            var uid = SEntMan.SpawnEntity(null, map.GridCoords);
            var component = SEntMan.EnsureComponent<NpcFactionMemberComponent>(uid);
            Server.System<NpcFactionSystem>().AddFaction((uid, component), faction);
            return uid;
        }
    }

    private void AssertRelation(
        NpcFactionSystem faction,
        EntityUid leftMember,
        EntityUid rightMember,
        string left,
        string right,
        bool friendly,
        bool hostile)
    {
        var leftComponent = SEntMan.GetComponent<NpcFactionMemberComponent>(leftMember);
        var rightComponent = SEntMan.GetComponent<NpcFactionMemberComponent>(rightMember);
        Assert.Multiple(() =>
        {
            Assert.That(faction.IsFactionFriendly(left, right), Is.EqualTo(friendly));
            Assert.That(faction.IsFactionHostile(left, right), Is.EqualTo(hostile));
            Assert.That(leftComponent.FriendlyFactions.Contains(right), Is.EqualTo(friendly));
            Assert.That(rightComponent.FriendlyFactions.Contains(left), Is.EqualTo(friendly));
            Assert.That(leftComponent.HostileFactions.Contains(right), Is.EqualTo(hostile));
            Assert.That(rightComponent.HostileFactions.Contains(left), Is.EqualTo(hostile));
        });
    }

    private static (bool Friendly, bool Hostile) Relation(
        NpcFactionSystem faction,
        string source,
        string target)
    {
        var data = faction.GetFactions()[source];
        return (data.Friendly.Contains(target), data.Hostile.Contains(target));
    }

    private static void Restore(
        NpcFactionSystem faction,
        string source,
        string target,
        (bool Friendly, bool Hostile) relation)
    {
        if (relation.Friendly)
            faction.RealMakeFriendly(source, target);
        else if (relation.Hostile)
            faction.RealMakeHostile(source, target);
        else
            faction.RealMakeNeutral(source, target);
    }

    private static void InvokePrivate(object instance, string method)
    {
        instance.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.Name == method && candidate.GetParameters().Length == 0)
            .Invoke(instance, null);
    }

    private static void SetParents(NpcFactionPrototype prototype, string[]? parents)
    {
        typeof(NpcFactionPrototype)
            .GetProperty(nameof(NpcFactionPrototype.Parents), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(prototype, parents);
    }
}
