using System;
using System.Collections.Generic;
using System.IO;

namespace uYouWin.Services.Playback.Fmp4
{
    internal static class Fmp4TrackParser
    {
        public static Fmp4TrackInfo ParseTrack(
            byte[] moovData,
            string requiredHandler)
        {
            Mp4Box trak =
                FindTrack(
                    moovData,
                    requiredHandler);

            if (trak == null)
            {
                throw new InvalidDataException(
                    "No MP4 track with handler '" +
                    requiredHandler +
                    "' was found.");
            }

            return ParseTrackBox(trak);
        }

        public static Dictionary<uint, Fmp4TrackInfo>
            ParseTrexDefaults(
                byte[] moovData)
        {
            var result =
                new Dictionary<uint, Fmp4TrackInfo>();

            Mp4Box mvex =
                Mp4BoxParser.FindChild(
                    moovData,
                    "mvex");

            if (mvex == null)
                return result;

            foreach (Mp4Box trex in
                     Mp4BoxParser.FindChildren(
                         mvex.Data,
                         "trex"))
            {
                if (trex.Data.Length < 24)
                    throw new InvalidDataException(
                        "Invalid trex box.");

                uint trackId =
                    Mp4BinaryReader.UInt32(
                        trex.Data,
                        4);

                uint defaultDuration =
                    Mp4BinaryReader.UInt32(
                        trex.Data,
                        12);

                uint defaultSize =
                    Mp4BinaryReader.UInt32(
                        trex.Data,
                        16);

                uint defaultFlags =
                    Mp4BinaryReader.UInt32(
                        trex.Data,
                        20);

                result[trackId] =
                    new Fmp4TrackInfo
                    {
                        TrackId = trackId,
                        DefaultSampleDuration =
                            defaultDuration,
                        DefaultSampleSize =
                            defaultSize,
                        DefaultSampleFlags =
                            defaultFlags
                    };
            }

            return result;
        }

        private static Mp4Box FindTrack(
            byte[] moovData,
            string requiredHandler)
        {
            foreach (Mp4Box trak in
                     Mp4BoxParser.FindChildren(
                         moovData,
                         "trak"))
            {
                Mp4Box mdia =
                    Mp4BoxParser.FindChild(
                        trak.Data,
                        "mdia");

                if (mdia == null)
                    continue;

                Mp4Box hdlr =
                    Mp4BoxParser.FindChild(
                        mdia.Data,
                        "hdlr");

                if (hdlr == null ||
                    hdlr.Data.Length < 12)
                    continue;

                string handler =
                    Mp4BinaryReader.FourCC(
                        hdlr.Data,
                        8);

                if (string.Equals(
                        handler,
                        requiredHandler,
                        StringComparison.Ordinal))
                {
                    return trak;
                }
            }

            return null;
        }

        private static Fmp4TrackInfo ParseTrackBox(
            Mp4Box trak)
        {
            var result =
                new Fmp4TrackInfo();

            ParseTrackHeader(
                trak.Data,
                result);

            Mp4Box mdia =
                Mp4BoxParser.FindChild(
                    trak.Data,
                    "mdia");

            if (mdia == null)
                throw new InvalidDataException(
                    "Track has no mdia box.");

            ParseMediaHeader(
                mdia.Data,
                result);

            ParseHandler(
                mdia.Data,
                result);

            ParseSampleDescription(
                mdia.Data,
                result);

            return result;
        }

        private static void ParseTrackHeader(
            byte[] trakData,
            Fmp4TrackInfo result)
        {
            Mp4Box tkhd =
                Mp4BoxParser.FindChild(
                    trakData,
                    "tkhd");

            if (tkhd == null)
                throw new InvalidDataException(
                    "Track has no tkhd box.");

            if (tkhd.Data.Length < 20)
                throw new InvalidDataException(
                    "Invalid tkhd box.");

            int version =
                tkhd.Data[0];

            if (version == 0)
            {
                result.TrackId =
                    Mp4BinaryReader.UInt32(
                        tkhd.Data,
                        12);
            }
            else if (version == 1)
            {
                if (tkhd.Data.Length < 28)
                    throw new InvalidDataException(
                        "Invalid version 1 tkhd box.");

                result.TrackId =
                    Mp4BinaryReader.UInt32(
                        tkhd.Data,
                        20);
            }
            else
            {
                throw new InvalidDataException(
                    "Unsupported tkhd version.");
            }
        }

