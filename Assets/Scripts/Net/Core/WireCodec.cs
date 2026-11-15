using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Banda.Core;
using Banda.Heist;

namespace Banda.Net
{
    /// <summary>Тип повідомлення (перший байт кожного пакета).</summary>
    public enum MsgType : byte
    {
        // Клієнт → хост
        Hello = 1,
        PickRole = 2,
        StartHeist = 3,
        Action = 4,
        RequestState = 5,
        BackToLobby = 6,

        // Хост → клієнт
        Welcome = 10,
        Lobby = 11,
        Static = 12,
        Snapshot = 13,
        Finished = 14,
        Closed = 15,
        Notice = 16,
    }

    public enum RoomPhase : byte
    {
        Lobby = 0,
        Heist = 1,
        Finished = 2,
    }

    /// <summary>Відповідь хоста на Hello: прийнято чи ні, роль, чи це повернення після обриву.</summary>
    public sealed class WelcomeInfo
    {
        public bool Ok;
        public string Error = string.Empty;
        public string RoomCode = string.Empty;
        public bool IsHost;
        public RoleId Role = RoleId.None;
        public bool Resumed;
    }

    public sealed class LobbyPlayerInfo
    {
        public string Name = string.Empty;
        public RoleId Role = RoleId.None;
        public bool IsHost;
        public bool Connected = true;
    }

    /// <summary>Лобі, як його бачить один гравець (YourIndex — його рядок у списку).</summary>
    public sealed class LobbyState
    {
        public string RoomCode = string.Empty;
        public RoomPhase Phase;
        public int YourIndex = -1;
        public bool CanStart;
        public List<LobbyPlayerInfo> Players = new List<LobbyPlayerInfo>();
    }

    public sealed class ProtocolException : Exception
    {
        public ProtocolException(string message) : base(message)
        {
        }
    }

    /// <summary>
    /// Бінарний формат повідомлень. Власний, а не JsonUtility: Vec2/Box — readonly struct, їх Unity не серіалізує.
    /// Читання захищене від сміття: довжини списків і рядків обмежені, зайві байти відкидаються як помилка протоколу.
    /// </summary>
    public static class WireCodec
    {
        public const byte Version = 1;
        public const int MaxList = 4096;
        public const int MaxStringBytes = 512;

        // ------------------------------------------------------------------ пакет

