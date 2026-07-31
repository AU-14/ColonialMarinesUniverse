using Content.Server._AU14.Insurgency;
using Content.Shared._AU14.Insurgency;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests._AU14.Insurgency;

[TestFixture]
public sealed class InsurgencyFactionValidatorTest
{
    [Test]
    public void NestedPayloadValuesAndCollectionsAreBounded()
    {
        var faction = new FactionDefinition
        {
            Metadata = new FactionMetadata { Title = new string('x', FactionDefinition.MaxTitleLength + 20) },
            Economy = new FactionEconomy
            {
                DollarsToPointsRate = -50,
                PointsSubmissions =
                {
                    new PointsSubmissionEntry
                    {
                        Entity = new EntProtoId("TestEntity"),
                        AmountPerPoint = -100,
                        PointsPerItem = int.MaxValue,
                    },
                },
            },
        };

        for (var i = 0; i < FactionDefinition.MaxPlaceableEntities + 10; i++)
            faction.CellKit.PlaceableEntities.Add(new EntProtoId("TestEntity"));

        InsurgencyFactionValidator.Sanitize(faction);

        Assert.Multiple(() =>
        {
            Assert.That(faction.Metadata.Title, Has.Length.EqualTo(FactionDefinition.MaxTitleLength));
            Assert.That(faction.Economy.DollarsToPointsRate, Is.EqualTo(InsurgencyFactionValidator.MinDollarsToPointsRate));
            Assert.That(faction.Economy.PointsSubmissions[0].AmountPerPoint, Is.EqualTo(1));
            Assert.That(faction.Economy.PointsSubmissions[0].PointsPerItem, Is.EqualTo(FactionDefinition.MaxSubmissionRatio));
            Assert.That(faction.CellKit.PlaceableEntities, Has.Count.EqualTo(FactionDefinition.MaxPlaceableEntities));
        });
    }
}