        private static void ParseMediaHeader(
            byte[] mdiaData,
            Fmp4TrackInfo result)
        {
            Mp4Box mdhd =
                Mp4BoxParser.FindChild(
                    mdiaData,
                    "mdhd");

            if (mdhd == null)
                throw new InvalidDataException(
                    "Track has no mdhd box.");

            int version =
                mdhd.Data[0];

            if (version == 0)
            {
                if (mdhd.Data.Length < 16)
                    throw new InvalidDataException(
                        "Invalid mdhd box.");

                result.TimeScale =
                    Mp4BinaryReader.UInt32(
                        mdhd.Data,
                        12);
            }
            else if (version == 1)
            {
                if (mdhd.Data.Length < 24)
                    throw new InvalidDataException(
                        "Invalid version 1 mdhd box.");

                result.TimeScale =
                    Mp4BinaryReader.UInt32(
                        mdhd.Data,
                        20);
            }
            else
            {
                throw new InvalidDataException(
                    "Unsupported mdhd version.");
            }

            if (result.TimeScale == 0)
                throw new InvalidDataException(
                    "MP4 media timescale is zero.");
        }

        private static void ParseHandler(
            byte[] mdiaData,
            Fmp4TrackInfo result)
        {
            Mp4Box hdlr =
                Mp4BoxParser.FindChild(
                    mdiaData,
                    "hdlr");

            if (hdlr == null ||
                hdlr.Data.Length < 12)
                throw new InvalidDataException(
                    "Invalid hdlr box.");

            result.HandlerType =
                Mp4BinaryReader.FourCC(
                    hdlr.Data,
                    8);
        }

        private static void ParseSampleDescription(
            byte[] mdiaData,
            Fmp4TrackInfo result)
        {
            Mp4Box minf =
                Mp4BoxParser.FindChild(
                    mdiaData,
                    "minf");

            if (minf == null)
                throw new InvalidDataException(
                    "Track has no minf box.");

            Mp4Box stbl =
                Mp4BoxParser.FindChild(
                    minf.Data,
                    "stbl");

            if (stbl == null)
                throw new InvalidDataException(
                    "Track has no stbl box.");

            Mp4Box stsd =
                Mp4BoxParser.FindChild(
                    stbl.Data,
                    "stsd");

            if (stsd == null)
                throw new InvalidDataException(
                    "Track has no stsd box.");

            if (stsd.Data.Length < 8)
                throw new InvalidDataException(
                    "Invalid stsd box.");

            uint entryCount =
                Mp4BinaryReader.UInt32(
                    stsd.Data,
                    4);

            if (entryCount == 0)
                throw new InvalidDataException(
                    "stsd has no sample entries.");

            Mp4Box entry =
                Mp4BoxParser.ParseAt(
                    stsd.Data,
                    8);

            if (string.Equals(
                    result.HandlerType,
                    "vide",
                    StringComparison.Ordinal))
            {
                ParseVideoEntry(
                    entry,
                    result);
            }
            else if (string.Equals(
                         result.HandlerType,
                         "soun",
                         StringComparison.Ordinal))
            {
                ParseAudioEntry(
                    entry,
                    result);
            }
            else
            {
                throw new InvalidDataException(
                    "Unsupported MP4 track type: " +
                    result.HandlerType);
            }
        }

