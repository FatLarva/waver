namespace WavEncoding
{
    public enum WaveFormat : ushort
    {
        WaveFormatPCM = 0x0001,	// PCM
        WaveFormatIeeeFloat = 0x0003,	// IEEE float
        WaveFormatAlaw = 0x0006,	// 8-bit ITU-T G.711 A-law
        WaveFormatMulaw = 0x0007,	// 8-bit ITU-T G.711 µ-law
        WaveFormatExtensible = 0xFFFE,	// Determined by SubFormat 'field'
    }
}