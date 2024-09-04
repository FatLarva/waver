using System;
using System.Buffers.Binary;
using System.Text;

namespace WavEncoding
{
    public readonly struct FmtChunk
    {
        public const string Id = "fmt ";
        private const uint HeaderSize = 8;

        public readonly FmtChunkSize ChunkSize;
        public readonly WaveFormat WaveFormat;
        public readonly ushort ChannelsCount;
        public readonly uint SamplesPerSecond;
        public readonly uint BytesPerSecond;
        public readonly ushort BlockAlign;
        public readonly ushort BitsPerSample;

        public readonly ushort SizeOfTheExtensionInBits;
        public readonly ushort ValidBitsPerSample;
        public readonly uint ChannelMask;
        public readonly SubFormatGuid SubFormat;

        public readonly uint ChunkWithHeaderSize;
        
        public FmtChunk(int numChannels, int sampleRate, PcmFormatVariant pcmVariant)
        {
            ChannelsCount = (ushort)numChannels;
            SamplesPerSecond = (uint)sampleRate;

            BitsPerSample = (ushort)(pcmVariant == PcmFormatVariant.Byte8 ? 8 : 16);
            var bytesPerSample = BitsPerSample / 8;
            BytesPerSecond = (uint)(sampleRate * numChannels * bytesPerSample);
            BlockAlign = (ushort)(numChannels * bytesPerSample);

            ChunkSize = FmtChunkSize.SimplePcm;
            WaveFormat = WaveFormat.WaveFormatPCM;

            SizeOfTheExtensionInBits = default;
            ValidBitsPerSample = default;
            ChannelMask = default;
            SubFormat = default;

            ChunkWithHeaderSize = (uint)ChunkSize + HeaderSize;
        }

        public FmtChunk(int numChannels, int sampleRate, int bitsPerSample, WaveFormat waveFormat, int channelMask = 0)
        {
            if (waveFormat == WaveFormat.WaveFormatAlaw || waveFormat == WaveFormat.WaveFormatMulaw)
            {
                throw new ArgumentOutOfRangeException(nameof(waveFormat), $"Currently unsupported format: {waveFormat}");
            }

            ChannelsCount = (ushort)numChannels;
            SamplesPerSecond = (uint)sampleRate;

            BitsPerSample = (ushort)bitsPerSample;
            var bytesPerSample = BitsPerSample / 8;
            BytesPerSecond = (uint)(sampleRate * numChannels * bytesPerSample);
            BlockAlign = (ushort)(numChannels * bytesPerSample);

            WaveFormat = waveFormat;
            ChunkSize = FmtChunkSize.EmptyExtra;
            SizeOfTheExtensionInBits = 0;
            ValidBitsPerSample = default;
            ChannelMask = default;
            SubFormat = default;

            var isTooManyChannels = numChannels > 2;
            // var isTooManyBits = bitsPerSample > 16;
            var isExplicitlySet = waveFormat == WaveFormat.WaveFormatExtensible;

            if (isTooManyChannels /*|| isTooManyBits*/ || isExplicitlySet)
            {
                WaveFormat = WaveFormat.WaveFormatExtensible;
                ChunkSize = FmtChunkSize.FullExtensible;
                SizeOfTheExtensionInBits = 22;
                ValidBitsPerSample = (ushort)bitsPerSample;
                ChannelMask = (uint)channelMask;
                SubFormat = new SubFormatGuid(WaveFormat.WaveFormatExtensible);
            }
            
            ChunkWithHeaderSize = (uint)ChunkSize + HeaderSize;
        }

        private FmtChunk(in ReadOnlySpan<byte> buffer)
        {
            ChunkSize = (FmtChunkSize)BinaryPrimitives.ReadUInt32LittleEndian(buffer[0..4]);

            if (ChunkSize == FmtChunkSize.Unsupported)
            {
                throw new ArgumentOutOfRangeException(nameof(buffer), "buffer contains unsupported chunkSize");
            }

            WaveFormat = (WaveFormat)BinaryPrimitives.ReadUInt16LittleEndian(buffer[4..6]);

            if (WaveFormat is WaveFormat.WaveFormatAlaw or WaveFormat.WaveFormatMulaw)
            {
                throw new ArgumentOutOfRangeException(nameof(buffer), $"buffer contains unsupported Format: {WaveFormat}");
            }

            ChannelsCount = BinaryPrimitives.ReadUInt16LittleEndian(buffer[6..8]);
            SamplesPerSecond = BinaryPrimitives.ReadUInt32LittleEndian(buffer[8..12]);
            BytesPerSecond = BinaryPrimitives.ReadUInt32LittleEndian(buffer[12..16]);
            BlockAlign = BinaryPrimitives.ReadUInt16LittleEndian(buffer[16..18]);
            BitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(buffer[18..20]);

            SizeOfTheExtensionInBits = default;
            ValidBitsPerSample = default;
            ChannelMask = default;
            SubFormat = default;
            ChunkWithHeaderSize = (uint)ChunkSize + HeaderSize;

            if (ChunkSize == FmtChunkSize.SimplePcm)
            {
                return;
            }

            SizeOfTheExtensionInBits = BinaryPrimitives.ReadUInt16LittleEndian(buffer[20..22]);

            if (ChunkSize == FmtChunkSize.EmptyExtra)
            {
                return;
            }

            ValidBitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(buffer[22..24]);
            ChannelMask = BinaryPrimitives.ReadUInt32LittleEndian(buffer[24..28]);

            if (!SubFormatGuid.ReadFromSpan(buffer[28..44], out SubFormat))
            {
                throw new ArgumentOutOfRangeException(nameof(buffer), "Invalid guid sequence.");
            }
        }

        public static bool TryReadAndShift(ref ReadOnlySpan<byte> buffer, out FmtChunk fmtChunk)
        {
            var isFmtChunk = buffer.Slice(0, 4).IsEqualToString(Id);
            if (!isFmtChunk)
            {
                fmtChunk = default;

                return false;
            }

            fmtChunk = new FmtChunk(buffer.Slice(4));
            buffer = buffer.Slice((int)fmtChunk.ChunkWithHeaderSize);
            
            return true;
        }

        public int WriteAndShift(ref Span<byte> buffer)
        {
            // Chunk header
            var chunkSizeUint = (uint)ChunkSize;
            Encoding.ASCII.GetBytes(Id, buffer[0..4]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[4..8], chunkSizeUint);

            // Chunk body
            var body = buffer.Slice(8, (int)chunkSizeUint);

            BinaryPrimitives.WriteUInt16LittleEndian(body[0..2], (ushort)WaveFormat);
            BinaryPrimitives.WriteUInt16LittleEndian(body[2..4], ChannelsCount);
            BinaryPrimitives.WriteUInt32LittleEndian(body[4..8], SamplesPerSecond);
            BinaryPrimitives.WriteUInt32LittleEndian(body[8..12], BytesPerSecond);
            BinaryPrimitives.WriteUInt16LittleEndian(body[12..14], BlockAlign);
            BinaryPrimitives.WriteUInt16LittleEndian(body[14..16], BitsPerSample);

            if (ChunkSize == FmtChunkSize.SimplePcm)
            {
                buffer = buffer.Slice((int)ChunkWithHeaderSize);

                return (int)ChunkWithHeaderSize;
            }

            BinaryPrimitives.WriteUInt16LittleEndian(body[16..18], SizeOfTheExtensionInBits);

            if (ChunkSize == FmtChunkSize.EmptyExtra)
            {
                buffer = buffer.Slice((int)ChunkWithHeaderSize);

                return (int)ChunkWithHeaderSize;
            }

            BinaryPrimitives.WriteUInt16LittleEndian(body[18..20], ValidBitsPerSample);
            BinaryPrimitives.WriteUInt32LittleEndian(body[20..24], ChannelMask);
            SubFormat.WriteToSpan(body[24..40]);

            buffer = buffer.Slice((int)ChunkWithHeaderSize);

            return (int)ChunkWithHeaderSize;
        }
    }
}
