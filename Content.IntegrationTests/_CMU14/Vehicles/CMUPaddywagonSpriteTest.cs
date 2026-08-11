#nullable enable
using Content.IntegrationTests.Fixtures;
using Robust.Client.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests._CMU14.Vehicles;

[TestFixture]
public sealed class CMUPaddywagonSpriteTest : GameTest
{
    private static readonly EntProtoId Paddywagon = "VehicleCMBPoliceVan";
    private static readonly ResPath PaddywagonRsi = new("/Textures/_CMU14/Structures/vehicles/marshalpaddywagon.rsi");

    [Test]
    public async Task ConfiguredSpriteStatesResolve()
    {
        await Client.WaitAssertion(() =>
        {
            var uid = CEntMan.SpawnEntity(Paddywagon, MapCoordinates.Nullspace);
            try
            {
                var sprite = CEntMan.GetComponent<SpriteComponent>(uid);

                Assert.Multiple(() =>
                {
                    Assert.That(sprite.BaseRSI?.Path, Is.EqualTo(PaddywagonRsi));
                    Assert.That(sprite.BaseRSI?.TryGetState("van_base", out _), Is.True);
                    Assert.That(sprite.BaseRSI?.TryGetState("damaged_frame", out _), Is.True);
                    Assert.That(sprite.BaseRSI?.TryGetState("wheels_1", out _), Is.True);
                });
            }
            finally
            {
                CEntMan.DeleteEntity(uid);
            }
        });
    }
}
