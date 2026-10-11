using BabyChampion.Game;
using NUnit.Framework;

namespace BabyChampion.Tests.EditMode
{
    public class DevArgsTests
    {
        [Test]
        public void CommandLine_ParsesAllArguments()
        {
            var args = DevArgs.FromCommandLine(new[] { "BabyChampion.exe", "-level", "10", "-timescale", "4", "-seed", "123", "-skipIntro" });
            Assert.AreEqual(10, args.Level);
            Assert.AreEqual(4f, args.TimeScale);
            Assert.AreEqual(123, args.Seed);
            Assert.IsTrue(args.SkipIntro);
        }

        [Test]
        public void CommandLine_IgnoresUnknownAndInvalidValues()
        {
            var args = DevArgs.FromCommandLine(new[] { "-batchmode", "-level", "abc", "-timescale", "0" });
            Assert.IsNull(args.Level);
            Assert.IsNull(args.TimeScale);
            Assert.IsFalse(args.SkipIntro);
        }

        [Test]
        public void Url_ParsesQueryParameters()
        {
            var args = DevArgs.FromUrl("https://example.itch.io/baby-champion/index.html?level=8&timescale=2.5&skipIntro#top");
            Assert.AreEqual(8, args.Level);
            Assert.AreEqual(2.5f, args.TimeScale);
            Assert.IsNull(args.Seed);
            Assert.IsTrue(args.SkipIntro);
        }

        [Test]
        public void Url_WithoutQuery_GivesDefaults()
        {
            var args = DevArgs.FromUrl("https://example.com/index.html");
            Assert.IsNull(args.Level);
            Assert.IsFalse(args.SkipIntro);
        }
    }
}
