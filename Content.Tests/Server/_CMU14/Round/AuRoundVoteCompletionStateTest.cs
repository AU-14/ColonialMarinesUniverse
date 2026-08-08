#nullable enable

using Content.Server.AU14.Round;
using NUnit.Framework;

namespace Content.Tests.Server._CMU14.Round;

[TestFixture]
public sealed class AuRoundVoteCompletionStateTest
{
    [Test]
    public void SignalsCompletionOnlyAfterEveryRequiredBranchAndOnlyOnce()
    {
        var state = new AuRoundVoteCompletionState();
        state.Begin(7, AuRoundVoteBranch.Govfor | AuRoundVoteBranch.Opfor);

        Assert.Multiple(() =>
        {
            Assert.That(state.Complete(7, AuRoundVoteBranch.Opfor), Is.False);
            Assert.That(state.Complete(7, AuRoundVoteBranch.Opfor), Is.False);
            Assert.That(state.Complete(7, AuRoundVoteBranch.Govfor), Is.True);
            Assert.That(state.Complete(7, AuRoundVoteBranch.Govfor), Is.False);
        });
    }

    [Test]
    public void OptionalBranchesDoNotBlockCompletion()
    {
        var state = new AuRoundVoteCompletionState();
        state.Begin(3, AuRoundVoteBranch.Govfor);

        Assert.Multiple(() =>
        {
            Assert.That(state.Complete(3, AuRoundVoteBranch.Opfor), Is.False);
            Assert.That(state.Complete(3, AuRoundVoteBranch.Govfor), Is.True);
        });
    }

    [Test]
    public void OldGenerationCannotCompleteRestartedSequence()
    {
        var state = new AuRoundVoteCompletionState();
        state.Begin(10, AuRoundVoteBranch.Govfor | AuRoundVoteBranch.Opfor);
        Assert.That(state.Complete(10, AuRoundVoteBranch.Govfor), Is.False);

        state.Begin(11, AuRoundVoteBranch.Govfor);

        Assert.Multiple(() =>
        {
            Assert.That(state.Complete(10, AuRoundVoteBranch.Opfor), Is.False);
            Assert.That(state.Complete(11, AuRoundVoteBranch.Govfor), Is.True);
        });
    }
}
