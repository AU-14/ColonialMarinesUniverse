using Content.Shared._RMC14.Cassette;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._RMC14.Networking;

[TestFixture]
public sealed class StaleEntityStateTest
{
    [Test]
    public async Task DeletedEntityReferencesSerializeWithoutResolutionErrors()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false,
        });
        var server = pair.Server;

        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var deleted = entities.SpawnEntity(null, MapCoordinates.Nullspace);

            var projectile = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var targeted = entities.AddComponent<TargetedProjectileComponent>(projectile);
            SetField(targeted, nameof(TargetedProjectileComponent.Target), deleted);

            var player = entities.SpawnEntity(null, MapCoordinates.Nullspace);
            var cassette = entities.AddComponent<CassettePlayerComponent>(player);
            SetField(cassette, nameof(CassettePlayerComponent.PlayPauseAction), (EntityUid?) deleted);
            SetField(cassette, nameof(CassettePlayerComponent.NextAction), (EntityUid?) deleted);
            SetField(cassette, nameof(CassettePlayerComponent.RestartAction), (EntityUid?) deleted);
            SetField(cassette, nameof(CassettePlayerComponent.AudioStream), (EntityUid?) deleted);

            entities.DeleteEntity(deleted);

            var targetedState = (TargetedProjectileComponentState) entities.GetComponentState(
                entities.EventBus,
                targeted,
                null,
                GameTick.Zero)!;
            var cassetteState = (CassettePlayerComponentState) entities.GetComponentState(
                entities.EventBus,
                cassette,
                null,
                GameTick.Zero)!;

            Assert.Multiple(() =>
            {
                Assert.That(targetedState.Target, Is.EqualTo(NetEntity.Invalid));
                Assert.That(cassetteState.PlayPauseAction, Is.EqualTo(NetEntity.Invalid));
                Assert.That(cassetteState.NextAction, Is.EqualTo(NetEntity.Invalid));
                Assert.That(cassetteState.RestartAction, Is.EqualTo(NetEntity.Invalid));
                Assert.That(cassetteState.AudioStream, Is.EqualTo(NetEntity.Invalid));
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void SetField<T>(object component, string name, T value)
    {
        component.GetType().GetField(name)!.SetValue(component, value);
    }
}
