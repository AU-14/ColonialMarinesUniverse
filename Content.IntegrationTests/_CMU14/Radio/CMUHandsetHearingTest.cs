using Content.IntegrationTests.Fixtures;
using Content.Server.CMU14.Radio;
using Content.Shared.CMU14.Radio;
using Content.Shared.Radio.Components;

namespace Content.IntegrationTests.CMU14.Radio;

[TestFixture]
public sealed class CMUHandsetHearingTest : GameTest
{
    [Test]
    public async Task ChannelChangesAndHeadsetOverlapPreserveUnrelatedGrants()
    {
        await Server.WaitAssertion(() =>
        {
            var radio = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var user = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var headset = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            try
            {
                var pack = SEntMan.AddComponent<ANPRCRadioComponent>(radio);
                pack.Planted = true;
                pack.GrantedChannels.UnionWith(new[] { "MarineCommon", "MarineMedical" });
                var holder = SEntMan.AddComponent<ANPRCHandsetUserComponent>(user);
                holder.Radio = radio;
                var system = SEntMan.System<ANPRCRadioSystem>();
                system.Update(0);
                var active = SEntMan.GetComponent<ActiveRadioComponent>(user);
                Assert.That(active.Channels, Is.EquivalentTo(pack.GrantedChannels));
                Assert.That(SEntMan.HasComponent<IntrinsicRadioReceiverComponent>(user), Is.True);

                active.Channels.Add("MarineCommand");
                system.Update(0);
                Assert.That(active.Channels.Contains("MarineCommand"), Is.True);
                pack.GrantedChannels.Remove("MarineMedical");
                pack.GrantedChannels.Add("MarineEngineer");
                system.Update(0);
                Assert.That(active.Channels, Is.EquivalentTo(new[] { "MarineCommon", "MarineEngineer", "MarineCommand" }));

                var keys = SEntMan.AddComponent<EncryptionKeyHolderComponent>(headset);
                keys.Channels.Add("MarineCommon");
                SEntMan.AddComponent<WearingHeadsetComponent>(user).Headset = headset;
                system.Update(0);
                Assert.That(holder.GrantedChannels, Is.EquivalentTo(new[] { "MarineEngineer" }));
                keys.Channels.Clear();
                system.Update(0);
                Assert.That(holder.GrantedChannels, Is.EquivalentTo(pack.GrantedChannels));
                pack.GrantedChannels.Clear();
                system.Update(0);
                Assert.That(holder.GrantedChannels, Is.Empty);
                Assert.That(active.Channels, Is.EquivalentTo(new[] { "MarineCommand" }));
            }
            finally
            {
                SEntMan.DeleteEntity(user);
                SEntMan.DeleteEntity(headset);
                SEntMan.DeleteEntity(radio);
            }
        });
    }
}
