using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using UnityEngine;

namespace WavEncoding
{
    // Wav-file header implementation based on http://soundfile.sapp.org/doc/WaveFormat/
    // Wav-file header implementation based on https://www-mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html
    public readonly struct WavHeader
    {
        private readonly RiffHeader _riffHeader;
        private readonly FmtChunk _fmtChunk;
        private readonly FactChunk? _factChunk;
        private readonly DataChunkHeader _dataChunk;

        public RiffHeader RiffHeader => _riffHeader;
        public FmtChunk FmtChunk => _fmtChunk;
        public FactChunk FactChunk => _factChunk ?? default;
        public DataChunkHeader DataChunkHeader => _dataChunk;

        public readonly uint Size;

        public WavHeader(int numberOfSamples, int numChannels, int sampleRate, int bitsPerSample, WaveFormat format)
        {
            var bytesPerSample = (ushort)(bitsPerSample / 8);
            var dataSizeInBytes = numberOfSamples * numChannels * bytesPerSample;
            
            _fmtChunk = new FmtChunk(numChannels, sampleRate, bitsPerSample, format);
            _factChunk = new FactChunk(numberOfSamples);

            var dataChunkSize = (uint)dataSizeInBytes + DataChunkHeader.HeaderSize;
            var fileSize = RiffHeader.FormatFieldSize + _fmtChunk.ChunkWithHeaderSize + _factChunk.Value.ChunkWithHeaderSize + dataChunkSize;
            
            _riffHeader = new RiffHeader(fileSize);

            Size = _riffHeader.HeaderOnlySize + _fmtChunk.ChunkWithHeaderSize + _factChunk.Value.ChunkWithHeaderSize + DataChunkHeader.HeaderSize;
            
            _dataChunk = new DataChunkHeader(dataSizeInBytes, Size);
        }
        
        public WavHeader(RiffHeader riffHeader, FmtChunk fmtChunk, DataChunkHeader dataChunkHeader, FactChunk? factChunk)
        {
            _riffHeader = riffHeader;
            _fmtChunk = fmtChunk;
            _factChunk = factChunk;
            _dataChunk = dataChunkHeader;

            Size = _riffHeader.HeaderOnlySize + _fmtChunk.ChunkWithHeaderSize + _dataChunk.HeaderOnlySize + (_factChunk?.ChunkWithHeaderSize ?? 0);
        }
        
        public int WriteToSpan(in Span<byte> targetSpan)
        {
            var pointerSpan = targetSpan;
            
            int bytesWritten = 0;
            bytesWritten += _riffHeader.WriteAndShift(ref pointerSpan);
            bytesWritten += _fmtChunk.WriteAndShift(ref pointerSpan);
            bytesWritten += _factChunk?.WriteAndShift(ref pointerSpan) ?? 0;
            bytesWritten += _dataChunk.WriteAndShift(ref pointerSpan);

            return bytesWritten;
        }

        public static bool ReadFromFileStream(Stream fileStream, out WavHeader resultHeader)
        {
            if (!TryReadRiffHeader(fileStream, out RiffHeader riffHeader))
            {
                resultHeader = default;

                return false;
            }

            var headersStorage = new HeadersStorage();
            headersStorage.RiffHeader = riffHeader;
            
            while (fileStream.Position < fileStream.Length)
            {
                if (!TryReadNextChunk(fileStream, headersStorage))
                {
                    resultHeader = default;

                    return false;
                }
            }

            if (!headersStorage.HasNecessaryInfo)
            {
                resultHeader = default;
                
                return false;
            }

            resultHeader = new WavHeader(headersStorage.RiffHeader.Value, headersStorage.FmtChunk.Value, headersStorage.DataChunkHeader.Value, headersStorage.FactChunk);
            
            return true;
        }

        private static bool TryReadRiffHeader(Stream fileStream, out RiffHeader riffHeader)
        {
            const int riffHeaderSize = 12;
            Span<byte> riffHeaderBytes = stackalloc byte[riffHeaderSize];

            var bytesRead = fileStream.Read(riffHeaderBytes);
            if (bytesRead != riffHeaderSize)
            {
                riffHeader = default;
                return false;
            }

            ReadOnlySpan<byte> readOnlySpan = riffHeaderBytes;

            if (!RiffHeader.TryReadAndShift(ref readOnlySpan, out riffHeader))
            {
                riffHeader = default;
                return false;
            }

            return true;
        }
        
        private static bool TryReadNextChunk(Stream stream, HeadersStorage headersStorage)
        {
            if (!TryPeekIffHeader(stream, out string chunkId, out uint size))
            {
                return false;
            }

            switch (chunkId)
            {
                case FmtChunk.Id:
                    return TryReadFmtChunk(stream, size, headersStorage);
                
                case FactChunk.Id:
                    return TryReadFactChunk(stream, size, headersStorage);
                
                case DataChunkHeader.Id:
                    return TryReadDataChunk(stream, size, headersStorage);
            }
            
            Debug.LogWarning($"Unsupported data chunk id: {chunkId}");
            
            return SkipUnsupportedChunkInStream(stream, size);
        }

        private static bool SkipUnsupportedChunkInStream(Stream stream, uint size)
        {
            var bytesToSkip = size + 8;
            if (stream.Position + bytesToSkip <= stream.Length)
            {
                stream.Position += bytesToSkip;

                return true;
            }

            return false;
        }

        private static bool TryReadDataChunk(Stream stream, uint size, HeadersStorage headersStorage)
        {
            Span<byte> buffer = stackalloc byte[(int)DataChunkHeader.HeaderSize];

            ForceReadBytesOrDie(stream, buffer);
            
            ReadOnlySpan<byte> readOnlyBuffer = buffer;
            
            if (DataChunkHeader.TryReadAndShift(ref readOnlyBuffer, (uint)stream.Position, out DataChunkHeader dataChunkHeader))
            {
                headersStorage.DataChunkHeader = dataChunkHeader;
                stream.Position += dataChunkHeader.ChunkWithHeaderSize - 8;
                
                return true;
            }

            return false;
        }

        private static bool TryReadFactChunk(Stream stream, uint size, HeadersStorage headersStorage)
        {
            Span<byte> buffer = stackalloc byte[(int)size + 8];

            ForceReadBytesOrDie(stream, buffer);
            
            ReadOnlySpan<byte> readOnlyBuffer = buffer;
            
            if (FactChunk.TryReadAndShift(ref readOnlyBuffer, out FactChunk factChunk))
            {
                headersStorage.FactChunk = factChunk;

                return true;
            }

            return false;
        }

        private static bool TryReadFmtChunk(Stream stream, uint size, HeadersStorage headersStorage)
        {
            Span<byte> buffer = stackalloc byte[(int)size + 8];

            ForceReadBytesOrDie(stream, buffer);
            
            ReadOnlySpan<byte> readOnlyBuffer = buffer;
            
            if (FmtChunk.TryReadAndShift(ref readOnlyBuffer, out var fmtChunk))
            {
                headersStorage.FmtChunk = fmtChunk;

                return true;
            }
               
            return false;
        }

        private static void ForceReadBytesOrDie(Stream stream, in Span<byte> buffer)
        {
            var safetyCounter = 1000;
            var bytesRead = stream.Read(buffer);
            
            while (bytesRead != buffer.Length && safetyCounter-- > 0)
            {
                bytesRead += stream.Read(buffer[bytesRead..]);
            }

            if (bytesRead != buffer.Length)
            {
                throw new InvalidOperationException("Unable to read necessary amount of bytes from stream.");
            }
        }

        private static bool TryPeekIffHeader(Stream stream, out string chunkId, out uint size)
        {
            Span<byte> iffHeader = stackalloc byte[8];

            var bytesRead = stream.Read(iffHeader);
            stream.Position -= bytesRead;

            if (bytesRead != iffHeader.Length)
            {
                chunkId = default;
                size = default;
                
                return false;
            }

            chunkId = Encoding.ASCII.GetString(iffHeader[0..4]);
            size = BinaryPrimitives.ReadUInt32LittleEndian(iffHeader[4..8]);

            return true;
        }
    }
}
