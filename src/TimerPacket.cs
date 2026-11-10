using System;
using Unity.Netcode;

namespace POGMods.Timer
{
    /// <summary>What the countdown shows: whether a round runs, seconds left, whether the game's warning played.</summary>
    internal struct TimerState
    {
        public bool Active;
        public float Remaining;
        public bool Warning;

        public static readonly TimerState None = default;

        public bool SameShape(TimerState o) => Active == o.Active && Warning == o.Warning;
    }

    /// <summary>
    /// The packet the host sends on Netcode's unnamed-message channel, little-endian:
    /// <c>[magic "POGT": u32][seconds left: f32 bits][flags: u8]</c>, flags bit 0 = round running,
    /// bit 1 = the game's warning has played.
    /// This layout is unchanged since 1.0.1, so hosts and clients of 1.0.x and 1.1.x understand each other.
    /// It is a contract with every copy already installed: never change it, add a new magic instead.
    /// </summary>
    internal static class TimerPacket
    {
        public const uint Magic = 0x54474F50u;   // "POGT"
        public const int Size = 9;

        private const byte FlagActive = 1, FlagWarning = 2;

        public static void Write(FastBufferWriter w, TimerState s)
        {
            WriteU32(w, Magic);
            WriteU32(w, BitConverter.SingleToUInt32Bits(Math.Max(0f, s.Remaining)));
            w.WriteByteSafe((byte)((s.Active ? FlagActive : 0) | (s.Warning ? FlagWarning : 0)));
        }

        /// <summary>
        /// Reads a POG Timer packet. Anything else on the shared channel (another mod's) is left
        /// unread, with the reader where it was.
        /// </summary>
        public static bool TryRead(FastBufferReader r, out TimerState s)
        {
            s = TimerState.None;
            int start = r.Position;
            if (r.Length - start < Size) return false;
            if (ReadU32(r) != Magic) { r.Seek(start); return false; }
            float remaining = BitConverter.UInt32BitsToSingle(ReadU32(r));
            r.ReadByteSafe(out byte flags);
            s = new TimerState
            {
                Active = (flags & FlagActive) != 0,
                Remaining = float.IsFinite(remaining) ? Math.Max(0f, remaining) : 0f,
                Warning = (flags & FlagWarning) != 0,
            };
            return true;
        }

        private static void WriteU32(FastBufferWriter w, uint v)
        {
            for (int i = 0; i < 4; i++) w.WriteByteSafe((byte)(v >> (8 * i)));
        }

        private static uint ReadU32(FastBufferReader r)
        {
            uint v = 0;
            for (int i = 0; i < 4; i++) { r.ReadByteSafe(out byte b); v |= (uint)b << (8 * i); }
            return v;
        }
    }
}
