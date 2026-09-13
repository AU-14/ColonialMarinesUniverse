using Content.Server.CMU14.Threats;
using Content.Server.GameTicking;
using Content.Shared.CMU14.Threats;
using NUnit.Framework;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Tests.Server.CMU14.Round;

[TestFixture]
[NonParallelizable]
public sealed class ThreatVoteCooldownTest : ContentUnitTest
{
    private static readonly ProtoId<ThreatPrototype> ApeThreat = "ApeThreatCF";
    private static readonly ProtoId<ThreatPrototype> CultistThreat = "CultistThreatCF";
    private static readonly ProtoId<ThreatPrototype> XenoThreat = "XenoThreat";
    private IPrototypeManager _prototypes = default!;

    [OneTimeSetUp]
    public void InitializePrototypes()
    {
        IoCManager.Resolve<ISerializationManager>().Initialize();
        _prototypes = IoCManager.Resolve<IPrototypeManager>();
        _prototypes.Initialize();
        _prototypes.LoadString("""
            - type: threat
              id: ApeThreatCF
              voteCooldownGroup: Ape
            - type: threat
              id: ApeThreatDS
              voteCooldownGroup: Ape
            - type: threat
              id: XenoThreat
              allowConsecutiveVotes: true
            - type: threat
              id: XenoThreatCF
              allowConsecutiveVotes: true
            - type: threat
              id: CultistThreatCF
            - type: threat
              id: WendigoThreatCF
            """);
        _prototypes.ResolveResults();
    }

    [TestCase("ApeThreatCF", "ApeThreatDS")]
    [TestCase("ApeThreatDS", "ApeThreatCF")]
    [TestCase("CultistThreatCF", "CultistThreatCF")]
    [TestCase("WendigoThreatCF", "WendigoThreatCF")]
    public void PreviousWinnerIsBlockedForExactlyOneRound(string winnerId, string candidateId)
    {
        var vote = new ThreatVoteSystem();
        var candidate = _prototypes.Index<ThreatPrototype>(candidateId);
        Assert.That(vote.CanVoteForThreat(candidate), Is.True);

        vote.RecordVotedThreat(_prototypes.Index<ThreatPrototype>(winnerId));
        vote.OnRunLevelChanged(new(GameRunLevel.InRound, GameRunLevel.PostRound));
        vote.OnRunLevelChanged(new(GameRunLevel.PostRound, GameRunLevel.PreRoundLobby));
        Assert.That(vote.CanVoteForThreat(candidate), Is.False);
        Assert.That(vote.CanVoteForThreat(_prototypes.Index(XenoThreat)), Is.True);

        vote.OnRunLevelChanged(new(GameRunLevel.PreRoundLobby, GameRunLevel.InRound));
        Assert.That(vote.CanVoteForThreat(candidate), Is.False);

        // A round without a completed vote still consumes the cooldown.
        vote.OnRunLevelChanged(new(GameRunLevel.InRound, GameRunLevel.PostRound));
        Assert.That(vote.CanVoteForThreat(candidate), Is.True);
    }

    [TestCase("XenoThreat")]
    [TestCase("XenoThreatCF")]
    public void BasicXenosCanRepeat(string threatId)
    {
        var vote = new ThreatVoteSystem();
        var xeno = _prototypes.Index<ThreatPrototype>(threatId);
        vote.RecordVotedThreat(xeno);
        vote.OnRunLevelChanged(new(GameRunLevel.InRound, GameRunLevel.PostRound));

        Assert.That(vote.CanVoteForThreat(xeno), Is.True);
    }

    [Test]
    public void DirectRestartPreservesCooldownAndNextWinnerReplacesIt()
    {
        var vote = new ThreatVoteSystem();
        var ape = _prototypes.Index(ApeThreat);
        var cultist = _prototypes.Index(CultistThreat);
        vote.RecordVotedThreat(ape);
        vote.OnRunLevelChanged(new(GameRunLevel.InRound, GameRunLevel.PreRoundLobby));

        Assert.That(vote.CanVoteForThreat(ape), Is.False);
        Assert.That(vote.CanVoteForThreat(cultist), Is.True);

        vote.OnRunLevelChanged(new(GameRunLevel.PreRoundLobby, GameRunLevel.InRound));
        vote.RecordVotedThreat(cultist);
        vote.OnRunLevelChanged(new(GameRunLevel.InRound, GameRunLevel.PostRound));

        Assert.That(vote.CanVoteForThreat(ape), Is.True);
        Assert.That(vote.CanVoteForThreat(cultist), Is.False);
    }
}