        public static byte[] Encode(MsgType type, Action<BinaryWriter> body = null)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                w.Write((byte)type);
                body?.Invoke(w);
                w.Flush();
                return ms.ToArray();
            }
        }

        /// <summary>Тип повідомлення й читач решти. Порожній або невідомий пакет — ProtocolException.</summary>
        public static MsgType ReadType(byte[] data, out BinaryReader reader)
        {
            if (data == null || data.Length == 0)
            {
                throw new ProtocolException("Порожній пакет");
            }

            byte raw = data[0];
            if (!Enum.IsDefined(typeof(MsgType), raw))
            {
                throw new ProtocolException("Невідомий тип повідомлення " + raw);
            }

            reader = new BinaryReader(new MemoryStream(data, 1, data.Length - 1, false));
            return (MsgType)raw;
        }

        // ------------------------------------------------------------------ примітиви

        public static void WriteString(BinaryWriter w, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (bytes.Length > MaxStringBytes)
            {
                Array.Resize(ref bytes, MaxStringBytes);
            }

            w.Write((ushort)bytes.Length);
            w.Write(bytes);
        }

        public static string ReadString(BinaryReader r)
        {
            int length = r.ReadUInt16();
            if (length > MaxStringBytes)
            {
                throw new ProtocolException("Задовгий рядок");
            }

            byte[] bytes = r.ReadBytes(length);
            if (bytes.Length != length)
            {
                throw new ProtocolException("Обірваний рядок");
            }

            return Encoding.UTF8.GetString(bytes);
        }

        private static int ReadCount(BinaryReader r)
        {
            int n = r.ReadInt32();
            if (n < 0 || n > MaxList)
            {
                throw new ProtocolException("Недопустима довжина списку " + n);
            }

            return n;
        }

        public static void WriteVec(BinaryWriter w, Vec2 v)
        {
            w.Write(v.X);
            w.Write(v.Y);
        }

        public static Vec2 ReadVec(BinaryReader r) => new Vec2(r.ReadSingle(), r.ReadSingle());

        public static void WriteBox(BinaryWriter w, Box b)
        {
            w.Write(b.X);
            w.Write(b.Y);
            w.Write(b.W);
            w.Write(b.H);
        }

        public static Box ReadBox(BinaryReader r) => new Box(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

        private static void WriteBoxes(BinaryWriter w, List<Box> list)
        {
            w.Write(list.Count);
            foreach (Box b in list)
            {
                WriteBox(w, b);
            }
        }

        private static List<Box> ReadBoxes(BinaryReader r)
        {
            int n = ReadCount(r);
            List<Box> list = new List<Box>(n);
            for (int i = 0; i < n; i++)
            {
                list.Add(ReadBox(r));
            }

            return list;
        }

        private static void WriteVecs(BinaryWriter w, List<Vec2> list)
        {
            w.Write(list.Count);
            foreach (Vec2 v in list)
            {
                WriteVec(w, v);
            }
        }

        private static List<Vec2> ReadVecs(BinaryReader r)
        {
            int n = ReadCount(r);
            List<Vec2> list = new List<Vec2>(n);
            for (int i = 0; i < n; i++)
            {
                list.Add(ReadVec(r));
            }

            return list;
        }

        private static RoleId ReadRole(BinaryReader r)
        {
            int raw = r.ReadInt32();
            if (!Enum.IsDefined(typeof(RoleId), raw))
            {
                throw new ProtocolException("Невідома роль " + raw);
            }

            return (RoleId)raw;
        }

        // ------------------------------------------------------------------ дія гравця

        public static void WriteAction(BinaryWriter w, HeistAction a)
        {
            w.Write((int)a.Kind);
            w.Write(a.Index);
            int[] numbers = a.Numbers ?? Array.Empty<int>();
            w.Write(numbers.Length);
            foreach (int n in numbers)
            {
                w.Write(n);
            }

            WriteVec(w, a.Direction);
        }

        public static HeistAction ReadAction(BinaryReader r)
        {
            int kind = r.ReadInt32();
            if (!Enum.IsDefined(typeof(HeistActionKind), kind))
            {
                throw new ProtocolException("Невідома дія " + kind);
            }

            HeistAction a = new HeistAction { Kind = (HeistActionKind)kind, Index = r.ReadInt32() };
            int count = r.ReadInt32();
            if (count < 0 || count > 16)
            {
                throw new ProtocolException("Задовгий набір чисел");
            }

            a.Numbers = new int[count];
            for (int i = 0; i < count; i++)
            {
                a.Numbers[i] = r.ReadInt32();
            }

            Vec2 d = ReadVec(r);
            // Довжина напрямку ≤ 1 (ТЗ: джойстик) — хост не вірить клієнту, що той не «прискорить» Тінь.
            a.Direction = d.LimitLength(1f);
            if (float.IsNaN(a.Direction.X) || float.IsNaN(a.Direction.Y))
            {
                a.Direction = Vec2.Zero;
            }

            return a;
        }

        // ------------------------------------------------------------------ спільне й підсумок

        private static void WriteShared(BinaryWriter w, SharedView s)
        {
            w.Write(s.Alarm);
            w.Write(s.TimeLeft);
            w.Write(s.Alert);
            w.Write(s.Players.Count);
            foreach (PlayerChip p in s.Players)
            {
                WriteString(w, p.Name);
                w.Write((int)p.Role);
                w.Write((int)p.Status);
                w.Write(p.Connected);
            }
        }

        private static SharedView ReadShared(BinaryReader r)
        {
            SharedView s = new SharedView { Alarm = r.ReadSingle(), TimeLeft = r.ReadSingle(), Alert = r.ReadBoolean() };
            int n = ReadCount(r);
            for (int i = 0; i < n; i++)
            {
                PlayerChip chip = new PlayerChip { Name = ReadString(r), Role = ReadRole(r) };
                int status = r.ReadInt32();
                chip.Status = Enum.IsDefined(typeof(PlayerStatus), status) ? (PlayerStatus)status : PlayerStatus.Waiting;
                chip.Connected = r.ReadBoolean();
                s.Players.Add(chip);
            }

            return s;
        }

        public static void WriteSummary(BinaryWriter w, HeistSummary s)
        {
            w.Write((int)s.Outcome);
            w.Write(s.MaxAlarm);
            w.Write(s.TimeUsedSeconds);
            w.Write(s.Loot);
            w.Write(s.SafeMistakes);
        }

        public static HeistSummary ReadSummary(BinaryReader r)
        {
            int outcome = r.ReadInt32();
            return new HeistSummary
            {
                Outcome = Enum.IsDefined(typeof(HeistOutcome), outcome) ? (HeistOutcome)outcome : HeistOutcome.None,
                MaxAlarm = r.ReadInt32(),
                TimeUsedSeconds = r.ReadInt32(),
                Loot = r.ReadBoolean(),
                SafeMistakes = r.ReadInt32(),
            };
        }

        // ------------------------------------------------------------------ статика ролі

        public static void WriteStatic(BinaryWriter w, RoleStatic s)
        {
            w.Write((int)s.Role);
            switch (s)
            {
                case HackerStatic h:
                    WriteVec(w, h.Size);
                    WriteBoxes(w, h.Walls);
                    WriteBoxes(w, h.Covers);
                    WriteBox(w, h.Door);
                    WriteVecs(w, h.Cameras);
                    w.Write(h.CameraRange);
                    w.Write(h.CameraOffTime);
                    WriteVec(w, h.Safe);
                    WriteBoxes(w, h.Exits);
                    break;
                case ShadowStatic sh:
                    w.Write(sh.ViewRadius);
                    break;
                case SafecrackerStatic sc:
                    w.Write(sc.Combo.Length);
                    foreach (int n in sc.Combo)
                    {
                        w.Write(n);
                    }

                    w.Write(sc.Range);
                    break;
                default:
                    throw new ProtocolException("Невідомий тип статики " + s.GetType().Name);
            }
        }

        public static RoleStatic ReadStatic(BinaryReader r)
        {
            switch (ReadRole(r))
            {
                case RoleId.Hacker:
                    return new HackerStatic
                    {
                        Size = ReadVec(r),
                        Walls = ReadBoxes(r),
                        Covers = ReadBoxes(r),
                        Door = ReadBox(r),
                        Cameras = ReadVecs(r),
                        CameraRange = r.ReadSingle(),
                        CameraOffTime = r.ReadSingle(),
                        Safe = ReadVec(r),
                        Exits = ReadBoxes(r),
                    };
                case RoleId.Shadow:
                    return new ShadowStatic { ViewRadius = r.ReadSingle() };
                case RoleId.Safecracker:
                    int n = ReadCount(r);
                    int[] combo = new int[n];
                    for (int i = 0; i < n; i++)
                    {
                        combo[i] = r.ReadInt32();
                    }

                    return new SafecrackerStatic { Combo = combo, Range = r.ReadInt32() };
                default:
                    throw new ProtocolException("Статики для цієї ролі немає");
            }
        }

        // ------------------------------------------------------------------ знімок

        public static void WriteSnapshot(BinaryWriter w, RoleSnapshot snap)
        {
            WriteShared(w, snap.Shared);
            if (snap.View == null)
            {
                w.Write(-1);
                return;
            }

            w.Write((int)snap.View.Role);
            switch (snap.View)
            {
                case HackerView h:
                    w.Write(h.CamerasOff.Count);
                    foreach (float f in h.CamerasOff)
                    {
                        w.Write(f);
                    }

                    w.Write(h.DoorOpen);
                    WriteVec(w, h.Guard);
                    w.Write(h.GuardFacing);
                    w.Write(h.Alert);
                    break;
                case ShadowView s:
                    WriteVec(w, s.Position);
                    w.Write(s.Hidden);
                    w.Write(s.Carrying);
                    w.Write(s.Seen);
                    w.Write(s.CanHide);
                    w.Write(s.CanTake);
                    WriteBoxes(w, s.Walls);
                    WriteBoxes(w, s.Covers);
                    WriteVecs(w, s.Cameras);
                    w.Write(s.GuardVisible);
                    WriteVec(w, s.Guard);
                    w.Write(s.GuardFacing);
                    w.Write(s.DoorVisible);
                    WriteBox(w, s.Door);
                    w.Write(s.DoorOpen);
                    w.Write(s.SafeVisible);
                    WriteVec(w, s.Safe);
                    w.Write(s.SafeOpen);
                    WriteBoxes(w, s.Exits);
                    break;
                case SafecrackerView sc:
                    w.Write(sc.Open);
                    w.Write(sc.Mistakes);
                    break;
                default:
                    throw new ProtocolException("Невідомий тип знімка " + snap.View.GetType().Name);
            }
        }

        public static RoleSnapshot ReadSnapshot(BinaryReader r)
        {
            RoleSnapshot snap = new RoleSnapshot { Shared = ReadShared(r) };
            int role = r.ReadInt32();
            if (role == -1)
            {
                return snap;
            }

            if (!Enum.IsDefined(typeof(RoleId), role))
            {
                throw new ProtocolException("Невідома роль " + role);
            }

            switch ((RoleId)role)
            {
                case RoleId.Hacker:
                    HackerView h = new HackerView();
                    int cams = ReadCount(r);
                    for (int i = 0; i < cams; i++)
                    {
                        h.CamerasOff.Add(r.ReadSingle());
                    }

                    h.DoorOpen = r.ReadBoolean();
                    h.Guard = ReadVec(r);
                    h.GuardFacing = r.ReadSingle();
                    h.Alert = r.ReadBoolean();
                    snap.View = h;
                    break;
                case RoleId.Shadow:
                    snap.View = new ShadowView
                    {
                        Position = ReadVec(r),
                        Hidden = r.ReadBoolean(),
                        Carrying = r.ReadBoolean(),
                        Seen = r.ReadBoolean(),
                        CanHide = r.ReadBoolean(),
                        CanTake = r.ReadBoolean(),
                        Walls = ReadBoxes(r),
                        Covers = ReadBoxes(r),
                        Cameras = ReadVecs(r),
                        GuardVisible = r.ReadBoolean(),
                        Guard = ReadVec(r),
                        GuardFacing = r.ReadSingle(),
                        DoorVisible = r.ReadBoolean(),
                        Door = ReadBox(r),
                        DoorOpen = r.ReadBoolean(),
                        SafeVisible = r.ReadBoolean(),
                        Safe = ReadVec(r),
                        SafeOpen = r.ReadBoolean(),
                        Exits = ReadBoxes(r),
                    };
                    break;
                case RoleId.Safecracker:
                    snap.View = new SafecrackerView { Open = r.ReadBoolean(), Mistakes = r.ReadInt32() };
                    break;
                default:
                    throw new ProtocolException("Знімка для цієї ролі немає");
            }

            return snap;
        }

        // ------------------------------------------------------------------ лобі й вітання

        public static void WriteWelcome(BinaryWriter w, WelcomeInfo info)
        {
            w.Write(info.Ok);
            WriteString(w, info.Error);
            WriteString(w, info.RoomCode);
            w.Write(info.IsHost);
            w.Write((int)info.Role);
            w.Write(info.Resumed);
        }

        public static WelcomeInfo ReadWelcome(BinaryReader r)
        {
            return new WelcomeInfo
            {
                Ok = r.ReadBoolean(),
                Error = ReadString(r),
                RoomCode = ReadString(r),
                IsHost = r.ReadBoolean(),
                Role = ReadRole(r),
                Resumed = r.ReadBoolean(),
            };
        }

        public static void WriteLobby(BinaryWriter w, LobbyState lobby)
        {
            WriteString(w, lobby.RoomCode);
            w.Write((byte)lobby.Phase);
            w.Write(lobby.YourIndex);
            w.Write(lobby.CanStart);
            w.Write(lobby.Players.Count);
            foreach (LobbyPlayerInfo p in lobby.Players)
            {
                WriteString(w, p.Name);
                w.Write((int)p.Role);
                w.Write(p.IsHost);
                w.Write(p.Connected);
            }
        }

        public static LobbyState ReadLobby(BinaryReader r)
        {
            LobbyState lobby = new LobbyState { RoomCode = ReadString(r) };
            byte phase = r.ReadByte();
            lobby.Phase = Enum.IsDefined(typeof(RoomPhase), phase) ? (RoomPhase)phase : RoomPhase.Lobby;
            lobby.YourIndex = r.ReadInt32();
            lobby.CanStart = r.ReadBoolean();
            int n = ReadCount(r);
            for (int i = 0; i < n; i++)
            {
                lobby.Players.Add(new LobbyPlayerInfo
                {
                    Name = ReadString(r),
                    Role = ReadRole(r),
                    IsHost = r.ReadBoolean(),
                    Connected = r.ReadBoolean(),
                });
            }

            return lobby;
        }
    }
}
