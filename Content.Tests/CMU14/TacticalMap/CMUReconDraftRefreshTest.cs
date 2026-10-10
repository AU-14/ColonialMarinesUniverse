using Content.Client.CMU14.TacticalMap.Reconstruction;
using Content.Shared.CMU14.TacticalMap.Reconstruction;
using NUnit.Framework;

namespace Content.Tests.CMU14.TacticalMap;

[TestFixture]
public sealed class CMUReconDraftRefreshTest
{
    [Test]
    public void UnchangedMapCanPublishContactsAgainAfterAcknowledgement()
    {
        var draft = new CMUReconDraft();
        Assert.That(draft.Changed, Is.False);
        var first = draft.Send(17);
        Assert.That(first, Is.Not.Null);
        Assert.That(first!.Generation, Is.EqualTo(17));
        Assert.That(first.Additions, Is.Empty);
        Assert.That(first.Removals, Is.Empty);
        Assert.That(draft.Send(17), Is.Null, "Only one request can be outstanding.");
        Assert.That(draft.Acknowledge(new CMUReconSentMessage(first.RequestId, true, [])), Is.True);
        var second = draft.Send(17);
        Assert.That(second, Is.Not.Null);
        Assert.That(second!.RequestId, Is.GreaterThan(first.RequestId));
        Assert.That(second.Additions, Is.Empty);
        Assert.That(second.Removals, Is.Empty);
    }
}
