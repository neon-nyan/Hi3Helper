using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable PartialTypeWithSinglePart

#nullable enable
namespace CollapseLauncher.Helper.StreamUtility
{
    internal sealed partial class JsonFieldToEnumerableStream : Stream
    {
        private readonly Stream _redirectStream;
        private readonly Stream _innerStream;
        private readonly byte[] _innerBuffer = new byte[16 << 10];
        private readonly char[] _searchStartValuesUtf16;
        private readonly byte[] _searchStartValuesUtf8;
        private readonly char[] _searchEndValuesUtf16 = "]\r\n}".ToCharArray();
        private readonly byte[] _searchEndValuesUtf8  = "]\r\n}"u8.ToArray();
        private          bool   _isFieldStart;
        private          bool   _isFieldEnd;

        internal JsonFieldToEnumerableStream(string? redirectedToFilePath, Stream sourceStream, string fieldSource = "files", string? customSearchStartFormat = null)
        {
            _innerStream = sourceStream;
            if (!string.IsNullOrWhiteSpace(redirectedToFilePath))
            {
                string? filePathDir = Path.GetDirectoryName(redirectedToFilePath);
                if (!string.IsNullOrEmpty(filePathDir) && !Directory.Exists(filePathDir))
                {
                    Directory.CreateDirectory(filePathDir);
                }
                _redirectStream = File.Open(redirectedToFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
            }
            else
            {
                _redirectStream = Null;
            }
            InitializeStartValue(fieldSource, out _searchStartValuesUtf16, out _searchStartValuesUtf8, customSearchStartFormat);
        }

        internal JsonFieldToEnumerableStream(Stream redirectedToStream, Stream sourceStream, string fieldSource = "files", string? customSearchStartFormat = null)
        {
            _innerStream = sourceStream;
            _redirectStream = redirectedToStream;

            InitializeStartValue(fieldSource, out _searchStartValuesUtf16, out _searchStartValuesUtf8, customSearchStartFormat);
        }

        private static void InitializeStartValue(string fieldSource, out char[] startValuesUtf16, out byte[] startValuesUtf8, string? customSearchStartFormat)
        {
            string fieldSourceStart = string.Format(customSearchStartFormat ?? "\"{0}\": [", fieldSource);
            startValuesUtf16 = fieldSourceStart.ToCharArray();
            startValuesUtf8  = Encoding.UTF8.GetBytes(fieldSourceStart);
        }

        public override bool CanRead => true;

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => throw new NotImplementedException();

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            _innerStream.Flush();
            _redirectStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count) => InternalRead(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer) => InternalRead(buffer);

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => await InternalReadAsync(buffer.AsMemory(offset, count), cancellationToken);

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => await InternalReadAsync(buffer, cancellationToken);

        private async ValueTask<int> InternalReadAsync(Memory<byte> buffer, CancellationToken token)
        {
            if (_isFieldEnd)
                return 0;

            Start:
            // - 8 is important to ensure that the EOF detection is working properly
            int toRead = Math.Min(_innerBuffer.Length - 8, buffer.Length);
            int read = await _innerStream.ReadAtLeastAsync(_innerBuffer.AsMemory(0, toRead), toRead, false, token);
            if (read == 0)
                return 0;

            await _redirectStream.WriteAsync(_innerBuffer.AsMemory(0, read), token);

            int lastIndexOffset;
            if (_isFieldStart && (lastIndexOffset = EnsureIsEnd(_innerBuffer)) > 0)
            {
                _innerBuffer.AsSpan(0, lastIndexOffset).CopyTo(buffer.Span);
                _isFieldEnd = true;
                return lastIndexOffset;
            }

            int offset = 0;
            if (!_isFieldStart && !(_isFieldStart = !((offset = EnsureIsStart(_innerBuffer)) < 0)))
                goto Start;

            bool isOneGoBufferLoadEnd = read < toRead && _isFieldStart && !_isFieldEnd;
            ReadOnlySpan<byte> spanToCopy = isOneGoBufferLoadEnd ? _innerBuffer.AsSpan(offset, read - offset).TrimEnd((byte)'}')
                : _innerBuffer.AsSpan(offset, read - offset);

            spanToCopy.CopyTo(buffer.Span);
            return isOneGoBufferLoadEnd ? spanToCopy.Length : read - offset;
        }

        private int InternalRead(Span<byte> buffer)
        {
            if (_isFieldEnd)
                return 0;

            Start:
            // - 8 is important to ensure that the EOF detection is working properly
            int toRead = Math.Min(_innerBuffer.Length - 8, buffer.Length);
            int read = _innerStream.ReadAtLeast(_innerBuffer.AsSpan(0, toRead), toRead, false);
            if (read == 0)
                return 0;

            _redirectStream.Write(_innerBuffer, 0, read);

            int lastIndexOffset;
            if (_isFieldStart && (lastIndexOffset = EnsureIsEnd(_innerBuffer)) > 0)
            {
                _innerBuffer.AsSpan(0, lastIndexOffset).CopyTo(buffer);
                _isFieldEnd = true;
                return lastIndexOffset;
            }

            int offset = 0;
            if (!_isFieldStart && !(_isFieldStart = !((offset = EnsureIsStart(_innerBuffer)) < 0)))
                goto Start;

            _innerBuffer.AsSpan(offset, read - offset).CopyTo(buffer);
            return read - offset;
        }

        private int EnsureIsEnd(Span<byte> buffer)
        {
            ReadOnlySpan<char> bufferAsChars = MemoryMarshal.Cast<byte, char>(buffer);

            int lastIndexOfAnyUtf8 = buffer.LastIndexOf(_searchEndValuesUtf8);
            if (lastIndexOfAnyUtf8 >= _searchEndValuesUtf8.Length)
            {
                return lastIndexOfAnyUtf8 + 1;
            }

            int lastIndexOfAnyUtf16 = bufferAsChars.LastIndexOf(_searchEndValuesUtf16);
            return lastIndexOfAnyUtf16 > 0 ? lastIndexOfAnyUtf16 + 1 : -1;

        }

        private int EnsureIsStart(Span<byte> buffer)
        {
            ReadOnlySpan<char> bufferAsChars = MemoryMarshal.Cast<byte, char>(buffer);

            int indexOfAnyUtf8 = buffer.IndexOf(_searchStartValuesUtf8);
            if (indexOfAnyUtf8 >= _searchStartValuesUtf8.Length)
            {
                return indexOfAnyUtf8 + (_searchStartValuesUtf8.Length - 1);
            }

            int indexOfAnyUtf16 = bufferAsChars.IndexOf(_searchStartValuesUtf16);
            return indexOfAnyUtf16 > 0 ? indexOfAnyUtf16 + (_searchStartValuesUtf16.Length - 1) : -1;

        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override async ValueTask DisposeAsync()
        {
            await _innerStream.DisposeAsync();
            await _redirectStream.DisposeAsync();
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                return;
            }

            _innerStream.Dispose();
            _redirectStream.Dispose();
        }
    }
}
