using System;

using NUnit.Framework;

using DigitalThermometer.Hardware;

namespace DigitalThermometer.UnitTests
{
    [TestFixture]
    class DS18B20Tests
    {
        /// <summary>
        /// TEMPERATURE/DATA RELATIONSHIP Table 2
        /// </summary>
        [Test]
        public void DecodeTemperature12bit()
        {
            Assert.AreEqual(+125.0, DS18B20.DecodeTemperature12bit(0x07D0));
            Assert.AreEqual(+85.0, DS18B20.DecodeTemperature12bit(0x0550));
            Assert.AreEqual(+25.0625, DS18B20.DecodeTemperature12bit(0x0191));
            Assert.AreEqual(+10.125, DS18B20.DecodeTemperature12bit(0x00A2));
            Assert.AreEqual(+0.5, DS18B20.DecodeTemperature12bit(0x0008));

            Assert.AreEqual(0.0, DS18B20.DecodeTemperature12bit(0x0000));

            Assert.AreEqual(-0.5, DS18B20.DecodeTemperature12bit(0xFFF8));
            Assert.AreEqual(-10.125, DS18B20.DecodeTemperature12bit(0xFF5E));
            Assert.AreEqual(-25.0625, DS18B20.DecodeTemperature12bit(0xFE6F));
            Assert.AreEqual(-55.0, DS18B20.DecodeTemperature12bit(0xFC90));

            Assert.AreEqual(DS18B20.PowerOnTemperature, DS18B20.DecodeTemperature12bit(DS18B20.PowerOnTemperatureCode));

            Assert.That(() => { DS18B20.DecodeTemperature12bit(0x07FF); }, Throws.Exception.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => { DS18B20.DecodeTemperature12bit(0xF000); }, Throws.Exception.TypeOf<ArgumentOutOfRangeException>());
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
            Assert.AreEqual(0x0550, DS18B20.GetTemperatureCode(new byte[] { 0x50, 0x05, 0x00, 0x00, 0x00, 0xFF, 0x0C, 0x10, 0xD6 }));
        }

        [Test]
        public void GetThermometerResolution()
        {
            Assert.AreEqual(ThermometerResolution.Resolution12bit, DS18B20.GetThermometerResolution(0x7F));
            Assert.AreEqual(ThermometerResolution.Resolution12bit, DS18B20.GetThermometerResolution(new byte[] { 0x55, 0x01, 0x4B, 0x46, 0x7F, 0xFF, 0x0B, 0x10, 0xD0 }));
        }

        [Test]
        public void EncodeScratchpad12bit()
        {
            var temperatureValue = DS18B20.DecodeTemperature12bit(0x0155);
            Assert.AreEqual(
                new byte[] { 0x55, 0x01, 0x4B, 0x46, 0x7F, 0xFF, 0x0B, 0x10, 0xD0 },
                DS18B20.EncodeScratchpad12bit(temperatureValue, 0x4B, 0x46, 0xFF, 0x0B, 0x10));
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
            Assert.AreEqual(0x00, Crc8Utility.CalculateCrc8(BitConverter.GetBytes(DS18B20.RomCodeFromLEString(romCodeString))));
        }

        [Test]
        public void CheckScratchpad()
        {
            Assert.IsTrue(DS18B20.CheckScratchpad(new byte[] { 0x50, 0x05, 0x4B, 0x46, 0x7F, 0xFF, 0x0C, 0x10, 0x1C, }));
            Assert.IsTrue(DS18B20.CheckScratchpad(new byte[] { 0x9E, 0x01, 0x4B, 0x46, 0x7F, 0xFF, 0x02, 0x10, 0x56, }));

            Assert.That(() => { DS18B20.CheckScratchpad(null); }, Throws.ArgumentNullException);
        }

        [TestCase("28341BF802000001")]
        [TestCase("280DBA7800000012")]
        [TestCase("28ABCE780000008E")]
        [TestCase("28FCB078000000EA")]
        public void RomCodeToLEString(string romCodeString)
        {
            var romCode = DS18B20.RomCodeFromLEString(romCodeString);
            var romCodeStringNew = DS18B20.RomCodeToLEString(romCode);
            Assert.AreEqual(romCodeString, romCodeStringNew);
        }
    }
}
