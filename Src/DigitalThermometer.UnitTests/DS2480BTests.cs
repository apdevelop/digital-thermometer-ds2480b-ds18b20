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
            Assert.Multiple(() =>
            {
                Assert.That(DS2480B.IsBusResetResponse(0xCD));
                Assert.That(DS2480B.IsBusResetResponse(0xED));
                Assert.That(DS2480B.IsBusResetResponse(0xCF));
                Assert.That(DS2480B.IsBusResetResponse(0xEF));
                Assert.That(DS2480B.IsBusResetResponse(0xCC));
                Assert.That(DS2480B.IsBusResetResponse(0xEC));
            });
        }

        [Test]
        public void CheckResetResponsePresencePulse()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DS2480B.GetBusResetResponse(0xCD), Is.EqualTo(OneWireBusResetResponse.PresencePulse));
                Assert.That(DS2480B.GetBusResetResponse(0xED), Is.EqualTo(OneWireBusResetResponse.PresencePulse)); // Bit 5 is reserved and undefined.
            });
        }

        [Test]
        public void CheckResetResponseNoPresencePulse()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DS2480B.GetBusResetResponse(0xCF), Is.EqualTo(OneWireBusResetResponse.NoPresencePulse));
                Assert.That(DS2480B.GetBusResetResponse(0xEF), Is.EqualTo(OneWireBusResetResponse.NoPresencePulse));
            });
        }

        [Test]
        public void CheckResetResponseBusShorted()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DS2480B.GetBusResetResponse(0xCC), Is.EqualTo(OneWireBusResetResponse.BusShorted));
                Assert.That(DS2480B.GetBusResetResponse(0xEC), Is.EqualTo(OneWireBusResetResponse.BusShorted));
            });
        }
    }
}
