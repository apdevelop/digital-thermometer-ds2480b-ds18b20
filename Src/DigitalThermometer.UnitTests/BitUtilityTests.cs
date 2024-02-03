using NUnit.Framework;

using DigitalThermometer.OneWire;

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
                Assert.That(BitUtility.ReadBit(testData, i), Is.EqualTo(0));
            }
        }

        [Test]
        public void ReadBit0xFF()
        {
            var testData = new byte[] { 0xFF };
            for (var i = 0; i < 8; i++)
            {
                Assert.That(BitUtility.ReadBit(testData, i), Is.EqualTo(1));
            }
        }

        [Test]
        public void ReadBit0xAA()
        {
            var testData = new byte[] { 0xAA };
            for (var i = 0; i < 8; i++)
            {
                Assert.That(BitUtility.ReadBit(testData, i), Is.EqualTo(i % 2));
            }
        }

        [Test]
        public void ReadBit0x55()
        {
            var testData = new byte[] { 0x55 };
            for (var i = 0; i < 8; i++)
            {
                Assert.That(BitUtility.ReadBit(testData, i), Is.EqualTo(1 - i % 2));
            }
        }

        [Test]
        public void ReadBit0x00AA00()
        {
            var testData = new byte[] { 0x00, 0xAA, 0x00 };

            Assert.Multiple(() =>
            {
                Assert.That(BitUtility.ReadBit(testData, 8), Is.EqualTo(0));
                Assert.That(BitUtility.ReadBit(testData, 15), Is.EqualTo(1));
                Assert.That(BitUtility.ReadBit(testData, 23), Is.EqualTo(0));
            });
        }

        [Test]
        public void InverseBits()
        {
            var testData = new byte[] { 0xAA };
            for (var i = 0; i < 8; i++)
            {
                BitUtility.WriteBit(testData, i, (byte)(1 - BitUtility.ReadBit(testData, i)));
            }

            Assert.That(testData[0], Is.EqualTo(0x55));
        }
    }
}
