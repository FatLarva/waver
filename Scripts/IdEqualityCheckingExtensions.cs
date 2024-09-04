using System;
using System.Text;

namespace WavEncoding
{
    public static class IdEqualityCheckingExtensions
    {
        public static bool IsEqualToString(this ReadOnlySpan<byte> sequence, string id)
        {
            if (sequence.Length != id.Length)
            {
                return false;
            }

            Span<byte> idBytes = stackalloc byte[id.Length];
            Encoding.ASCII.GetBytes(id, idBytes);

            return sequence.SequenceEqual(idBytes);
        }
        
        public static bool IsEqualToString(this Span<byte> sequence, string id)
        {
            if (sequence.Length != id.Length)
            {
                return false;
            }

            Span<byte> idBytes = stackalloc byte[id.Length];
            Encoding.ASCII.GetBytes(id, idBytes);

            return sequence.SequenceEqual(idBytes);
        }
    }
}
