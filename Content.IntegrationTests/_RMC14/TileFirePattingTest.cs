using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Atmos;
using Content.Shared.Interaction;
using Robust.Client.GameStates;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests._RMC14;

[TestFixture]
[TestOf(typeof(SharedRMCFlammableSystem))]
public sealed class TileFirePattingTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = true, Dirty = true };

    [Test]
    public async Task ServerExtinguishesAfterTwoPatsAndRespectsCooldown()
    {
        var map = await Pair.CreateTestMap();
        EntityUid user = default;
        EntityUid fire = default;

        await Server.WaitAssertion(() =>
        {
            user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            fire = SEntMan.SpawnEntity("RMCTileFire", map.GridCoords);
            var tileFire = SEntMan.GetComponent<TileFireComponent>(fire);
            var nonPatter = SEntMan.SpawnEntity(null, map.GridCoords);

            Assert.That(PatFire(SEntMan, nonPatter, fire), Is.False,
                "An entity without TileFirePatter must not extinguish tile fires.");
            Assert.That(tileFire.CurrentPats, Is.Zero);

            var patter = SEntMan.GetComponent<TileFirePatterComponent>(user);
            patter.Last = SGameTiming.CurTime - patter.Cooldown;

            Assert.That(PatFire(SEntMan, user, fire), Is.True);
            Assert.That(tileFire.CurrentPats, Is.EqualTo(1));
            Assert.That(SEntMan.IsQueuedForDeletion(fire), Is.False);

            Assert.That(PatFire(SEntMan, user, fire), Is.False,
                "A second interaction during cooldown must not count as another pat.");
            Assert.That(tileFire.CurrentPats, Is.EqualTo(1));
            Assert.That(SEntMan.IsQueuedForDeletion(fire), Is.False);
        });

        await Pair.RunSeconds(0.6f);

        await Server.WaitAssertion(() =>
        {
            Assert.That(PatFire(SEntMan, user, fire), Is.True);
            Assert.That(SEntMan.IsQueuedForDeletion(fire), Is.True,
                "The second accepted pat must extinguish the fire on the server.");
        });
        await Pair.RunTicksSync(1);
        await Server.WaitAssertion(() => Assert.That(SEntMan.Deleted(fire), Is.True));
    }

    [Test]
    public async Task PredictionReplaysDoNotCountPatsOrDeleteNetworkedFire()
    {
        var map = await Pair.CreateTestMap();
        NetEntity userNet = default;
        NetEntity fireNet = default;

        await Server.WaitAssertion(() =>
        {
            var user = SEntMan.SpawnEntity("CMMobHuman", map.GridCoords);
            var patter = SEntMan.GetComponent<TileFirePatterComponent>(user);
            patter.Last = SGameTiming.CurTime - patter.Cooldown;
            SEntMan.Dirty(user, patter);
            var fire = SEntMan.SpawnEntity("RMCTileFire", map.GridCoords);
            Server.PlayerMan.SetAttachedEntity(Pair.Player!, user);
            userNet = SEntMan.GetNetEntity(user);
            fireNet = SEntMan.GetNetEntity(fire);
        });
        await Pair.RunUntilSynced();

        await Client.WaitAssertion(() =>
        {
            var user = CEntMan.GetEntity(userNet);
            var fire = CEntMan.GetEntity(fireNet);
            var patter = CEntMan.GetComponent<TileFirePatterComponent>(user);
            var gameStates = Client.ResolveDependency<IClientGameStateManager>();
            var lastPat = patter.Last;
            Assert.That(CGameTiming.InPrediction, Is.True);
            Assert.That(CEntMan.IsClientSide(fire), Is.False);

            Assert.That(PatFire(CEntMan, user, fire), Is.True);
            Assert.That(patter.Last, Is.EqualTo(CGameTiming.CurTime),
                "The client must still predict the pat cooldown.");
            Assert.That(PatFire(CEntMan, user, fire), Is.False);

            // Replaying the same input used to increment the unsynchronized counter a second time.
            gameStates.ResetPredictedEntities();
            Assert.That(patter.Last, Is.EqualTo(lastPat),
                "Rollback must restore the server's cooldown before replaying the interaction.");
            CGameTiming.StartPastPrediction();
            try
            {
                Assert.That(PatFire(CEntMan, user, fire), Is.True);
            }
            finally
            {
                CGameTiming.EndPastPrediction();
            }

            Assert.That(CEntMan.GetComponent<TileFireComponent>(fire).CurrentPats, Is.Zero,
                "Only the server may advance the extinguishing counter.");
            Assert.That(CEntMan.IsQueuedForDeletion(fire), Is.False);
            Assert.That(CEntMan.Deleted(fire), Is.False);
        });
    }

    private static bool PatFire(IEntityManager entMan, EntityUid user, EntityUid fire)
    {
        var interaction = new InteractHandEvent(user, fire);
        entMan.EventBus.RaiseLocalEvent(fire, interaction);
        return interaction.Handled;
    }
}
