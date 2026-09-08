using System;
using System.IO;
using System.Text;

namespace uYouWin.Services.Playback.Fmp4
{
    internal static class Mp4BinaryReader
    {
        public static ushort UInt16(
            byte[] data,
            int offset)
        {
            Ensure(data, offset, 2);

            return (ushort)(
                (data[offset] << 8) |
                data[offset + 1]);
        }

        public static uint UInt32(
            byte[] data,
            int offset)
        {
            Ensure(data, offset, 4);

            return
                ((uint)data[offset] << 24) |
                ((uint)data[offset + 1] << 16) |
                ((uint)data[offset + 2] << 8) |
                data[offset + 3];
        }

        public static int Int32(
            byte[] data,
            int offset)
        {
            return unchecked(
                (int)UInt32(
                    data,
                    offset));
        }

        public static ulong UInt64(
            byte[] data,
            int offset)
        {
            Ensure(data, offset, 8);

            uint high =
                UInt32(
                    data,
                    offset);

            uint low =
                UInt32(
                    data,
                    offset + 4);

            return
                ((ulong)high << 32) |
                low;
        }

        public static long Int64(
            byte[] data,
            int offset)
        {
            return unchecked(
                (long)UInt64(
                    data,
                    offset));
        }

        public static string FourCC(
            byte[] data,
            int offset)
        {
            Ensure(data, offset, 4);

            return Encoding.ASCII.GetString(
                data,
                offset,
                4);
        }

        public static double Fixed16(
            byte[] data,
            int offset)
        {
            uint value =
                UInt32(
                    data,
                    offset);

            return
                (value >> 16) +
                ((value & 0xFFFF) / 65536.0);
        }

        public static TimeSpan ToTimeSpan(
            long units,
            uint timescale)
        {
            if (timescale == 0)
                throw new InvalidDataException(
                    "MP4 track has zero timescale.");

            decimal ticks =
                ((decimal)units *
                 TimeSpan.TicksPerSecond) /
                timescale;

            if (ticks > long.MaxValue)
                return TimeSpan.MaxValue;

            if (ticks < long.MinValue)
                return TimeSpan.MinValue;

            return TimeSpan.FromTicks(
                (long)decimal.Truncate(ticks));
        }

        public static byte[] Copy(
            byte[] data,
            int offset,
            int count)
        {
            Ensure(data, offset, count);

            var result =
                new byte[count];

            Buffer.BlockCopy(
                data,
                offset,
                result,
                0,
                count);

            return result;
        }

        public static void Ensure(
            byte[] data,
            int offset,
            int count)
        {
            if (data == null)
                throw new ArgumentNullException(
                    nameof(data));

            if (offset < 0 ||
                count < 0 ||
                offset > data.Length - count)
            {
                throw new InvalidDataException(
                    "MP4 box contains an invalid offset or length.");
            }
        }
    }
}