        private static void ParseVideoEntry(
            Mp4Box entry,
            Fmp4TrackInfo result)
        {
            if (!string.Equals(
                    entry.Type,
                    "avc1",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    entry.Type,
                    "avc3",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Expected avc1/avc3 video sample entry, got " +
                    entry.Type);
            }

            if (entry.Data.Length < 78)
                throw new InvalidDataException(
                    "Invalid AVC sample entry.");

            result.Width =
                Mp4BinaryReader.UInt16(
                    entry.Data,
                    24);

            result.Height =
                Mp4BinaryReader.UInt16(
                    entry.Data,
                    26);

            byte[] childData =
                Mp4BinaryReader.Copy(
                    entry.Data,
                    78,
                    entry.Data.Length - 78);

            Mp4Box avcConfig = null;

            foreach (Mp4Box child in
                     Mp4BoxParser.ParseChildren(
                         childData))
            {
                if (child.Type == "avcC")
                {
                    avcConfig = child;
                    break;
                }
            }

            if (avcConfig == null)
                throw new InvalidDataException(
                    "avc1 entry has no avcC box.");

            ParseAvcConfiguration(
                avcConfig.Data,
                result);
        }

        private static void ParseAvcConfiguration(
            byte[] data,
            Fmp4TrackInfo result)
        {
            if (data.Length < 7)
                throw new InvalidDataException(
                    "Invalid avcC box.");

            result.AvcConfiguration =
                (byte[])data.Clone();

            result.ProfileId = data[1];

            result.NaluLengthSize =
                (data[4] & 0x03) + 1;

            if (result.NaluLengthSize != 1 &&
                result.NaluLengthSize != 2 &&
                result.NaluLengthSize != 4)
            {
                throw new InvalidDataException(
                    "Unsupported H.264 NALU length size.");
            }

            int offset = 5;

            int spsCount =
                data[offset] & 0x1F;

            offset++;

            if (spsCount == 0)
                throw new InvalidDataException(
                    "avcC contains no SPS.");

            byte[] firstSps = null;

            for (int i = 0;
                 i < spsCount;
                 i++)
            {
                if (offset + 2 > data.Length)
                    throw new InvalidDataException(
                        "Invalid SPS length.");

                int length =
                    Mp4BinaryReader.UInt16(
                        data,
                        offset);

                offset += 2;

                if (length <= 0 ||
                    offset + length > data.Length)
                {
                    throw new InvalidDataException(
                        "Invalid SPS payload.");
                }

                if (firstSps == null)
                {
                    firstSps =
                        Mp4BinaryReader.Copy(
                            data,
                            offset,
                            length);
                }

                offset += length;
            }

            if (offset >= data.Length)
                throw new InvalidDataException(
                    "avcC contains no PPS count.");

            int ppsCount =
                data[offset];

            offset++;

            if (ppsCount == 0)
                throw new InvalidDataException(
                    "avcC contains no PPS.");

            byte[] firstPps = null;

            for (int i = 0;
                 i < ppsCount;
                 i++)
            {
                if (offset + 2 > data.Length)
                    throw new InvalidDataException(
                        "Invalid PPS length.");

                int length =
                    Mp4BinaryReader.UInt16(
                        data,
                        offset);

                offset += 2;

                if (length <= 0 ||
                    offset + length > data.Length)
                {
                    throw new InvalidDataException(
                        "Invalid PPS payload.");
                }

                if (firstPps == null)
                {
                    firstPps =
                        Mp4BinaryReader.Copy(
                            data,
                            offset,
                            length);
                }

                offset += length;
            }

            result.Sps = firstSps;
            result.Pps = firstPps;
        }

        private static void ParseAudioEntry(
            Mp4Box entry,
            Fmp4TrackInfo result)
        {
            if (!string.Equals(
                    entry.Type,
                    "mp4a",
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "Expected mp4a audio sample entry, got " +
                    entry.Type);
            }

            if (entry.Data.Length < 28)
                throw new InvalidDataException(
                    "Invalid mp4a sample entry.");

            result.ChannelCount =
                Mp4BinaryReader.UInt16(
                    entry.Data,
                    16);

            result.SampleRate =
                (uint)(
                    Mp4BinaryReader.UInt32(
                        entry.Data,
                        24) >> 16);

            byte[] childData =
                Mp4BinaryReader.Copy(
                    entry.Data,
                    28,
                    entry.Data.Length - 28);

            foreach (Mp4Box child in
                     Mp4BoxParser.ParseChildren(
                         childData))
            {
                if (child.Type == "esds")
                {
                    result.AudioSpecificConfig =
                        ParseEsds(
                            child.Data);

                    break;
                }
            }

            if (result.SampleRate == 0)
                throw new InvalidDataException(
                    "AAC sample rate is zero.");

            if (result.ChannelCount == 0)
                throw new InvalidDataException(
                    "AAC channel count is zero.");
        }

