using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
// ReSharper disable PartialTypeWithSinglePart

namespace CollapseLauncher
{
    internal sealed partial class BridgedNetworkStream(HttpResponseMessage networkResponse, Stream networkStream) : Stream
    {
        internal static async ValueTask<BridgedNetworkStream> CreateStream(HttpResponseMessage networkResponse, CancellationToken token)
        {
            Stream networkStream = await networkResponse.Content.ReadAsStreamAsync(token);
            return new BridgedNetworkStream(networkResponse, networkStream);
        }

        ~BridgedNetworkStream() => Dispose();

        private int ReadBytes(Span<byte> buffer) => networkStream.Read(buffer);

        private int ReadBytes(byte[] buffer, int offset, int count) => networkStream.Read(buffer, offset, count);

        public override int Read(Span<byte> buffer) => ReadBytes(buffer);
        public override int Read(byte[] buffer, int offset, int count) => ReadBytes(buffer, offset, count);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            networkStream.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            networkStream.ReadAsync(buffer, cancellationToken);

        public new async ValueTask ReadExactlyAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int totalRead = 0;
            while (totalRead < buffer.Length)
            {
                int read = await ReadAsync(buffer[totalRead..], cancellationToken);
                if (read == 0) return;

                totalRead += read;
            }
        }

        public override void Write(ReadOnlySpan<byte> buffer) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override void Flush() => networkStream.Flush();

        public override long Length { get; } = networkResponse?.Content.Headers.ContentLength ?? 0;

        public override long Position
        {
            get { throw new NotSupportedException(); }
            set { throw new NotSupportedException(); }
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();

        public override void SetLength(long value) => throw new NotImplementedException();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
            {
                return;
            }

            networkResponse?.Dispose();
            networkStream?.Dispose();
        }

        /// <inheritdoc/>
        public override async ValueTask DisposeAsync()
        {
            networkResponse?.Dispose();
            if (networkStream != null)
                await networkStream.DisposeAsync();

            await base.DisposeAsync();
            GC.SuppressFinalize(this);
        }
    }
}
