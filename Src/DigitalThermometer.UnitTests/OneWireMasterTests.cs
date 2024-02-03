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

            Assert.That(result, Is.EqualTo(OneWireBusResetResponse.PresencePulse));
        }

        [Test]
        public async Task ResetBusNoPresenceAsync()
        {
            var emulator = new ThermoStringEmulator(new ulong[] { });
            var busMaster = new OneWireMaster(emulator);
            var result = await busMaster.OpenAsync();

            Assert.That(result, Is.EqualTo(OneWireBusResetResponse.NoPresencePulse));
        }

        [Test]
        public async Task SearchDevicesOnBusAsync()
        {
            const ulong device1 = 0x4D000000BE736128;
            const ulong device2 = 0x91000000BED06928;

            var emulator = new ThermoStringEmulator(new ulong[] { device1, device2, });
            var busMaster = new OneWireMaster(emulator);
            var result = await busMaster.OpenAsync();

            Assert.That(result, Is.EqualTo(OneWireBusResetResponse.PresencePulse));

            var devices = await busMaster.SearchDevicesOnBusAsync();
            Assert.That(devices, Has.Count.EqualTo(2));
            Assert.Multiple(() =>
            {
                Assert.That(devices[0], Is.EqualTo(device1));
                Assert.That(devices[1], Is.EqualTo(device2));
            });
        }

        [Test]
        public async Task PerformMeasureAsync()
        {
            var romCodes = new ulong[] { 0x4D000000BE736128, 0x91000000BED06928, };
            var emulator = new ThermoStringEmulator(romCodes);
            var busMaster = new OneWireMaster(emulator);
            var result = await busMaster.OpenAsync();
            Assert.That(result, Is.EqualTo(OneWireBusResetResponse.PresencePulse));

            var measurements = await busMaster.PerformDS18B20TemperatureMeasurementAsync(romCodes);
           
            Assert.Multiple(() =>
            {
                Assert.That(romCodes, Has.Length.EqualTo(measurements.Count));
                Assert.That(measurements[romCodes[0]].Temperature, Is.EqualTo(25.9375));
                Assert.That(measurements[romCodes[1]].Temperature, Is.EqualTo(25.9375));
            });
        }
    }
}
