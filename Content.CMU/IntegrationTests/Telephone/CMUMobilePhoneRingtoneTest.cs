using Content.IntegrationTests.Fixtures;
using Content.Shared._RMC14.Telephone;
using Content.Shared.Audio;
using Content.Shared.CMU14.Telephone;
using Robust.Shared.Audio;

namespace Content.IntegrationTests.CMU14.Telephone;

[TestFixture]
public sealed class CMUMobilePhoneRingtoneTest : GameTest
{
    [Test]
    public async Task OrdinaryCallRestoresRingtoneAfterPrank()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var caller = SEntMan.SpawnEntity("CMUMobilePhone", map.GridCoords);
            var receiver = SEntMan.SpawnEntity("CMUMobilePhone", map.GridCoords);
            var mobile = SEntMan.GetComponent<CMUMobilePhoneComponent>(receiver);
            var phone = SEntMan.GetComponent<RotaryPhoneComponent>(receiver);
            var ambient = SEntMan.EnsureComponent<AmbientSoundComponent>(receiver);
            var normal = new SoundPathSpecifier("/Audio/_RMC14/Phone/telephone_ring.ogg",
                AudioParams.Default.WithVolume(-7));
            phone.ReceivingSound = normal;

            mobile.PrankRingtoneChance = 1;
            var ring = new RMCTelephoneRingEvent(receiver, caller, caller);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ring);
            Assert.That(ambient.Sound, Is.EqualTo(mobile.PrankRingtone));

            mobile.PrankRingtoneChance = 0;
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref ring);
            Assert.Multiple(() =>
            {
                Assert.That(ambient.Sound, Is.EqualTo(normal), "A later ordinary call must restore the receiver's ringtone.");
                Assert.That(ambient.Volume, Is.EqualTo(normal.Params.Volume), "Restore its configured volume too.");
            });
        });
    }
}
