using System;

using NUnit.Framework;

using DigitalThermometer.OneWire;

namespace DigitalThermometer.UnitTests
{
    [TestFixture]
    class DS18B20Tests
    {
        [Test]
        public void DecodeTemperature12bit()
        {
            Assert.Multiple(() =>
            {
                // TEMPERATURE/DATA RELATIONSHIP Table 2
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x07D0), Is.EqualTo(+125.0));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x0550), Is.EqualTo(+85.0));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x0191), Is.EqualTo(+25.0625));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x00A2), Is.EqualTo(+10.125));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x00A0), Is.EqualTo(+10.0));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x0008), Is.EqualTo(+0.5));

                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x0000), Is.EqualTo(0.0));

                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0xFFFF), Is.EqualTo(-0.0625));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0xFFF8), Is.EqualTo(-0.5));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0xFF5E), Is.EqualTo(-10.125));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0xFE6F), Is.EqualTo(-25.0625));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0xFC90), Is.EqualTo(-55.0));

                Assert.That(DS18B20.Scratchpad.DecodeTemperature(DS18B20.PowerOnTemperatureCode), Is.EqualTo(DS18B20.PowerOnTemperature));

                Assert.That(() => { DS18B20.Scratchpad.DecodeTemperature(0x07FF); }, Throws.Exception.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => { DS18B20.Scratchpad.DecodeTemperature(0xF000); }, Throws.Exception.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void DecodeTemperature()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x01C0), Is.EqualTo(28.0));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x01C0, DS18B20.ThermometerResolution.Resolution12bit), Is.EqualTo(28.0));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x01C0, DS18B20.ThermometerResolution.Resolution11bit), Is.EqualTo(28.0));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x01C0, DS18B20.ThermometerResolution.Resolution10bit), Is.EqualTo(28.0));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x01C0, DS18B20.ThermometerResolution.Resolution9bit), Is.EqualTo(28.0));

                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x0000, DS18B20.ThermometerResolution.Resolution9bit), Is.EqualTo(0.0));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x0008, DS18B20.ThermometerResolution.Resolution9bit), Is.EqualTo(+0.5));
                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0xFFF8, DS18B20.ThermometerResolution.Resolution9bit), Is.EqualTo(-0.5));

                Assert.That(DS18B20.Scratchpad.DecodeTemperature(0x0004, DS18B20.ThermometerResolution.Resolution9bit), Is.EqualTo(0.0)); // Real resolution = 0.5C
            });
        }

        [Test]
        public void EncodeTemperature12bit()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DS18B20.EncodeTemperature12bit(+125.0), Is.EqualTo(0x07D0));
                Assert.That(DS18B20.EncodeTemperature12bit(+0.5), Is.EqualTo(0x0008));

                Assert.That(DS18B20.EncodeTemperature12bit(0.0), Is.EqualTo(0x0000));

                Assert.That(DS18B20.EncodeTemperature12bit(-0.5), Is.EqualTo(0xFFF8));
                Assert.That(DS18B20.EncodeTemperature12bit(-55.0), Is.EqualTo(0xFC90));

                Assert.That(DS18B20.EncodeTemperature12bit(DS18B20.PowerOnTemperature), Is.EqualTo(DS18B20.PowerOnTemperatureCode));
            });
        }

        [Test]
        public void GetTemperatureCode()
        {
            var scratchpad = new DS18B20.Scratchpad(new byte[] { 0x50, 0x05, 0x00, 0x00, 0x00, 0xFF, 0x0C, 0x10, 0xD6 });
            Assert.Multiple(() =>
            {
                Assert.That(scratchpad.TemperatureRawData, Is.EqualTo(0x0550));
                Assert.That(scratchpad.IsPowerOnTemperature, Is.True);
            });
        }

        [Test]
        public void GetAlarmTriggerRegisters()
        {
            var scratchpad = new DS18B20.Scratchpad(new byte[] { 0xB4, 0x01, 0x4B, 0x46, 0x7F, 0xFF, 0x0C, 0x10, 0x8E });
            Assert.Multiple(() =>
            {
                Assert.That(scratchpad.HighAlarmTemperature, Is.EqualTo(75));
                Assert.That(scratchpad.LowAlarmTemperature, Is.EqualTo(70));
            });
        }

        [Test]
        public void GetThermometerResolution()
        {
            var scratchpad = new DS18B20.Scratchpad(new byte[] { 0x55, 0x01, 0x4B, 0x46, 0x7F, 0xFF, 0x0B, 0x10, 0xD0 });

            Assert.That(scratchpad.ThermometerActualResolution, Is.EqualTo(DS18B20.ThermometerResolution.Resolution12bit));
        }

        [Test]
        public void EncodeScratchpad12bit()
        {
            var temperatureValue = DS18B20.Scratchpad.DecodeTemperature(0x0155);
            var scratchpad = DS18B20.Scratchpad.EncodeScratchpad(temperatureValue, 0x4B, 0x46);
            Assert.Multiple(() =>
            {
                Assert.That(scratchpad[0], Is.EqualTo(0x55));
                Assert.That(scratchpad[1], Is.EqualTo(0x01));
                Assert.That(scratchpad[2], Is.EqualTo(0x4B));
                Assert.That(scratchpad[3], Is.EqualTo(0x46));
                Assert.That(scratchpad[4], Is.EqualTo(0x7F));
            });
        }

        [Test]
        public void IsValidTemperatureCode()
        {
            Assert.Multiple(() =>
            {
                Assert.That(DS18B20.IsValidTemperatureCode(0x0550), Is.True);
                Assert.That(DS18B20.IsValidTemperatureCode(0x0000), Is.True);
                Assert.That(DS18B20.IsValidTemperatureCode(0xFFF8), Is.True);

                Assert.That(DS18B20.IsValidTemperatureCode(0x07FF), Is.False);
                Assert.That(DS18B20.IsValidTemperatureCode(0xF000), Is.False);
            });
        }

        [TestCase("28341BF802000001")]
        [TestCase("280DBA7800000012")]
        [TestCase("28ABCE780000008E")]
        [TestCase("28FCB078000000EA")]
        [TestCase("286173BE0000004D")]
        [TestCase("2869D0BE00000091")]
        [TestCase("28 16 E1 74 00 00 00 46")]
        [TestCase("28 1F 7B BE 00 00 00 F8")]
        public void CalculateRomCodeCrc8(string romCodeString)
        {
            Assert.That(Crc8Utility.CalculateCrc8(BitConverter.GetBytes(Utils.RomCodeFromLEString(romCodeString))), Is.EqualTo(0x00));
        }

        [Test]
        public void CheckScratchpad()
        {
            Assert.Multiple(() =>
            {
                Assert.That(new DS18B20.Scratchpad(new byte[] { 0x50, 0x05, 0x4B, 0x46, 0x7F, 0xFF, 0x0C, 0x10, 0x1C, }).IsValidCrc, Is.True);
                Assert.That(new DS18B20.Scratchpad(new byte[] { 0x9E, 0x01, 0x4B, 0x46, 0x7F, 0xFF, 0x02, 0x10, 0x56, }).IsValidCrc, Is.True);

                Assert.That(() => { new DS18B20.Scratchpad(null); }, Throws.ArgumentNullException);
                Assert.That(() => { new DS18B20.Scratchpad(new byte[0]); }, Throws.ArgumentException);
            });
        }

        [Test]
        public void ScratchpadFromAbsentDeviceTest()
        {
            // When processing response from absent device
            var scratchpad = new DS18B20.Scratchpad(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, });
            Assert.Multiple(() =>
            {
                Assert.That(scratchpad.IsValidCrc, Is.False);
                Assert.That(scratchpad.Temperature.HasValue, Is.False);
                Assert.That(scratchpad.ThermometerActualResolution.HasValue, Is.False);
            });
        }

        [TestCase("28341BF802000001")]
        [TestCase("280DBA7800000012")]
        [TestCase("28ABCE780000008E")]
        [TestCase("28FCB078000000EA")]
        public void RomCodeToLEString(string romCodeString)
        {
            var romCode = Utils.RomCodeFromLEString(romCodeString);
            var romCodeStringNew = Utils.RomCodeToLEString(romCode);
            Assert.That(romCodeStringNew, Is.EqualTo(romCodeString));
        }
    }
}
