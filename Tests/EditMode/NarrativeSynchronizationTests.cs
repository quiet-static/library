using NUnit.Framework;
using QuietStatic.Toolkit.Editor.Validation;

namespace QuietStatic.Tests.EditMode
{
    public sealed class NarrativeSynchronizationTests
    {
        [Test]
        public void SemanticJsonEquals_IgnoresFormatting()
        {
            Assert.That(
                NarrativeSynchronization.SemanticJsonEquals(
                    "{\n  \"id\": \"a b\", \"value\": 2\n}",
                    "{\"id\":\"a b\",\"value\":2}"),
                Is.True);
        }

        [Test]
        public void SemanticJsonEquals_DetectsContentChange()
        {
            Assert.That(
                NarrativeSynchronization.SemanticJsonEquals(
                    "{\"value\":2}",
                    "{\"value\":3}"),
                Is.False);
        }
    }
}
