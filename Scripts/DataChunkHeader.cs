using System;
using System.Buffers.Binary;
using System.Text;

namespace WavEncoding
{
    public readonly struct DataChunkHeader
    {
        public const string Id = "data";
        public const uint HeaderSize = 8;

        public readonly uint ChunkSize;
        public readonly uint ChunkWithHeaderSize;
        public readonly uint DataStartPosition;

        public uint HeaderOnlySize => HeaderSize;

        public DataChunkHeader(int dataSizeInBytes, uint dataStartPosition)
        {
            DataStartPosition = dataStartPosition;
            ChunkSize = (uint)dataSizeInBytes;
            var oddByteAdding = dataSizeInBytes % 2 != 0 ? 1 : 0;
            ChunkWithHeaderSize = HeaderSize + ChunkSize + (uint)oddByteAdding;
        }

        private DataChunkHeader(in ReadOnlySpan<byte> buffer, uint dataStartPosition)
        {
            DataStartPosition = dataStartPosition;
            ChunkSize = BinaryPrimitives.ReadUInt32LittleEndian(buffer[0..4]);
            ChunkWithHeaderSize = HeaderSize + ChunkSize;
        }

        public static bool TryReadAndShift(ref ReadOnlySpan<byte> buffer, uint dataStartPosition, out DataChunkHeader dataChunkHeader)
        {
            var isDataChunk = buffer.Slice(0, 4).IsEqualToString(Id);
            if (!isDataChunk)
            {
                dataChunkHeader = default;

                return false;
            }

            dataChunkHeader = new DataChunkHeader(buffer.Slice(4), dataStartPosition);
            
            buffer = buffer.Slice((int)dataChunkHeader.HeaderOnlySize);

            return true;
        }

        public int WriteAndShift(ref Span<byte> buffer)
        {
            // Chunk header
            Encoding.ASCII.GetBytes(Id, buffer[0..4]);
            BinaryPrimitives.WriteUInt32LittleEndian(buffer[4..8], ChunkSize);

            buffer = buffer.Slice((int)HeaderSize);
            
            return (int)HeaderSize;
        }
    }
}
