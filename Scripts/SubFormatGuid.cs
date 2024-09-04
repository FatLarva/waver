using System;
using System.Buffers.Binary;
using System.Text;

namespace WavEncoding
{
    public readonly struct SubFormatGuid
    {
        private const string ConstantEnding = "\x00\x00\x00\x00\x10\x00\x80\x00\x00\xAA\x00\x38\x9B\x71";
        
        public readonly WaveFormat WaveFormat;

        public SubFormatGuid(WaveFormat waveFormat)
        {
            WaveFormat = waveFormat;
        }
        
        public static bool ReadFromSpan(in ReadOnlySpan<byte> buffer, out SubFormatGuid guid)
        {
            if (!buffer.Slice(2, 16).IsEqualToString(ConstantEnding))
            {
                guid = default;
                return false;
            }
            
            var format = (WaveFormat)BinaryPrimitives.ReadUInt16LittleEndian(buffer[0..2]);
            guid = new SubFormatGuid(format);

            return true;
        }
            
        public void WriteToSpan(in Span<byte> buffer)
        {
            BinaryPrimitives.WriteUInt16LittleEndian(buffer[0..2], (ushort)WaveFormat);
            Encoding.ASCII.GetBytes(ConstantEnding, buffer[2..16]);
        }
    }
}
