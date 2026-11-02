using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.Server.Chat.Systems;
using Content.Server.CMU14.Surrender;
using Content.Shared.CMU14.Surrender;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.CMU14.Surrender;

[TestFixture]
public sealed class CMUSurrenderCuffingTest : GameTest
{
    [TestCase("RMCHandcuffs", false)]
    [TestCase("RMCHandcuffs", true)]
    [TestCase("RMCZipties", false)]
    [TestCase("RMCZipties", true)]
    public async Task RestraintsUseCooperativeTimingOnlyForRaisedHands(string prototype, bool surrendering)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var cuffs = SEntMan.SpawnEntity(prototype, map.GridCoords);
            try
            {
                Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, cuffs), Is.True);
                if (surrendering)
                    Assert.That(Server.System<ChatSystem>().TryEmoteWithChat(target, CMUSurrenderSystem.SurrenderEmote), Is.True);
                Assert.That(Server.System<SharedCuffableSystem>().TryCuffing(user, target, cuffs), Is.True);
                var doAfters = SEntMan.GetComponent<DoAfterComponent>(user).DoAfters;
                var action = doAfters.Values.Single(d => d.Args.Event is AddCuffDoAfterEvent);
                var normal = SEntMan.GetComponent<HandcuffComponent>(cuffs).CuffTime;
                Assert.That(action.Args.Delay.TotalSeconds,
                    Is.EqualTo(surrendering ? normal / 2 : normal).Within(0.001));
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(target), Is.EqualTo(surrendering),
                    "Starting a cuff attempt must not cancel surrender.");
            }
            finally
            {
                SEntMan.DeleteEntity(user);
                SEntMan.DeleteEntity(target);
                if (!SEntMan.Deleted(cuffs)) SEntMan.DeleteEntity(cuffs);
            }
        });
    }

    [TestCase("RMCHandcuffs", false)]
    [TestCase("RMCHandcuffs", true)]
    [TestCase("RMCZipties", false)]
    [TestCase("RMCZipties", true)]
    public async Task AppliedRestraintsEndSurrenderAndPreventRestart(string prototype, bool instant)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var target = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            EntityUid? cuffs = null;
            try
            {
                var chat = Server.System<ChatSystem>();
                var surrender = Server.System<CMUSurrenderSystem>();
                var cuffing = Server.System<SharedCuffableSystem>();
                Assert.That(chat.TryEmoteWithChat(target, CMUSurrenderSystem.SurrenderEmote), Is.True);
                if (instant)
                {
                    cuffs = cuffing.TryAddCuffsInstant(target, prototype);
                    Assert.That(cuffs, Is.Not.Null);
                }
                else
                {
                    cuffs = SEntMan.SpawnEntity(prototype, map.GridCoords);
                    Assert.That(Server.System<SharedHandsSystem>().TryPickupAnyHand(user, cuffs.Value), Is.True);
                    Assert.That(cuffing.TryAddNewCuffs(target, user, cuffs.Value), Is.True);
                }

                Assert.That(SEntMan.GetComponent<CuffableComponent>(target).CuffedHandCount, Is.GreaterThan(0));
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(target), Is.False);
                Assert.That(surrender.CanSurrender(target), Is.False);
                Assert.That(chat.TryEmoteWithChat(target, CMUSurrenderSystem.SurrenderEmote), Is.False);
                Assert.That(SEntMan.HasComponent<CMUSurrenderComponent>(target), Is.False);
            }
            finally
            {
                SEntMan.DeleteEntity(user);
                SEntMan.DeleteEntity(target);
                if (cuffs is { } item && !SEntMan.Deleted(item))
                    SEntMan.DeleteEntity(item);
            }
        });
    }
}
