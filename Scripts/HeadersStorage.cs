namespace WavEncoding
{
    internal class HeadersStorage
    {
        public RiffHeader? RiffHeader;
        public FmtChunk? FmtChunk;
        public DataChunkHeader? DataChunkHeader;
        public FactChunk? FactChunk;

        public bool HasNecessaryInfo => RiffHeader.HasValue && FmtChunk.HasValue && DataChunkHeader.HasValue;
    }
}
