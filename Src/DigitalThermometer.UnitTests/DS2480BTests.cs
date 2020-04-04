﻿using NUnit.Framework;

using DigitalThermometer.Hardware;

namespace DigitalThermometer.UnitTests
{
    [TestFixture]
    class DS2480BTests
    {
        [Test]
        public void CheckResetResponsePresencePulse()
        {
            Assert.AreEqual(OneWireBusResetResponse.PresencePulse, DS2480B.CheckResetResponse(0xCD));
            Assert.AreEqual(OneWireBusResetResponse.PresencePulse, DS2480B.CheckResetResponse(0xED));
        }

        [Test]
        public void CheckResetResponseNoPresencePulse()
        {
            Assert.AreEqual(OneWireBusResetResponse.NoPresencePulse, DS2480B.CheckResetResponse(0xCF));
        }
    }
}