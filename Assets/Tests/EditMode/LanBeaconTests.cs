using Banda.Net;
using NUnit.Framework;

namespace Banda.Tests
{
    /// <summary>Wi-Fi-маяк: формат пакета, відкидання чужого й зіпсованого, зникнення кімнати через 3,5 с, код кімнати.</summary>
    public sealed class LanBeaconTests
    {
        [Test]
        public void Beacon_Roundtrip()
        {
            string text = BeaconCodec.Encode(new BeaconInfo { Code = "0042", HostName = "Іван", Players = 2, Open = true });
            Assert.That(text, Is.EqualTo("BANDA1|0042|Іван|2|1"));
            Assert.That(BeaconCodec.TryParse(text, out BeaconInfo info), Is.True);
            Assert.That(info.Code, Is.EqualTo("0042"));
            Assert.That(info.HostName, Is.EqualTo("Іван"));
            Assert.That(info.Players, Is.EqualTo(2));
            Assert.That(info.Open, Is.True);
        }

        [Test]
        public void Beacon_HostNameWithSeparator_IsSanitized()
        {
            string text = BeaconCodec.Encode(new BeaconInfo { Code = "1234", HostName = "а|б", Players = 1 });
            Assert.That(BeaconCodec.TryParse(text, out BeaconInfo info), Is.True);
            Assert.That(info.HostName, Is.EqualTo("а б"));
        }

        [TestCase("")]
        [TestCase("hello")]
        [TestCase("BANDA2|1234|x|1|1")]
        [TestCase("BANDA1|12a4|x|1|1")]
        [TestCase("BANDA1|123|x|1|1")]
        [TestCase("BANDA1|1234|x|-1|1")]
        [TestCase("BANDA1|1234|x|1|2")]
        [TestCase("BANDA1|1234|x|1")]
        [TestCase("BANDA1|1234|x|1|1|extra")]
        public void Beacon_RejectsForeignOrBrokenPackets(string text)
        {
            Assert.That(BeaconCodec.TryParse(text, out BeaconInfo info), Is.False);
            Assert.That(info, Is.Null);
        }

        [Test]
        public void Beacon_RejectsHugePacket()
        {
            Assert.That(BeaconCodec.TryParse("BANDA1|1234|" + new string('x', 500) + "|1|1", out _), Is.False);
        }

        [Test]
        public void RoomList_RoomVanishesWithoutBeacon_After3_5s()
        {
            LanRoomList list = new LanRoomList();
            list.Seen(new BeaconInfo { Code = "1111", Players = 1 }, "192.168.0.5", 0f);
            list.Prune(3.4f);
            Assert.That(list.TryGet("1111", out LanRoom room), Is.True);
            Assert.That(room.Address, Is.EqualTo("192.168.0.5"));
            list.Prune(3.6f);
            Assert.That(list.TryGet("1111", out _), Is.False, "через 3,5 с без маяка кімната зникає");
        }

        [Test]
        public void RoomList_BeaconRefreshesRoom_AndUpdatesPlayers()
        {
            LanRoomList list = new LanRoomList();
            int changes = 0;
            list.Changed += () => changes++;
            list.Seen(new BeaconInfo { Code = "2222", Players = 1 }, "10.0.0.2", 0f);
            list.Seen(new BeaconInfo { Code = "2222", Players = 1 }, "10.0.0.2", 3f);
            Assert.That(changes, Is.EqualTo(1), "повторний маяк без змін не будить екран");
            list.Prune(6f);
            Assert.That(list.TryGet("2222", out _), Is.True, "маяк продовжив життя кімнати");
            list.Seen(new BeaconInfo { Code = "2222", Players = 3 }, "10.0.0.2", 6f);
            Assert.That(changes, Is.EqualTo(2));
            list.TryGet("2222", out LanRoom room);
            Assert.That(room.Info.Players, Is.EqualTo(3));
        }

        [Test]
        public void RoomCode_FourDigitsOnly()
        {
            System.Random random = new System.Random(5);
            for (int i = 0; i < 200; i++)
            {
                Assert.That(RoomCode.IsValid(RoomCode.Generate(random)), Is.True);
            }

            Assert.That(RoomCode.IsValid("12345"), Is.False);
            Assert.That(RoomCode.IsValid("12a4"), Is.False);
            Assert.That(RoomCode.IsValid(null), Is.False);
        }
    }
}
