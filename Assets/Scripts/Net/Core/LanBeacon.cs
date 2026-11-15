using System;
using System.Collections.Generic;

namespace Banda.Net
{
    /// <summary>Те, що хост оголошує в локальній мережі: код кімнати, імʼя хоста, скільки гравців, чи відкрита.</summary>
    public sealed class BeaconInfo
    {
        public string Code = string.Empty;
        public string HostName = string.Empty;
        public int Players;
        public bool Open = true;
    }

    /// <summary>Формат маяка «BANDA1|код|імʼя|гравців|відкрита» (як у Godot-версії). Чужі й зіпсовані пакети відкидаються.</summary>
    public static class BeaconCodec
    {
        public const string Prefix = "BANDA1";
        public const int MaxPacketChars = 200;

        public static string Encode(BeaconInfo info)
        {
            string name = (info.HostName ?? string.Empty).Replace('|', ' ');
            if (name.Length > 24)
            {
                name = name.Substring(0, 24);
            }

            return Prefix + "|" + info.Code + "|" + name + "|" + info.Players + "|" + (info.Open ? "1" : "0");
        }

        public static bool TryParse(string text, out BeaconInfo info)
        {
            info = null;
            if (string.IsNullOrEmpty(text) || text.Length > MaxPacketChars)
            {
                return false;
            }

            string[] parts = text.Split('|');
            if (parts.Length != 5 || parts[0] != Prefix || parts[1].Length != 4)
            {
                return false;
            }

            foreach (char c in parts[1])
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            if (!int.TryParse(parts[3], out int players) || players < 0 || players > 99)
            {
                return false;
            }

            if (parts[4] != "0" && parts[4] != "1")
            {
                return false;
            }

            info = new BeaconInfo { Code = parts[1], HostName = parts[2], Players = players, Open = parts[4] == "1" };
            return true;
        }
    }

    /// <summary>Знайдена в мережі кімната: хто оголосив і з якої адреси прийшов маяк.</summary>
    public sealed class LanRoom
    {
        public BeaconInfo Info;
        public string Address = string.Empty;
        public float LastSeen;
    }

    /// <summary>Список кімнат за маяками: кімната зникає, якщо маяка немає 3,5 с (ТЗ). Час передається ззовні — тести без годинника.</summary>
    public sealed class LanRoomList
    {
        public const float TimeoutSeconds = 3.5f;
        private readonly Dictionary<string, LanRoom> _rooms = new Dictionary<string, LanRoom>();

        public int Count => _rooms.Count;

        public event Action Changed;

        public void Seen(BeaconInfo info, string address, float now)
        {
            bool isNew = !_rooms.TryGetValue(info.Code, out LanRoom room);
            if (isNew)
            {
                room = new LanRoom();
                _rooms[info.Code] = room;
            }

            bool changed = isNew || room.Address != address || room.Info.Players != info.Players || room.Info.Open != info.Open || room.Info.HostName != info.HostName;
            room.Info = info;
            room.Address = address;
            room.LastSeen = now;
            if (changed)
            {
                Changed?.Invoke();
            }
        }

        public void Prune(float now)
        {
            List<string> gone = null;
            foreach (KeyValuePair<string, LanRoom> pair in _rooms)
            {
                if (now - pair.Value.LastSeen > TimeoutSeconds)
                {
                    (gone ?? (gone = new List<string>())).Add(pair.Key);
                }
            }

            if (gone == null)
            {
                return;
            }

            foreach (string code in gone)
            {
                _rooms.Remove(code);
            }

            Changed?.Invoke();
        }

        public bool TryGet(string code, out LanRoom room) => _rooms.TryGetValue(code ?? string.Empty, out room);

        public List<LanRoom> Snapshot() => new List<LanRoom>(_rooms.Values);
    }

    /// <summary>Чотири цифри коду кімнати. Лише цифри, щоб набирати з цифрової клавіатури.</summary>
    public static class RoomCode
    {
        public static string Generate(Random random)
        {
            return random.Next(0, 10000).ToString("D4");
        }

        public static bool IsValid(string code)
        {
            if (code == null || code.Length != 4)
            {
                return false;
            }

            foreach (char c in code)
            {
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            return true;
        }
    }
}
