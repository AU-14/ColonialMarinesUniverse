// cmu edit start
using Content.Shared.CMU14.Preferences;
using NUnit.Framework;

namespace Content.Tests.CMU14.Preferences;

[TestFixture]
public sealed class CMUCharacterNameTest
{
    [TestCase("Shannona A.W. 'Ace' Stroha", "Shannona Stroha")]
    [TestCase("Pvt. Shannona A.W. 'Ace' Stroha", "Pvt. Shannona Stroha")]
    [TestCase("Sgt. John 'Big Mike' Smith", "Sgt. John Smith")]
    [TestCase("John Smith", "John Smith")]
    [TestCase("Unknown", "Unknown")]
    public void StripNicknameKeepsRankAndFirstAndLastName(string name, string expected)
    {
        Assert.That(CMUCharacterName.StripNickname(name), Is.EqualTo(expected));
    }
}
// cmu edit end
