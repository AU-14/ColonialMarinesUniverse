using System.Numerics;
using Content.Client._CMU14.ZLevels.Core;
using NUnit.Framework;

namespace Content.Tests.Client._CMU14.ZLevels;

[TestFixture]
public sealed class VisibleEntityScreenLabelLifetimeTest
{
    [Test]
    public void LabelsSurviveWorldPassAndAreConsumedByScreenPass()
    {
        var store = new CMUZLevelScreenLabelStore();
        var labels = store.BeginWorldPass(42);
        labels.Add(new Vector2(10f, 20f));

        Assert.That(store.ConsumeScreenPass(42), Is.EqualTo(new[] { new Vector2(10f, 20f) }));
        Assert.That(store.ConsumeScreenPass(42), Is.Empty);
    }

    [Test]
    public void LabelsAreScopedToViewport()
    {
        var store = new CMUZLevelScreenLabelStore();
        store.BeginWorldPass(1).Add(new Vector2(1f, 1f));
        store.BeginWorldPass(2).Add(new Vector2(2f, 2f));

        Assert.That(store.ConsumeScreenPass(2), Is.EqualTo(new[] { new Vector2(2f, 2f) }));
        Assert.That(store.ConsumeScreenPass(1), Is.EqualTo(new[] { new Vector2(1f, 1f) }));
    }
}
