using NUnit.Framework;

using DigitalThermometer.OneWire;

namespace DigitalThermometer.UnitTests
{
    [TestFixture]
    class DS2480BTests
    {
        [Test]
        public void CheckResetResponsePresencePulse()
        {
            Assert.AreEqual(OneWireBusResetResponse.PresencePulse, DS2480B.GetBusResetResponse(0xCD));
            Assert.AreEqual(OneWireBusResetResponse.PresencePulse, DS2480B.GetBusResetResponse(0xED)); // Bit 5 is reserved and undefined.
        }

        [Test]
        public void CheckResetResponseNoPresencePulse()
        {
            Assert.AreEqual(OneWireBusResetResponse.NoPresencePulse, DS2480B.GetBusResetResponse(0xCF));
            Assert.AreEqual(OneWireBusResetResponse.NoPresencePulse, DS2480B.GetBusResetResponse(0xEF));
        }

        [Test]
        public void CheckResetResponseBusShorted()
        {
            Assert.AreEqual(OneWireBusResetResponse.BusShorted, DS2480B.GetBusResetResponse(0xCC));
            Assert.AreEqual(OneWireBusResetResponse.BusShorted, DS2480B.GetBusResetResponse(0xEC));
        }
    }
}