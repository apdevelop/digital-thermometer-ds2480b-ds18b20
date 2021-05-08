using NUnit.Framework;

using DigitalThermometer.OneWire;

namespace DigitalThermometer.UnitTests
{
    [TestFixture]
    class DS2480BTests
    {
        [Test]
        public void IsBusResetResponse()
        {
            Assert.IsTrue(DS2480B.IsBusResetResponse(0xCD));
            Assert.IsTrue(DS2480B.IsBusResetResponse(0xED));
            Assert.IsTrue(DS2480B.IsBusResetResponse(0xCF));
            Assert.IsTrue(DS2480B.IsBusResetResponse(0xEF));
            Assert.IsTrue(DS2480B.IsBusResetResponse(0xCC));
            Assert.IsTrue(DS2480B.IsBusResetResponse(0xEC));
        }

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
