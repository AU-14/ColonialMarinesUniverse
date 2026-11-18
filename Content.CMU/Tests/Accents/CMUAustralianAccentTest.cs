using Content.Shared.CMU14.Speech;
using NUnit.Framework;

namespace Content.Tests.CMU.Accents;

[TestFixture]
public sealed class CMUAustralianAccentTest
{
    [TestCase("Hello friend, thanks for the help!", ExpectedResult = "G'day mate, ta for the help!")]
    [TestCase("The doctor said yes.", ExpectedResult = "The doc said yes.")]
    public string Apply(string message)
        => CMUAustralianAccentRules.Apply(message);

    [TestCase("That's great.", ExpectedResult = "That's great, mate.")]
    [TestCase("Sure, mate", ExpectedResult = "Sure, mate")]
    public string AddMate(string message)
        => CMUAustralianAccentRules.AddMate(message);

    // an accent can be applied twice (directly and through an inventory relay), so a second pass must change nothing
    [Test]
    public void SecondPassChangesNothing()
    {
        foreach (var sample in new[] { "Hello friend, thanks for the help!", "What the hell is this?", "I was running to the colony", "He is getting something", "Hi, I think that mother is nothing", "Um, uh, er, em" })
        {
            var once = CMUAustralianAccentRules.Apply(sample);
            Assert.That(CMUAustralianAccentRules.Apply(once), Is.EqualTo(once), sample);
        }
    }
}
