namespace WavEncoding
{
    public enum FmtChunkSize : uint
    {
        Unsupported = 0,
        SimplePcm = 16,
        EmptyExtra = 18,
        FullExtensible = 40,
    }
}
