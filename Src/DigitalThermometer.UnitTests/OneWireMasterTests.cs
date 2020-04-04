using System;

using NUnit.Framework;

using DigitalThermometer.Hardware;

namespace DigitalThermometer.UnitTests
{
    [TestFixture]
    class OneWireMasterTests
    {
        [Test]
        public void ResetBus()
        {
            var emulator = new ThermoStringEmulator(new[] { 0x91000000BED06928, });
            var busMaster = new OneWireMaster(emulator);
            var result = busMaster.Open();

            Assert.AreEqual(OneWireBusResetResponse.PresencePulse, result);
        }

        [Test]
        public void ResetBusNoPresense()
        {
            var emulator = new ThermoStringEmulator(new ulong[] { });
            var busMaster = new OneWireMaster(emulator);
            var result = busMaster.Open();

            Assert.AreEqual(OneWireBusResetResponse.NoPresencePulse, result);
        }

        [Test]
        public void SearchDevicesOnBus()
        {
            const ulong device1 = 0x4D000000BE736128;
            const ulong device2 = 0x91000000BED06928;

            var emulator = new ThermoStringEmulator(new ulong[] { device1, device2, });
            var busMaster = new OneWireMaster(emulator);
            var result = busMaster.Open();

            Assert.AreEqual(OneWireBusResetResponse.PresencePulse, result);

            var devices = busMaster.SearchDevicesOnBus();
            Assert.AreEqual(2, devices.Count);
            Assert.AreEqual(device1, devices[0]);
            Assert.AreEqual(device2, devices[1]);
        }

        [Test]
        public void PerformMeasure()
        {
            var romCodes = new ulong[] { 0x4D000000BE736128, 0x91000000BED06928, };
            var emulator = new ThermoStringEmulator(romCodes);
            var busMaster = new OneWireMaster(emulator);
            var result = busMaster.Open();
            Assert.AreEqual(OneWireBusResetResponse.PresencePulse, result);

            var measurements = busMaster.PerformDS18B20TemperatureMeasure(romCodes);
            Assert.AreEqual(measurements.Count, romCodes.Length);
            Assert.AreEqual(25.9375, measurements[romCodes[0]]);
            Assert.AreEqual(25.9375, measurements[romCodes[1]]);
        }
    }
}