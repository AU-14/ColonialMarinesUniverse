using Content.Shared.CMU14.Speech;
using NUnit.Framework;

namespace Content.Tests.CMU.Accents;

[TestFixture]
public sealed class CMUAppalachianAccentTest
{
    [TestCase("Hello, he's running", ExpectedResult = "Howdy, he's a-runnin'")]
    [TestCase("SHE WAS RUNNING", ExpectedResult = "SHE WAS A-RUNNIN'")]
    [TestCase("He is nothing", ExpectedResult = "He is nothin'")]
    public string Apply(string message)
        => CMUAppalachianAccentRules.Apply(message);

    // an accent can be applied twice (directly and through an inventory relay), so a second pass must change nothing
    [Test]
    public void SecondPassChangesNothing()
    {
        foreach (var sample in new[] { "Hello friend, thanks for the help!", "What the hell is this?", "I was running to the colony", "He is getting something", "Hi, I think that mother is nothing", "Um, uh, er, em" })
        {
            var once = CMUAppalachianAccentRules.Apply(sample);
            Assert.That(CMUAppalachianAccentRules.Apply(once), Is.EqualTo(once), sample);
        }
    }
}
