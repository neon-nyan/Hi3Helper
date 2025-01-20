// Original Source:
// https://raw.githubusercontent.com/LogosBible/bsdiff.net/master/src/bsdiff/BinaryPatchUtility.cs

// using SharpCompress.Compressors.BZip2;

using Hi3Helper.Data;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
// ReSharper disable CommentTypo

namespace CollapseLauncher
{
    /*
	The original bsdiff.c source code (http://www.daemonology.net/bsdiff/) is
	distributed under the following license:

	Copyright 2003-2005 Colin Percival
	All rights reserved

	Redistribution and use in source and binary forms, with or without
	modification, are permitted providing that the following conditions 
	are met:
	1. Redistributions of source code must retain the above copyright
		notice, this list of conditions and the following disclaimer.
	2. Redistributions in binary form must reproduce the above copyright
		notice, this list of conditions and the following disclaimer in the
		documentation and/or other materials provided with the distribution.

	THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
	IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
	WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
	ARE DISCLAIMED.  IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY
	DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
	DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS
	OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION)
	HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT,
	STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING
	IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
	POSSIBILITY OF SUCH DAMAGE.
	*/

    public sealed class BinaryPatchUtility
    {
        private const int CBufferSize = 32 << 10;
        private Stream InputStream { get; set; }
        private Stream OutputStream { get; set; }
        private Func<Stream> PatchStream { get; set; }
        private Stopwatch ProgressStopwatch { get; set; }
        private BinaryPatchProgress Progress { get; set; }

        private bool LeaveOpenStream { get; set; }
        private bool CanContinueApply { get; set; }

        private long ControlLength { get; set; }
        private long DiffLength { get; set; }
        private long NewSize { get; set; }

        public long GetNewSize() => NewSize;
        public event EventHandler<BinaryPatchProgress> ProgressChanged;
        private void UpdateProgress(long sizePatched, long sizeToBePatched, long read)
        {
            Progress.UpdatePatchEvent(sizePatched, sizeToBePatched, read, ProgressStopwatch.Elapsed.TotalSeconds);
            ProgressChanged?.Invoke(this, Progress);
        }

        /// <summary>
        /// Initializing Input, Patch and Output stream before applying binary patch/
        /// </summary>
        /// <param name="inputStream">A <see cref="Stream"/> containing the input data.</param>
        /// <param name="patchStream">A func that can open a <see cref="Stream"/> positioned at the start of the patch data.
        /// This stream must support reading and seeking, and <paramref name="patchStream"/> must allow multiple streams on
        /// the patch to be opened concurrently.</param>
        /// <param name="outputStream">A <see cref="Stream"/> to which the patched data is written.</param>
        /// <param name="leaveOpen">Leave the stream open.</param>
        public void Initialize(Stream inputStream, Func<Stream> patchStream, Stream outputStream, bool leaveOpen = true)
        {
            InputStream = inputStream;
            OutputStream = outputStream;
            PatchStream = patchStream;
            LeaveOpenStream = leaveOpen;
            ProgressStopwatch = Stopwatch.StartNew();

            ReadHeader();
        }

