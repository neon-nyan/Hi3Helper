using System;
using System.Buffers;
using System.IO.Hashing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

#nullable enable
namespace CollapseLauncher.Helper
{
    public static partial class Hash
    {
        /// <summary>
        /// Asynchronously computes the cryptographic hash of a file specified by its path.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="filePath">The path of the file to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            string            filePath,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            using FileStream fileStream = File.OpenRead(filePath);
            return GetCryptoHashAsync<T>(fileStream, hmacKey, readProgress, token);
        }

        /// <summary>
        /// Asynchronously computes the cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            FileInfo          fileInfo,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            using FileStream fileStream = fileInfo.OpenRead();
            return GetCryptoHashAsync<T>(fileStream, hmacKey, readProgress, token);
        }

        /// <summary>
        /// Asynchronously computes the cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetCryptoHashAsync<T>(
            Stream            stream,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            // Create a new task from factory, assign a synchronous method to it with detached thread.
            Task<byte[]> task = Task<byte[]>
                               .Factory
                               .StartNew(() => GetCryptoHash<T>(stream, hmacKey, readProgress, token),
                                         token,
                                         TaskCreationOptions.DenyChildAttach,
                                         TaskScheduler.Default);

            // Create awaitable returnable-task
            return task.ConfigureAwait(false);
        }

        /// <summary>
        /// Synchronously computes the cryptographic hash of a file specified by its path.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="filePath">The path of the file to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] GetCryptoHash<T>(
            string            filePath,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            using FileStream fileStream = File.OpenRead(filePath);
            return GetCryptoHash<T>(fileStream, hmacKey, readProgress, token);
        }

        /// <summary>
        /// Synchronously computes the cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] GetCryptoHash<T>(
            FileInfo          fileInfo,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            using FileStream fileStream = fileInfo.OpenRead();
            return GetCryptoHash<T>(fileStream, hmacKey, readProgress, token);
        }

        /// <summary>
        /// Synchronously computes the cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the hash algorithm to use. Must inherit from <see cref="HashAlgorithm"/>.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <param name="hmacKey">The key to use for HMAC-based hash algorithms. If null, a standard hash algorithm is used.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] GetCryptoHash<T>(
            Stream            stream,
            byte[]?           hmacKey      = null,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : HashAlgorithm
        {
            // Create and use hasher. If hmacKey is not null and not empty, then create HMAC hasher
            using HashAlgorithm hashCryptoTransform = hmacKey != null && hmacKey.Length != 0 ?
                CreateHmacCryptoHash<T>(hmacKey) :
                CreateCryptoHash<T>();

            // Get length based on stream length or at least if bigger, use the default one
            int bufferLen = BufferLength > stream.Length ? (int)stream.Length : BufferLength;

            // Initialize buffer
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLen);
            bufferLen = buffer.Length;

            try
            {
                // Do read activity
                int read;
                while ((read = stream.Read(buffer, 0, bufferLen)) > 0)
                {
                    // Throw Cancellation exception if detected
                    token.ThrowIfCancellationRequested();

                    // Append buffer into hash block
                    hashCryptoTransform.TransformBlock(buffer, 0, read, buffer, 0);

                    // Update the read progress if the action is not null
                    readProgress?.Invoke(read);
                }

                // Finalize the hash calculation
                hashCryptoTransform.TransformFinalBlock(buffer, 0, read);

                // Return computed hash byte
                return hashCryptoTransform.Hash ?? [];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a file specified by its path.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="filePath">The path of the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            string            filePath,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            using FileStream fileStream = File.OpenRead(filePath);
            return GetHashAsync<T>(fileStream, readProgress, token);
        }

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            FileInfo          fileInfo,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            using FileStream fileStream = fileInfo.OpenRead();
            return GetHashAsync<T>(fileStream, readProgress, token);
        }

        /// <summary>
        /// Asynchronously computes the non-cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the task to complete.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the computed hash as a byte array.</returns>
        public static ConfiguredTaskAwaitable<byte[]> GetHashAsync<T>(
            Stream            stream,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            // Create a new task from factory, assign a synchronous method to it with detached thread.
            Task<byte[]> task = Task<byte[]>
                               .Factory
                               .StartNew(() => GetHash<T>(stream, readProgress, token),
                                         token,
                                         TaskCreationOptions.DenyChildAttach,
                                         TaskScheduler.Default);

            // Create awaitable returnable-task
            return task.ConfigureAwait(false);
        }

        /// <summary>
        /// Synchronously computes the non-cryptographic hash of a file specified by its path.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="filePath">The path of the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] GetHash<T>(
            string            filePath,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            using FileStream fileStream = File.OpenRead(filePath);
            return GetHash<T>(fileStream, readProgress, token);
        }

        /// <summary>
        /// Synchronously computes the non-cryptographic hash of a file specified by a <see cref="FileInfo"/> object.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="fileInfo">The <see cref="FileInfo"/> object representing the file to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] GetHash<T>(
            FileInfo          fileInfo,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            using FileStream fileStream = fileInfo.OpenRead();
            return GetHash<T>(fileStream, readProgress, token);
        }

        /// <summary>
        /// Synchronously computes the non-cryptographic hash of a stream.
        /// </summary>
        /// <typeparam name="T">The type of the non-cryptographic hash algorithm to use. Must inherit from <see cref="NonCryptographicHashAlgorithm"/> and have a parameterless constructor.</typeparam>
        /// <param name="stream">The stream to compute the hash for.</param>
        /// <param name="readProgress">An action to report the read progress.</param>
        /// <param name="token">A cancellation token to observe while waiting for the operation to complete.</param>
        /// <returns>The computed hash as a byte array.</returns>
        public static byte[] GetHash<T>(
            Stream            stream,
            Action<int>?      readProgress = null,
            CancellationToken token        = default)
            where T : NonCryptographicHashAlgorithm, new()
        {
            // Create hasher instance
            NonCryptographicHashAlgorithm hashProvider = CreateHash<T>();

            // Get length based on stream length or at least if bigger, use the default one
            int bufferLen = BufferLength > stream.Length ? (int)stream.Length : BufferLength;

            // Initialize buffer
            byte[] buffer = ArrayPool<byte>.Shared.Rent(bufferLen);
            bufferLen = buffer.Length;

            try
            {
                // Do read activity
                int read;
                while ((read = stream.Read(buffer, 0, bufferLen)) > 0)
                {
                    // Throw Cancellation exception if detected
                    token.ThrowIfCancellationRequested();

                    // Append buffer into hash block
                    hashProvider.Append(buffer.AsSpan(0, read));

                    // Update the read progress if the action is not null
                    readProgress?.Invoke(read);
                }

                // Return computed hash byte
                return hashProvider.GetHashAndReset();
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}
