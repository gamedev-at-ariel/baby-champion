using BabyChampion.Simulation;
using NUnit.Framework;

namespace BabyChampion.Tests.EditMode
{
    public class SeededRandomTests
    {
        [Test]
        public void SameSeed_GivesSameSequence()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void Range_StaysWithinBounds()
        {
            var random = new SeededRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                int value = random.Range(3, 9);
                Assert.That(value, Is.InRange(3, 8));
            }
        }

        [Test]
        public void ZeroSeed_DoesNotGetStuck()
        {
            var random = new SeededRandom(0);
            Assert.AreNotEqual(random.NextUInt(), random.NextUInt());
        }
    }
}
