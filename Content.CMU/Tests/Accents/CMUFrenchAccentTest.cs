using Content.Shared.CMU14.Speech;
using NUnit.Framework;

namespace Content.Tests.CMU.Accents;

[TestFixture]
public sealed class CMUFrenchAccentTest
{
    [TestCase("Hello friend, thanks for the help!", ExpectedResult = "Allo cher, merci for ze 'elp !")]
    [TestCase("Hi, I think that mother is nothing", ExpectedResult = "Allo, I tink zat mother is noting")]
    [TestCase("Um, uh, er, em", ExpectedResult = "Euh, euh, euh, euh")]
    public string Apply(string message)
        => CMUFrenchAccentRules.Apply(message);

    // an accent can be applied twice (directly and through an inventory relay), so a second pass must change nothing
    [Test]
    public void SecondPassChangesNothing()
    {
        foreach (var sample in new[] { "Hello friend, thanks for the help!", "What the hell is this?", "I was running to the colony", "He is getting something", "Hi, I think that mother is nothing", "Um, uh, er, em" })
        {
            var once = CMUFrenchAccentRules.Apply(sample);
            Assert.That(CMUFrenchAccentRules.Apply(once), Is.EqualTo(once), sample);
        }
    }
}