        private static byte[] ParseEsds(
  byte[] data)
        {
    if (data.Length < 4)
     throw new InvalidDataException(
  "Invalid esds box.");

  byte[] result =
    FindDescriptor(
     data,
           4,
        data.Length,
  0x05);

       if (result == null)
      throw new InvalidDataException(
        "esds contains no AudioSpecificConfig.");

            return result;
  }

        // MPEG-4 descriptors nest (ES_Descriptor 0x03 contains
        // DecoderConfigDescriptor 0x04, which contains
        // DecoderSpecificInfo 0x05). This walks the descriptor tree
      // instead of treating it as a flat sibling list, otherwise
        // the AudioSpecificConfig (tag 0x05) is never found.
        private static byte[] FindDescriptor(
   byte[] data,
int start,
            int end,
            int targetTag)
        {
      int position = start;

while (position < end)
            {
        if (position >= data.Length)
          throw new InvalidDataException(
     "Invalid MPEG-4 descriptor.");

     int tag = data[position++];

     int length =
             ReadDescriptorLength(
data,
            ref position);

    if (length < 0 ||
                position + length > end)
       {
    throw new InvalidDataException(
         "Invalid MPEG-4 descriptor.");
       }

              int payloadStart = position;
        int payloadEnd = position + length;

  if (tag == targetTag)
            {
          return Mp4BinaryReader.Copy(
                 data,
       payloadStart,
               length);
    }

      if (tag == 0x03)
         {
 // ES_Descriptor: ES_ID(2) + flags(1), plus
       // optional fields gated by the flags byte.
          int innerPos = payloadStart + 2;

   if (innerPos >= payloadEnd)
      throw new InvalidDataException(
     "Invalid ES_Descriptor.");

 byte flags = data[innerPos];
         innerPos++;

    bool streamDependenceFlag =
  (flags & 0x80) != 0;

      bool urlFlag =
       (flags & 0x40) != 0;

         bool ocrStreamFlag =
           (flags & 0x20) != 0;

              if (streamDependenceFlag)
       innerPos += 2;

        if (urlFlag)
           {
  if (innerPos >= payloadEnd)
         throw new InvalidDataException(
       "Invalid ES_Descriptor URL.");

         int urlLength = data[innerPos];
       innerPos += 1 + urlLength;
             }

         if (ocrStreamFlag)
            innerPos += 2;

           byte[] nested =
   FindDescriptor(
  data,
           innerPos,
                  payloadEnd,
            targetTag);

        if (nested != null)
     return nested;
     }
      else if (tag == 0x04)
    {
            // DecoderConfigDescriptor: objectTypeIndication(1)
                 // + streamType/upstream/reserved(1) +
            // bufferSizeDB(3) + maxBitrate(4) + avgBitrate(4)
            // = 13 fixed bytes before nested descriptors.
      int innerPos = payloadStart + 13;

      if (innerPos <= payloadEnd)
    {
       byte[] nested =
     FindDescriptor(
     data,
   innerPos,
             payloadEnd,
         targetTag);

           if (nested != null)
  return nested;
    }
          }

        position = payloadEnd;
      }

            return null;
        }

        private static int ReadDescriptorLength(
            byte[] data,
            ref int position)
        {
            int value = 0;

            for (int i = 0;
                 i < 4;
                 i++)
            {
                if (position >= data.Length)
                    throw new InvalidDataException(
                        "Invalid descriptor length.");

                byte b =
                    data[position++];

                value =
                    (value << 7) |
                    (b & 0x7F);

                if ((b & 0x80) == 0)
                    return value;
            }

            throw new InvalidDataException(
                "Descriptor length is too large.");
        }
    }
}