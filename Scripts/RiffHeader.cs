using System;
using System.Buffers.Binary;
using System.Text;

namespace WavEncoding
{
    public readonly struct RiffHeader
    {
        public const uint HeaderSize = 8;
        public const uint FormatFieldSize = 4;
        
        private const string Id = "RIFF";
        private const string Format = "WAVE";

        public readonly uint FileSize;
        public readonly uint ChunkWithHeaderSize;

        public uint HeaderOnlySize => HeaderSize + FormatFieldSize;

        public RiffHeader(uint fileSize)
        {
            FileSize = fileSize;
            ChunkWithHeaderSize = FileSize + HeaderSize;
        }
        
        public static bool TryReadAndShift(ref ReadOnlySpan<byte> buffer, out RiffHeader riffHeader)
        {
            var isRiffFile = buffer.Slice(0, 4).IsEqualToString(Id);
            if (!isRiffFile)
            {
                riffHeader = default;

                return false;
            }
                
            var isWavFile = buffer.Slice(8, 4).IsEqualToString(Format);
            if (!isWavFile)
            {
                riffHeader = default;

                return false;
            }

            var fileLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(4, 4));
            riffHeader = new RiffHeader(fileLength);

            buffer = buffer.Slice((int)riffHeader.HeaderOnlySize);
            
            return true;
        }
            
        public int WriteAndShift(ref Span<byte> buffer)
        {
            Encoding.ASCII.GetBytes(Id, buffer[0..4]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[4..8], FileSize);
            Encoding.ASCII.GetBytes(Format, buffer[8..12]);

            buffer = buffer.Slice((int)(HeaderSize + FormatFieldSize));

            return (int)(HeaderSize + FormatFieldSize);
        }
    }
}
