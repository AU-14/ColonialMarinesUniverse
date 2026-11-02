using Content.Server.CMU14.BugHelp;
using NUnit.Framework;

namespace Content.Tests.Server.CMU14
{
    public sealed class BugHelpTagsTest
    {
        [Test]
        public void PicksCategoryCaseInsensitive()
        {
            var (tags, malformed) = BugHelpSystem.ParseTags("Code:111, MAPPING:222", "mapping");

            Assert.That(tags, Is.EqualTo(new[] { "222" }));
            Assert.That(malformed, Is.Empty);
        }

        [Test]
        public void MultipleEntriesForOneCategoryAllApply()
        {
            var (tags, malformed) = BugHelpSystem.ParseTags("code:111,code:222", "code");

            Assert.That(tags, Is.EqualTo(new[] { "111", "222" }));
            Assert.That(malformed, Is.Empty);
        }

        [Test]
        public void MalformedEntriesAreSkippedAndReported()
        {
            var (tags, malformed) = BugHelpSystem.ParseTags("code:notanumber,,mapping:222,code", "code");

            Assert.That(tags, Is.Empty);
            Assert.That(malformed, Is.EqualTo(new[] { "code:notanumber", "code" }));
        }

        [Test]
        public void OtherCategoriesAreIgnoredSilently()
        {
            var (tags, malformed) = BugHelpSystem.ParseTags("mapping:222", "code");

            Assert.That(tags, Is.Empty);
            Assert.That(malformed, Is.Empty);
        }

        [Test]
        public void EmptyConfigYieldsNothing()
        {
            var (tags, malformed) = BugHelpSystem.ParseTags("", "code");

            Assert.That(tags, Is.Empty);
            Assert.That(malformed, Is.Empty);
        }
    }
}
