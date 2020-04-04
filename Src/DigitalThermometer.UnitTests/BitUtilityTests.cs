using NUnit.Framework;

using DigitalThermometer.Hardware;

namespace DigitalThermometer.UnitTests
{
    [TestFixture]
    class BitUtilityTests
    {
        [Test]
        public void ReadBit0x00()
        {
            var testData = new byte[] { 0x00 };
            for (var i = 0; i < 8; i++)
            {
                Assert.AreEqual(0, BitUtility.ReadBit(testData, i));
            }
        }

        [Test]
        public void ReadBit0xFF()
        {
            var testData = new byte[] { 0xFF };
            for (var i = 0; i < 8; i++)
            {
                Assert.AreEqual(1, BitUtility.ReadBit(testData, i));
            }
        }

        [Test]
        public void ReadBit0xAA()
        {
            var testData = new byte[] { 0xAA };
            for (var i = 0; i < 8; i++)
            {
                Assert.AreEqual(i % 2, BitUtility.ReadBit(testData, i));
            }
        }

        [Test]
        public void ReadBit0x55()
        {
            var testData = new byte[] { 0x55 };
            for (var i = 0; i < 8; i++)
            {
                Assert.AreEqual(1 - i % 2, BitUtility.ReadBit(testData, i));
            }
        }

        [Test]
        public void ReadBit0x00AA00()
        {
            var testData = new byte[] { 0x00, 0xAA, 0x00 };

            Assert.AreEqual(0, BitUtility.ReadBit(testData, 8));
            Assert.AreEqual(1, BitUtility.ReadBit(testData, 15));
            Assert.AreEqual(0, BitUtility.ReadBit(testData, 23));
        }

        [Test]
        public void InverseBits()
        {
            var testData = new byte[] { 0xAA };
            for (var i = 0; i < 8; i++)
            {
                BitUtility.WriteBit(testData, i, (byte)(1 - BitUtility.ReadBit(testData, i)));
            }

            Assert.AreEqual(0x55, testData[0]);
        }
    }
}