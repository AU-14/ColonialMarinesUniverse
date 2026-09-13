using Content.IntegrationTests.Fixtures;
using Content.Shared.Stacks;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests.Stacks;

[TestFixture]
[TestOf(typeof(StackPrototype))]
public sealed class StackPrototypeTest : GameTest
{
    private const string BaseStack = "StackPrototypeInheritanceBase";
    private const string ChildStack = "StackPrototypeInheritanceChild";
    private const string SpawnPrototype = "StackPrototypeInheritanceSpawn";
    private const string RawName = "cmu raw inherited stack name";

    [Test]
    public async Task InheritanceAbstractTypedSpawnAndRawNamesRemainSupported()
    {
        var localization = Server.ResolveDependency<ILocalizationManager>();
        var componentFactory = Server.ResolveDependency<IComponentFactory>();

        await Server.WaitAssertion(() =>
        {
            var childPrototype = SProtoMan.Index<StackPrototype>(ChildStack);
            EntProtoId<StackComponent> typedSpawn = childPrototype.Spawn;

            Assert.Multiple(() =>
            {
                Assert.That(SProtoMan.TryIndex<StackPrototype>(BaseStack, out _), Is.False,
                    "abstract stack prototypes participate in inheritance but are not indexable");
                Assert.That(childPrototype.Abstract, Is.False);
                Assert.That(childPrototype.Parents, Is.EqualTo(new[] { BaseStack }));
                Assert.That(childPrototype.Name, Is.EqualTo(RawName));
                Assert.That(childPrototype.MaxCount, Is.EqualTo(17));
                Assert.That(typedSpawn.Id, Is.EqualTo(SpawnPrototype));
                Assert.That(typedSpawn.TryGet(out var stack, SProtoMan, componentFactory), Is.True,
                    "The typed spawn must resolve to an entity prototype with StackComponent.");
                Assert.That(stack!.StackTypeId, Is.EqualTo(new ProtoId<StackPrototype>(ChildStack)));
            });

            var forkPrototype = SProtoMan.Index<StackPrototype>("CMUYautjaHealingGel");
            Assert.Multiple(() =>
            {
                Assert.That(forkPrototype.Name, Is.EqualTo("healing gel"));
                Assert.That(localization.HasString(forkPrototype.Name), Is.False,
                    "This representative fork stack name is intentionally raw, not a localization ID.");
                Assert.That(
                    localization.GetString(forkPrototype.Name, ("amount", 2)),
                    Is.EqualTo(forkPrototype.Name),
                    "Existing localization consumers must continue to fall back to the raw stack name.");
            });
        });
    }

    [TestPrototypes]
    private const string Prototypes = $@"
- type: stack
  id: {BaseStack}
  abstract: true
  name: {RawName}
  spawn: {SpawnPrototype}
  maxCount: 17

- type: stack
  id: {ChildStack}
  parent: {BaseStack}

- type: entity
  id: {SpawnPrototype}
  components:
  - type: Stack
    stackType: {ChildStack}
    count: 1
";
}
