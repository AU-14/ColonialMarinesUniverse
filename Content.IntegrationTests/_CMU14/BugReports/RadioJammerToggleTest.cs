using Content.IntegrationTests.Fixtures;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Radio.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.BugReports;

// BUG-1052: the jammer had no ItemToggle, so it could never switch on and jammed nothing
[TestFixture]
public sealed class RadioJammerToggleTest : GameTest
{
    [Test]
    public async Task JammerStartsOffAndJamsOnceSwitchedOn()
    {
        var map = await Pair.CreateTestMap();
        EntityUid jammer = default;
        await Server.WaitPost(() => jammer = SEntMan.SpawnEntity("AU14RadioJammer", map.GridCoords));
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<ActiveRadioJammerComponent>(jammer), Is.False, "a fresh cell shouldn't switch it on");
            Assert.That(Server.System<ItemToggleSystem>().TryActivate(jammer), Is.True);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
        {
            Assert.That(SEntMan.HasComponent<ActiveRadioJammerComponent>(jammer), Is.True);
            Assert.That(Server.System<ItemToggleSystem>().TryDeactivate(jammer), Is.True);
        });
        await Pair.RunTicksSync(5);

        await Server.WaitAssertion(() =>
            Assert.That(SEntMan.HasComponent<ActiveRadioJammerComponent>(jammer), Is.False));
    }
}
