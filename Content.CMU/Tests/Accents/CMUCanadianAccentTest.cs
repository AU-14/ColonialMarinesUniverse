using Content.Shared.CMU14.Speech;
using NUnit.Framework;

namespace Content.Tests.CMU.Accents;

[TestFixture]
public sealed class CMUCanadianAccentTest
{
    [TestCase("What the hell is this?", ExpectedResult = "What the heck is this?")]
    [TestCase("The doctor said yes.", ExpectedResult = "The doc said yup.")]
    public string Apply(string message)
        => CMUCanadianAccentRules.Apply(message);

    [TestCase("That's great.", ExpectedResult = "That's great, eh.")]
    [TestCase("Sure, eh?", ExpectedResult = "Sure, eh?")]
    public string AddEh(string message)
        => CMUCanadianAccentRules.AddEh(message);

    // an accent can be applied twice (directly and through an inventory relay), so a second pass must change nothing
    [Test]
    public void SecondPassChangesNothing()
    {
        foreach (var sample in new[] { "Hello friend, thanks for the help!", "What the hell is this?", "I was running to the colony", "He is getting something", "Hi, I think that mother is nothing", "Um, uh, er, em" })
        {
            var once = CMUCanadianAccentRules.Apply(sample);
            Assert.That(CMUCanadianAccentRules.Apply(once), Is.EqualTo(once), sample);
        }
    }
}
