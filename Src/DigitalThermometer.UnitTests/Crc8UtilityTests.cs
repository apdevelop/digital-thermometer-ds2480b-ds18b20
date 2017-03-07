using NUnit.Framework;

using DigitalThermometer.Hardware;

namespace DigitalThermometer.UnitTests
{
    [TestFixture]
    class Crc8UtilityTests
    {
        [Test]
        public void CalculateCrc8()
        {
            Assert.AreEqual(0x67, Crc8Utility.CalculateCrc8(new byte[] { 0x28, 0x0C, 0xCE, 0xBE, 0x00, 0x00, 0x00, }));
            Assert.AreEqual(0xDD, Crc8Utility.CalculateCrc8(new byte[] { 0x28, 0x07, 0xBA, 0x78, 0x00, 0x00, 0x00, }));

            Assert.That(() => { Crc8Utility.CalculateCrc8(null); }, Throws.ArgumentNullException);
        }
    }
}
