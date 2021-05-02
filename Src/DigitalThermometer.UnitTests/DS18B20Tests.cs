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
            // TEMPERATURE/DATA RELATIONSHIP Table 2
            Assert.AreEqual(+125.0, DS18B20.Scratchpad.DecodeTemperature12bit(0x07D0));
            Assert.AreEqual(+85.0, DS18B20.Scratchpad.DecodeTemperature12bit(0x0550));
            Assert.AreEqual(+25.0625, DS18B20.Scratchpad.DecodeTemperature12bit(0x0191));
            Assert.AreEqual(+10.125, DS18B20.Scratchpad.DecodeTemperature12bit(0x00A2));
            Assert.AreEqual(+10.0, DS18B20.Scratchpad.DecodeTemperature12bit(0x00A0));
            Assert.AreEqual(+0.5, DS18B20.Scratchpad.DecodeTemperature12bit(0x0008));

            Assert.AreEqual(0.0, DS18B20.Scratchpad.DecodeTemperature12bit(0x0000));

            Assert.AreEqual(-0.0625, DS18B20.Scratchpad.DecodeTemperature12bit(0xFFFF));
            Assert.AreEqual(-0.5, DS18B20.Scratchpad.DecodeTemperature12bit(0xFFF8));
            Assert.AreEqual(-10.125, DS18B20.Scratchpad.DecodeTemperature12bit(0xFF5E));
            Assert.AreEqual(-25.0625, DS18B20.Scratchpad.DecodeTemperature12bit(0xFE6F));
            Assert.AreEqual(-55.0, DS18B20.Scratchpad.DecodeTemperature12bit(0xFC90));

            Assert.AreEqual(DS18B20.PowerOnTemperature, DS18B20.Scratchpad.DecodeTemperature12bit(DS18B20.PowerOnTemperatureCode));

            Assert.That(() => { DS18B20.Scratchpad.DecodeTemperature12bit(0x07FF); }, Throws.Exception.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => { DS18B20.Scratchpad.DecodeTemperature12bit(0xF000); }, Throws.Exception.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void EncodeTemperature12bit()
        {
            Assert.AreEqual(0x07D0, DS18B20.EncodeTemperature12bit(+125.0));
            Assert.AreEqual(0x0008, DS18B20.EncodeTemperature12bit(+0.5));

            Assert.AreEqual(0x0000, DS18B20.EncodeTemperature12bit(0.0));

            Assert.AreEqual(0xFFF8, DS18B20.EncodeTemperature12bit(-0.5));
            Assert.AreEqual(0xFC90, DS18B20.EncodeTemperature12bit(-55.0));

            Assert.AreEqual(DS18B20.PowerOnTemperatureCode, DS18B20.EncodeTemperature12bit(DS18B20.PowerOnTemperature));
        }

        [Test]
        public void GetTemperatureCode()
        {
            var scratchpad = new DS18B20.Scratchpad(new byte[] { 0x50, 0x05, 0x00, 0x00, 0x00, 0xFF, 0x0C, 0x10, 0xD6 });
            Assert.AreEqual(0x0550, scratchpad.TemperatureRawData);
            Assert.IsTrue(scratchpad.IsPowerOnTemperature);
        }

        [Test]
        public void GetThermometerResolution()
        {
            var scratchpad = new DS18B20.Scratchpad(new byte[] { 0x55, 0x01, 0x4B, 0x46, 0x7F, 0xFF, 0x0B, 0x10, 0xD0 });

            Assert.AreEqual(DS18B20.ThermometerResolution.Resolution12bit, scratchpad.ThermometerActualResolution);
        }

        [Test]
        public void EncodeScratchpad12bit()
        {
            var temperatureValue = DS18B20.Scratchpad.DecodeTemperature12bit(0x0155);
            var scratchpad = DS18B20.Scratchpad.EncodeScratchpad(temperatureValue, 0x4B, 0x46);
            Assert.AreEqual(0x55, scratchpad[0]);
            Assert.AreEqual(0x01, scratchpad[1]);
            Assert.AreEqual(0x4B, scratchpad[2]);
            Assert.AreEqual(0x46, scratchpad[3]);
            Assert.AreEqual(0x7F, scratchpad[4]);
        }

        [Test]
        public void IsValidTemperatureCode()
        {
            Assert.IsTrue(DS18B20.IsValidTemperatureCode(0x0550));
            Assert.IsTrue(DS18B20.IsValidTemperatureCode(0x0000));
            Assert.IsTrue(DS18B20.IsValidTemperatureCode(0xFFF8));

            Assert.IsFalse(DS18B20.IsValidTemperatureCode(0x07FF));
            Assert.IsFalse(DS18B20.IsValidTemperatureCode(0xF000));
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
            Assert.AreEqual(0x00, Crc8Utility.CalculateCrc8(BitConverter.GetBytes(Utils.RomCodeFromLEString(romCodeString))));
        }

        [Test]
        public void CheckScratchpad()
        {
            Assert.IsTrue(new DS18B20.Scratchpad(new byte[] { 0x50, 0x05, 0x4B, 0x46, 0x7F, 0xFF, 0x0C, 0x10, 0x1C, }).IsValidCrc);
            Assert.IsTrue(new DS18B20.Scratchpad(new byte[] { 0x9E, 0x01, 0x4B, 0x46, 0x7F, 0xFF, 0x02, 0x10, 0x56, }).IsValidCrc);

            Assert.That(() => { new DS18B20.Scratchpad(null); }, Throws.ArgumentNullException);
            Assert.That(() => { new DS18B20.Scratchpad(new byte[0]); }, Throws.ArgumentException);
        }

        [Test]
        public void ScratchpadFromAbsentDeviceTest()
        {
            // When processing response from absent device
            var scratchpad = new DS18B20.Scratchpad(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, });
            Assert.IsFalse(scratchpad.IsValidCrc);
            Assert.IsFalse(scratchpad.Temperature.HasValue);
            Assert.IsFalse(scratchpad.ThermometerActualResolution.HasValue);
        }

        [TestCase("28341BF802000001")]
        [TestCase("280DBA7800000012")]
        [TestCase("28ABCE780000008E")]
        [TestCase("28FCB078000000EA")]
        public void RomCodeToLEString(string romCodeString)
        {
            var romCode = Utils.RomCodeFromLEString(romCodeString);
            var romCodeStringNew = Utils.RomCodeToLEString(romCode);
            Assert.AreEqual(romCodeString, romCodeStringNew);
        }
    }
}
