using Content.Shared.CMU14.Speech;
using NUnit.Framework;

namespace Content.Tests.CMU.Accents;

[TestFixture]
public sealed class CMUAccentTextTest
{
    [TestCase("Hello", "allo", ExpectedResult = "Allo")]
    [TestCase("HELLO", "allo", ExpectedResult = "ALLO")]
    [TestCase("hello", "allo", ExpectedResult = "allo")]
    public string MatchCaseCopiesCapitalization(string source, string replacement)
        => CMUAccentText.MatchCase(source, replacement);

    [TestCase("running", ExpectedResult = "runnin'")]
    [TestCase("GETTING out of here", ExpectedResult = "GETTIN' out of here")]
    [TestCase("thing king bring", ExpectedResult = "thing king bring")]
    public string DropG(string message)
        => CMUAccentText.DropG(message);

    [TestCase("Sure.", ExpectedResult = "Sure, partner.")]
    [TestCase("Sure, partner.", ExpectedResult = "Sure, partner.")]
    [TestCase("", ExpectedResult = "")]
    public string AppendTag(string message)
        => CMUTexasAccentRules.AddPartner(message);

    [Test]
    public void ApplyWordsReplacesWholeWordsOnly()
    {
        var words = CMUAccentText.BuildWords(("cat", "dog"));
        Assert.That(CMUAccentText.ApplyWords(words, "cat concat Cat CAT"), Is.EqualTo("dog concat Dog DOG"));
    }
}
