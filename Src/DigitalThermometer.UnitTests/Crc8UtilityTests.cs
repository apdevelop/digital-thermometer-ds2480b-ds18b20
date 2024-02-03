using NUnit.Framework;

using DigitalThermometer.OneWire;

namespace DigitalThermometer.UnitTests
{
    [TestFixture]
    class Crc8UtilityTests
    {
        [Test]
        public void CalculateCrc8()
        {
            Assert.Multiple(() =>
            {
                Assert.That(Crc8Utility.CalculateCrc8(new byte[] { 0x28, 0x0C, 0xCE, 0xBE, 0x00, 0x00, 0x00, }), Is.EqualTo(0x67));
                Assert.That(Crc8Utility.CalculateCrc8(new byte[] { 0x28, 0x07, 0xBA, 0x78, 0x00, 0x00, 0x00, }), Is.EqualTo(0xDD));

                Assert.That(() => { Crc8Utility.CalculateCrc8(null); }, Throws.ArgumentNullException);
            });
        }
    }
}
