using Content.Shared.CMU14.Speech;
using NUnit.Framework;

namespace Content.Tests.CMU.Accents;

[TestFixture]
public sealed class CMUIrishAccentTest
{
    [TestCase("Hello friend, thanks for the help!", ExpectedResult = "Hello pal, thanks for the help!")]
    [TestCase("Hi, I think that mother is nothing", ExpectedResult = "Howya, I think that mother is nothing")]
    public string Apply(string message)
        => CMUIrishAccentRules.Apply(message);

    // an accent can be applied twice (directly and through an inventory relay), so a second pass must change nothing
    [Test]
    public void SecondPassChangesNothing()
    {
        foreach (var sample in new[] { "Hello friend, thanks for the help!", "What the hell is this?", "I was running to the colony", "He is getting something", "Hi, I think that mother is nothing", "Um, uh, er, em" })
        {
            var once = CMUIrishAccentRules.Apply(sample);
            Assert.That(CMUIrishAccentRules.Apply(once), Is.EqualTo(once), sample);
        }
    }
}
