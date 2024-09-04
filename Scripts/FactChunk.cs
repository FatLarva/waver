using System;
using System.Buffers.Binary;
using System.Text;

namespace WavEncoding
{
    public readonly struct FactChunk
    {
        public const string Id = "fact";
        private const uint WholeChunkSize = 12;

        public readonly uint ChunkSize;
        public readonly uint NumberOfSamples;
        
        public uint ChunkWithHeaderSize => WholeChunkSize;

        public FactChunk(int numberOfSamples)
        {
            ChunkSize = 4;
            NumberOfSamples = (uint)numberOfSamples;
        }

        private FactChunk(in ReadOnlySpan<byte> buffer)
        {
            ChunkSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer[0..4]);
            NumberOfSamples = BinaryPrimitives.ReadUInt32LittleEndian(buffer[4..8]);
        }

        public static bool TryReadAndShift(ref ReadOnlySpan<byte> buffer, out FactChunk factChunk)
        {
            var isFactChunk = buffer.Slice(0, 4).IsEqualToString(Id);
            if (!isFactChunk)
            {
                factChunk = default;

                return false;
            }

            factChunk = new FactChunk(buffer.Slice(4));
            buffer = buffer.Slice((int)factChunk.ChunkWithHeaderSize);

            return true;
        }

        public int WriteAndShift(ref Span<byte> buffer)
        {
            // Chunk header
            Encoding.ASCII.GetBytes(Id, buffer[0..4]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[4..8], ChunkSize);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[8..12], NumberOfSamples);

            buffer = buffer.Slice((int)WholeChunkSize);

            return (int)WholeChunkSize;
        }
    }
}
