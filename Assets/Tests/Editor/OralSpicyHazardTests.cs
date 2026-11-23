using NUnit.Framework;

namespace Vampire.Tests.Editor
{
    public sealed class OralSpicyHazardTests
    {
        [TestCase(0f, false, 0)]
        [TestCase(6.999f, false, 0)]
        [TestCase(7f, true, 0)]
        [TestCase(7.999f, true, 0)]
        [TestCase(8f, true, 1)]
        [TestCase(12.5f, true, 5)]
        [TestCase(16.999f, true, 9)]
        [TestCase(17f, false, 10)]
        [TestCase(27f, false, 10)]
        public void TongueHeatUsesASevenSecondWarningAndTenWholeSecondTicks(float age, bool hot, int pulses)
        {
            Assert.AreEqual(hot, OralSpicyHazard.IsHot(age));
            Assert.AreEqual(pulses, OralSpicyHazard.PulsesThrough(age));
        }
        [Test]
        public void LongOrRepeatedFramesCannotAddAnEleventhTick()
        {
            int previous=0, applied=0;
            foreach(float time in new[]{.1f, 7.1f, 8.2f, 8.2f, 12.8f, 17.4f, 21f})
            {
                int due=OralSpicyHazard.PulsesThrough(time);
                applied+=due-previous;previous=due;
            }
            Assert.AreEqual(10, applied);
            Assert.AreEqual(1f, applied * OralSpicyHazard.HealthFraction, .0001f);
        }
    }
}