        public void Initialize(string inputPath, string patchPath, string outputPath, bool leaveOpen = false)
        {
            InputStream       = new FileStream(inputPath,  FileMode.Open,   FileAccess.Read, FileShare.Read);
            OutputStream      = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            PatchStream       = () => new FileStream(patchPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            LeaveOpenStream   = leaveOpen;
            ProgressStopwatch = Stopwatch.StartNew();

            ReadHeader();
        }

        private void ReadHeader()
        {
            // check arguments
            // ReSharper disable NotResolvedInText
            if (InputStream == null)
            {
                throw new ArgumentNullException("Input Stream cannot be null!");
            }
            if (PatchStream == null)
            {
                throw new ArgumentNullException("Patch cannot be null!");
            }
            if (OutputStream == null)
            {
                throw new ArgumentNullException("Output Stream cannot be null!");
            }
            // ReSharper restore NotResolvedInText
            /*
			File format:
				0	8	"BSDIFF40"
				8	8	X
				16	8	Y
				24	8	sizeof(newfile)
				32	X	bzip2(control block)
				32+X	Y	bzip2(diff block)
				32+X+Y	???	bzip2(extra block)
			with control block a set of triples (x,y,z) meaning "add x bytes
			from oldfile to x bytes from the diff block; copy y bytes from the
			extra block; seek forwards in oldfile by z bytes".
			*/
            // read header
            using Stream patchStream = PatchStream();
            // check patch stream capabilities
            // ReSharper disable NotResolvedInText
            if (!patchStream.CanRead)
            {
                throw new ArgumentException("Patch stream must be readable.", "_patchStream");
            }
            if (!patchStream.CanSeek)
            {
                throw new ArgumentException("Patch stream must be seekable.", "_patchStream");
            }
            // ReSharper restore NotResolvedInText

            Span<byte> header = stackalloc byte[CHeaderSize];
            patchStream.ReadExactly(header);

            // check for appropriate magic
            long signature = ReadInt64Sanity(header);
            if (signature != CFileSignature)
            {
                throw new InvalidOperationException("The patch file is not in a valid format!");
            }

            // read lengths from header
            ControlLength = ReadInt64Sanity(header[8..]);
            DiffLength    = ReadInt64Sanity(header[16..]);
            NewSize       = ReadInt64Sanity(header[24..]);
            if (ControlLength < 0 || DiffLength < 0 || NewSize < 0)
            {
                throw new InvalidOperationException($"The patch file may be corrupted! control: {ControlLength}, diff: {DiffLength}, newSize: {NewSize}");
            }

            CanContinueApply = true;
        }

        private static GZipStream TryGetCompressionStream(Stream source, long startPosition)
        {
            source.Position = startPosition;
            return new GZipStream(source, CompressionMode.Decompress, true);
        }
            /*
        {
            // Check if the stream is seekable and readable
            if (!source.CanRead) throw new InvalidOperationException("Stream is not readable!");
            if (!source.CanSeek) throw new InvalidOperationException("Stream is not seekable!");

            // Initialize the delegate function to return compressed stream
            Func<Stream>[] streams = new Func<Stream>[]
            {
                () => new CBZip2InputStream(source, true, true), // Commonly used compression in BZip2
                () => new GZipStream(source, CompressionMode.Decompress, true) // GZip compressed BSDiff used in miHoYo Games (like Honkai Impact 3rd)
            };
            Stream tempStream = null;

            // Try finding the compressed stream
            for (int i = 0; i < streams.Length; i++)
            {
                try
                {
                    source.Position = startPosition;
                    tempStream = streams[i]();
                    tempStream.ReadByte();
                    source.Position = startPosition;
                    return streams[i]();
                }
                catch { }
                finally
                {
                    tempStream?.Dispose();
                    tempStream = null;
                }
            }

            throw new FormatException("The diff file has unsupported compression format or the file is damaged/invalid!");
        }
            */

        private long[] ReadControlNumbers(Stream source, long newPosition)
        {
            Span<byte> buffer = stackalloc byte[8];
            long[] controls = new long[3];

            source.ReadExactly(buffer);
            controls[0] = ReadInt64Sanity(buffer); // Copy data control number

            source.ReadExactly(buffer);
            controls[1] = ReadInt64Sanity(buffer); // Additional/new data control number

            source.ReadExactly(buffer);
            controls[2] = ReadInt64Sanity(buffer); // Offset data control number

            // SANITY CHECK: If the new position + Copy data control number > new size, then throw.
            if (newPosition + controls[0] > NewSize)
                throw new InvalidDataException($"The patch file is corrupted! newPosition + control[0]/Copy control number ({newPosition} + {controls[0]}) > newSize ({NewSize})");

            // SANITY CHECK: If the copy control or new data control returns negative number, then throw.
            if (controls[0] < 0 || controls[1] < 0)
                throw new InvalidDataException($"The patch file is corrupted! control[0] ({controls[0]}) or control[1] ({controls[1]}) cannot be < 0!");

            return controls;
        }

        /// <summary>
        /// Applies a binary patch (in <a href="http://www.daemonology.net/bsdiff/">bsdiff</a> format) to the data in
        /// input stream and writes the results of patching to output stream defined after initialization.
        /// </summary>
        /// <exception cref="InvalidOperationException"></exception>
        /// <exception cref="InvalidDataException"></exception>
        public unsafe void Apply(CancellationToken token = default)
        {
            // check if apply can proceed
            if (!CanContinueApply)
            {
                throw new InvalidOperationException("You must initialize the patch before applying!");
            }

            // restart progress stopwatch
            ProgressStopwatch.Restart();
            Progress = new BinaryPatchProgress();

            if (InputStream.CanSeek) InputStream.Position = 0;
            if (OutputStream.CanSeek) OutputStream.Position = 0;

            // preallocate buffers for reading and writing
            Span<byte> newData = stackalloc byte[CBufferSize];
            Span<byte> oldData = stackalloc byte[CBufferSize];

            // decompress each part (to read it)
            using (Stream controlCompStream = PatchStream())
                using (Stream diffCompStream = PatchStream())
                    using (Stream extraCompStream = PatchStream())
                        using (Stream controlStream = TryGetCompressionStream(controlCompStream, CHeaderSize))
                            using (Stream diffStream = TryGetCompressionStream(diffCompStream, CHeaderSize + ControlLength))
                                using (Stream extraStream = TryGetCompressionStream(extraCompStream, CHeaderSize + ControlLength + DiffLength))
                                {
                                    // ReSharper disable once RedundantAssignment
                                    Span<long> control = stackalloc long[3];
                                    // ReSharper disable once UnusedVariable
                                    Span<byte> buffer  = stackalloc byte[8];
                                    
                                    long oldPosition = 0;
                                    long newPosition = 0;
                                    while (newPosition < NewSize)
                                    {
                                        // Get the control array
                                        control = ReadControlNumbers(controlStream, newPosition);

                                        // Get the size to copy
                                        long bytesToCopy = control[0];

                                        // Seek old file to the position that the new data is diffed against
                                        InputStream.Position = oldPosition;

                                        // Start the copy process
                                        while (bytesToCopy > 0)
                                        {
                                            // Throw if cancelation is called
                                            token.ThrowIfCancellationRequested();

                                            // Get minimum size to copy
                                            int actualBytesToCopy = (int)Math.Min(bytesToCopy, CBufferSize);
                                            // Get the minimum size from old data to copy
                                            int availableInputBytes = (int)Math.Min(actualBytesToCopy, InputStream.Length - InputStream.Position);

                                            // Read diff and old data
                                            diffStream.ReadExactly(newData[..actualBytesToCopy]);
                                            InputStream.ReadExactly(oldData[..availableInputBytes]);

                                            // Add the old with new data in vectors
                                            fixed (byte* newDataPtr = &newData[0])
                                                fixed (byte* oldDataPtr = &oldData[0])
                                                {
                                                    // Get the offset and remained offset
                                                    int  offset;
                                                    long offsetRemained = CBufferSize % Vector128<byte>.Count;
                                                    for (offset = 0; offset < CBufferSize - offsetRemained; offset += Vector128<byte>.Count)
                                                    {
                                                        Vector128<byte> newVector = Sse2.LoadVector128(newDataPtr + offset);
                                                        Vector128<byte> oldVector = Sse2.LoadVector128(oldDataPtr + offset);
                                                        Vector128<byte> resultVector = Sse2.Add(newVector, oldVector);

                                                        Sse2.Store(newDataPtr + offset, resultVector);
                                                    }

                                                    // Process the remained data by the last offset
                                                    while (offset < CBufferSize) *(newDataPtr + offset) += *(oldDataPtr + offset++);

                                                    // Write the data into the output
                                                    OutputStream.Write(newData[..actualBytesToCopy]);

                                                    // Adjust counters
                                                    newPosition += actualBytesToCopy;
                                                    oldPosition += actualBytesToCopy;
                                                    bytesToCopy -= actualBytesToCopy;

                                                    // Update progress
                                                    UpdateProgress(OutputStream.Length, NewSize, actualBytesToCopy);
                                                }
                                        }

                                        // SANITY CHECK: Check if the new position + Additional/new data has more size than _newSize.
                                        //               If yes, then throw.
                                        if (newPosition + control[1] > NewSize)
                                        {
                                            throw new InvalidDataException($"The patch file is corrupted! newPosition + control[1] ({newPosition} + {control[1]}) > newSize ({NewSize})");
                                        }

                                        // Get the bytes to copy for the additional data (new data)
                                        bytesToCopy = (int)control[1];
                                        while (bytesToCopy > 0)
                                        {
                                            // Throw if cancelation is called
                                            token.ThrowIfCancellationRequested();

                                            // Get the size of the additional data to copy
                                            int actualBytesToCopy = (int)Math.Min(bytesToCopy, CBufferSize);

                                            // Read the new data from extra stream and write it to output
                                            extraStream.ReadExactly(newData[..actualBytesToCopy]);
                                            OutputStream.Write(newData[..actualBytesToCopy]);

                                            newPosition += actualBytesToCopy;
                                            bytesToCopy -= actualBytesToCopy;

                                            // Update progress
                                            UpdateProgress(OutputStream.Length, NewSize, actualBytesToCopy);
                                        }

                                        // Adjust the position (either move it towards or behind the current position)
                                        oldPosition += control[2];
                                    }
                                }

            if (!LeaveOpenStream)
            {
                InputStream.Dispose();
                OutputStream.Dispose();
            }

            CanContinueApply = false;
        }

        private static unsafe long ReadInt64Sanity(ReadOnlySpan<byte> buf)
        {
            // Reference the buffer into byte pointer
            fixed (byte* ar = buf)
            {
                // Assign sanity number
                byte sanity = ar[7];

                // AND the last number with 0x7F (127)
                ar[7] &= 0x7F;

                // Points the byte pointer to cast as long
                long value = *(long*)ar;

                // If the sanity results non-zero number from AND 0x80 (128), then set it as negative value
                if ((sanity & 0x80) != 0)
                {
                    value *= -1;
                }

                // Return the value
                return value;
            }
        }

        private const long CFileSignature = 0x3034464649445342L;
        private const int  CHeaderSize    = 32;
    }

    public class BinaryPatchProgress
    {
        public void UpdatePatchEvent(long sizePatched, long sizeToBePatched, long read, double totalSecond)
        {
            Speed = sizePatched / totalSecond;
            SizePatched = sizePatched;
            SizeToBePatched = sizeToBePatched;
            Read = read;
        }

        public long     SizePatched        { get; private set; }
        public long     SizeToBePatched    { get; private set; }
        public double   ProgressPercentage => ConverterTool.ToPercentage(SizeToBePatched, SizePatched);
        public long     Read               { get; private set; }
        public double   Speed              { get; private set; }
        public TimeSpan TimeLeft           => ConverterTool.ToTimeSpanRemain(SizeToBePatched, SizePatched, Speed);
    }
}