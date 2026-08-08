#nullable enable

using System.Collections.Generic;
using Content.Server.AU14.Round;
using Content.Server.Voting;
using NUnit.Framework;
using Robust.Shared.Player;

namespace Content.Tests.Server._CMU14.Round;

[TestFixture]
public sealed class AuRoundVoteSequenceTrackerTest
{
    [Test]
    public void RestartInvalidatesCallbacksBeforeCancellingTrackedVotes()
    {
        var tracker = new AuRoundVoteSequenceTracker();
        var sequence = tracker.Restart();
        var handle = new TestVoteHandle();
        var staleCallbackAccepted = false;
        handle.OnCancelled += _ => staleCallbackAccepted = tracker.IsCurrent(sequence);
        tracker.Track(handle);
        tracker.Running = true;

        var nextSequence = tracker.Restart();

        Assert.Multiple(() =>
        {
            Assert.That(nextSequence, Is.EqualTo(sequence + 1));
            Assert.That(handle.Cancelled, Is.True);
            Assert.That(staleCallbackAccepted, Is.False);
            Assert.That(tracker.Running, Is.False);
        });
    }

    private sealed class TestVoteHandle : IVoteHandle
    {
        public int Id => 1;
        public string Title => string.Empty;
        public string InitiatorText => string.Empty;
        public bool Finished { get; private set; }
        public bool Cancelled { get; private set; }
        public IReadOnlyDictionary<ICommonSession, int> CastVotes { get; } =
            new Dictionary<ICommonSession, int>();
        public IReadOnlyDictionary<object, int> VotesPerOption { get; } =
            new Dictionary<object, int>();

        public event VoteFinishedEventHandler OnFinished
        {
            add { }
            remove { }
        }

        public event VoteCancelledEventHandler OnCancelled = delegate { };

        public bool IsValidOption(int optionId)
        {
            return false;
        }

        public void CastVote(ICommonSession session, int? optionId)
        {
        }

        public void Cancel()
        {
            Finished = true;
            Cancelled = true;
            OnCancelled(this);
        }
    }
}
