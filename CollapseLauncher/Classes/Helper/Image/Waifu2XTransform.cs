using Hi3Helper;
using PhotoSauce.MagicScaler;
using PhotoSauce.MagicScaler.Transforms;
using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

namespace CollapseLauncher.Helper.Image
{
    public class Waifu2XTransform(Waifu2X context) : IPixelTransform
    {
        private          IPixelSource _source;
        private          byte[]       _src;
        private          byte[]       _buffer;

        private ReadOnlySpan<byte> Span => _buffer;
        public Guid Format => _source.Format;
        public int Width => _source.Width * 2;
        public int Height => _source.Height * 2;

        private int Channel
        {
            get
            {
                if (Format == PixelFormats.Grey8bpp) return 1;
                if (Format == PixelFormats.Bgr24bpp) return 3;
                if (Format == PixelFormats.Bgra32bpp) return 4;
                throw new NotSupportedException("Pixel format not supported.");
            }
        }

        private int Stride => Width * Channel;

        public void Init(IPixelSource source)
        {
            _source = source;
            _buffer = new byte[Width * Height * Channel];
            int w = source.Width, h = source.Height;
            _src = new byte[w * h * Channel];
            source.CopyPixels(new Rectangle(0, 0, w, h), w * Channel, _src);

            Logger.LogWriteLine($"Waifu2X processing begins. Source image resolution: {w}x{h}.");
            var timeBegin = DateTime.Now;
            context.Process(source.Width, source.Height, Channel, _src, _buffer);
            Logger.LogWriteLine($"Waifu2X processing ends. Source image resolution: {w}x{h}. Elapsed time: {(DateTime.Now - timeBegin).TotalMilliseconds} ms.");
        }

        public void CopyPixels(Rectangle sourceArea, int cbStride, Span<byte> buffer)
        {
            var (rx, ry, rw, rh) = (sourceArea.X, sourceArea.Y, sourceArea.Width, sourceArea.Height);
            var bpp = Channel;
            var cb = rw * bpp;

            if (rx < 0 || ry < 0 || rw < 0 || rh < 0 || rx + rw > Width || ry + rh > Height)
                throw new ArgumentOutOfRangeException(nameof(sourceArea),
                    "Requested area does not fall within the image bounds");

            if (cb > cbStride)
                throw new ArgumentOutOfRangeException(nameof(cbStride), "Stride is too small for the requested area");

            if ((rh - 1) * cbStride + cb > buffer.Length)
                throw new ArgumentOutOfRangeException(nameof(buffer), "Buffer is too small for the requested area");

            ref var pixRef = ref Unsafe.Add(ref MemoryMarshal.GetReference(Span), ry * Stride + rx * bpp);
            for (var y = 0; y < rh; y++)
                Unsafe.CopyBlockUnaligned(ref buffer[y * cbStride], ref Unsafe.Add(ref pixRef, y * Stride), (uint) cb);
        }
    }
}
