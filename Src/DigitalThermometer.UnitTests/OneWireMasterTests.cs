using System.Threading.Tasks;

using NUnit.Framework;

using DigitalThermometer.OneWire;

namespace DigitalThermometer.UnitTests
{
    [TestFixture]
    class OneWireMasterTests
    {
        [Test]
        public async Task ResetBusAsync()
        {
            var emulator = new ThermoStringEmulator(new[] { 0x91000000BED06928, });
            var busMaster = new OneWireMaster(emulator);
            var result = await busMaster.OpenAsync();

            Assert.AreEqual(OneWireBusResetResponse.PresencePulse, result);
        }

        [Test]
        public async Task ResetBusNoPresenceAsync()
        {
            var emulator = new ThermoStringEmulator(new ulong[] { });
            var busMaster = new OneWireMaster(emulator);
            var result = await busMaster.OpenAsync();

            Assert.AreEqual(OneWireBusResetResponse.NoPresencePulse, result);
        }

        [Test]
        public async Task SearchDevicesOnBusAsync()
        {
            const ulong device1 = 0x4D000000BE736128;
            const ulong device2 = 0x91000000BED06928;

            var emulator = new ThermoStringEmulator(new ulong[] { device1, device2, });
            var busMaster = new OneWireMaster(emulator);
            var result = await busMaster.OpenAsync();

            Assert.AreEqual(OneWireBusResetResponse.PresencePulse, result);

            var devices = await busMaster.SearchDevicesOnBusAsync();
            Assert.AreEqual(2, devices.Count);
            Assert.AreEqual(device1, devices[0]);
            Assert.AreEqual(device2, devices[1]);
        }

        [Test]
        public async Task PerformMeasureAsync()
        {
            var romCodes = new ulong[] { 0x4D000000BE736128, 0x91000000BED06928, };
            var emulator = new ThermoStringEmulator(romCodes);
            var busMaster = new OneWireMaster(emulator);
            var result = await busMaster.OpenAsync();
            Assert.AreEqual(OneWireBusResetResponse.PresencePulse, result);

            var measurements = await busMaster.PerformDS18B20TemperatureMeasurementAsync(romCodes);
            Assert.AreEqual(measurements.Count, romCodes.Length);
            Assert.AreEqual(25.9375, measurements[romCodes[0]].Temperature);
            Assert.AreEqual(25.9375, measurements[romCodes[1]].Temperature);
        }
    }
}
