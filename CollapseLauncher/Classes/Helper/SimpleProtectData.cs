using Hi3Helper;
using System;
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Hi3Helper.SentryHelper;

#nullable enable
namespace CollapseLauncher.Helper
{
    internal static class SimpleProtectData
    {
        internal static unsafe string? ProtectString(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            byte[] entropyRng = new byte[20];
            byte[] buffer = new byte[input.Length + 16];
            byte[]? dataWithEntropy = null;

            using RandomNumberGenerator rng = RandomNumberGenerator.Create();
            rng.GetBytes(entropyRng);

            fixed (char* charB = input)
            {
                try
                {
                    int len = Encoding.UTF8.GetBytes(input, buffer);
                    byte[] bufferR = new byte[len];
                    Array.Copy(buffer, 0, bufferR, 0, len);

                    byte[] data = ProtectedData.Protect(bufferR, entropyRng, DataProtectionScope.CurrentUser);
                    dataWithEntropy = new byte[data.Length + entropyRng.Length];
                    Array.Copy(data, 0, dataWithEntropy, 0, data.Length);
                    Array.Copy(entropyRng, 0, dataWithEntropy, dataWithEntropy.Length - entropyRng.Length, entropyRng.Length);
                    return Convert.ToBase64String(dataWithEntropy);
                }
                catch (Exception ex)
                {
                    SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                    Logger.LogWriteLine($"Failed while protecting string! {ex}", LogType.Error);
                    return null;
                }
                finally
                {
                    Array.Clear(entropyRng);
                    Array.Clear(buffer);
                    if (dataWithEntropy != null)
                        Array.Clear(dataWithEntropy);

                    int len = input.Length;
                    for (int i = 0; i < len; i++)
                        *(charB + i) = '\0';
                }
            }
        }

        internal static string? UnprotectString(string? input)
        {
            if (string.IsNullOrEmpty(input))
                return null;

            if (!Base64.IsValid(input))
                return null;

            int possibleDecLen = Base64.GetMaxDecodedFromUtf8Length(input.Length);
            byte[] rawEncDataBase64Dec = new byte[possibleDecLen];
            byte[] strByte = new byte[input.Length + 16];

            byte[]? entropy = null, bufferDataRaw = null;
            byte[]? dataDec = null, unprotectedData = null;

            try
            {
                int strByteLen = Encoding.UTF8.GetBytes(input, strByte);
                Base64.DecodeFromUtf8(strByte.AsSpan(0, strByteLen), rawEncDataBase64Dec, out _, out int rawEncDataLen);

                entropy = new byte[20];
                bufferDataRaw = new byte[rawEncDataLen - 20];
                Array.Copy(rawEncDataBase64Dec, 0, bufferDataRaw, 0, bufferDataRaw.Length);
                Array.Copy(rawEncDataBase64Dec, bufferDataRaw.Length, entropy, 0, entropy.Length);

                unprotectedData = ProtectedData.Unprotect(bufferDataRaw, entropy, DataProtectionScope.CurrentUser);
                string unprotectedDataStr = Encoding.UTF8.GetString(unprotectedData);
                return unprotectedDataStr;
            }
            catch (Exception ex)
            {
                SentryHelper.ExceptionHandler(ex, SentryHelper.ExceptionType.UnhandledOther);
                Logger.LogWriteLine($"Failed while trying to unprotect string! {ex}", LogType.Error);
                return null;
            }
            finally
            {
                if (entropy != null)
                    Array.Clear(entropy);

                if (bufferDataRaw != null)
                    Array.Clear(bufferDataRaw);

                if (dataDec != null)
                    Array.Clear(dataDec);

                if (unprotectedData != null)
                    Array.Clear(unprotectedData);
            }
        }
    }
}