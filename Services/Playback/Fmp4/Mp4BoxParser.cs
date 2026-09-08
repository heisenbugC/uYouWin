using System;
using System.Collections.Generic;
using System.IO;

namespace uYouWin.Services.Playback.Fmp4
{
    internal static class Mp4BoxParser
    {
        public static List<Mp4Box> ParseChildren(
            byte[] data)
        {
            var boxes =
                new List<Mp4Box>();

            int position = 0;

            while (position < data.Length)
            {
                Mp4Box box =
                    ParseAt(
                        data,
                        position);

                boxes.Add(box);

                int advance =
                    checked((int)box.Size);

                if (advance <= 0)
                    throw new InvalidDataException(
                        "Invalid MP4 box size.");

                position += advance;
            }

            if (position != data.Length)
            {
                throw new InvalidDataException(
                    "MP4 child boxes do not consume the parent box exactly.");
            }

            return boxes;
        }

        public static Mp4Box ParseAt(
            byte[] data,
            int offset)
        {
            if (data.Length - offset < 8)
                throw new InvalidDataException(
                    "Incomplete MP4 box header.");

            uint size32 =
                Mp4BinaryReader.UInt32(
                    data,
                    offset);

            string type =
                Mp4BinaryReader.FourCC(
                    data,
                    offset + 4);

            long size;
            int headerSize;

            if (size32 == 1)
            {
                if (data.Length - offset < 16)
                {
                    throw new InvalidDataException(
                        "Incomplete extended MP4 box header.");
                }

                ulong extended =
                    Mp4BinaryReader.UInt64(
                        data,
                        offset + 8);

                if (extended > long.MaxValue)
                    throw new InvalidDataException(
                        "MP4 box is too large.");

                size = (long)extended;
                headerSize = 16;
            }
            else if (size32 == 0)
            {
                size =
                    data.Length - offset;

                headerSize = 8;
            }
            else
            {
                size = size32;
                headerSize = 8;
            }

            if (size < headerSize)
                throw new InvalidDataException(
                    "Invalid MP4 box size.");

            if (size > data.Length - offset)
                throw new InvalidDataException(
                    "MP4 box extends beyond the containing buffer.");

            int payloadLength =
                checked((int)size - headerSize);

            byte[] payload =
                Mp4BinaryReader.Copy(
                    data,
                    offset + headerSize,
                    payloadLength);

            return new Mp4Box
            {
                Type = type,
                Size = size,
                HeaderSize = headerSize,
                Data = payload
            };
        }

        public static Mp4Box FindChild(
            byte[] data,
            string type)
        {
            List<Mp4Box> boxes =
                ParseChildren(data);

            foreach (Mp4Box box in boxes)
            {
                if (string.Equals(
                        box.Type,
                        type,
                        StringComparison.Ordinal))
                {
                    return box;
                }
            }

            return null;
        }

        public static List<Mp4Box> FindChildren(
            byte[] data,
            string type)
        {
            var result =
                new List<Mp4Box>();

            foreach (Mp4Box box in
                     ParseChildren(data))
            {
                if (string.Equals(
                        box.Type,
                        type,
                        StringComparison.Ordinal))
                {
                    result.Add(box);
                }
            }

            return result;
        }
    }
}