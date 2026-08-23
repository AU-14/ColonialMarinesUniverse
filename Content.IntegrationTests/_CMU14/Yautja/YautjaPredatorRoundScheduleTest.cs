using Content.Server._CMU14.Yautja;

namespace Content.IntegrationTests._CMU14.Yautja;

[TestFixture]
public sealed class YautjaPredatorRoundScheduleTest
{
    [TestCase(3)]
    [TestCase(5)]
    public void ScheduleBecomesDueAfterExactRoundInterval(int interval)
    {
        var schedule = new YautjaPredatorRoundSchedule(interval);

        for (var roundId = 1; roundId < interval; roundId++)
            Assert.That(schedule.CountRound(roundId), Is.False);

        Assert.Multiple(() =>
        {
            Assert.That(schedule.CountRound(interval), Is.True);
            Assert.That(schedule.RoundsRemaining, Is.Zero);
            Assert.That(schedule.Due, Is.True);
        });
    }

    [Test]
    public void ScheduleIgnoresDuplicateAndOutOfOrderRounds()
    {
        var schedule = new YautjaPredatorRoundSchedule(3);

        Assert.Multiple(() =>
        {
            Assert.That(schedule.CountRound(0), Is.False);
            Assert.That(schedule.CountRound(2), Is.False);
            Assert.That(schedule.CountRound(2), Is.False);
            Assert.That(schedule.CountRound(1), Is.False);
            Assert.That(schedule.RoundsRemaining, Is.EqualTo(2));
        });

        Assert.That(schedule.CountRound(3), Is.False);
        Assert.That(schedule.CountRound(4), Is.True);
    }
}
