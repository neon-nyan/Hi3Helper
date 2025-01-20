#nullable enable
    using System;

    namespace CollapseLauncher.Helper.Metadata
{
    public class GameDataVersion
    {
        public byte[]? Data
        {
            get;
            init
            {
                if (DataCooker.IsServeV3Data(value))
                {
                    DataCooker.GetServeV3DataSize(value, out long compressedSize, out long decompressedSize);
                    byte[] dataOut = new byte[decompressedSize];
                    DataCooker.ServeV3Data(value, dataOut, (int)compressedSize, (int)decompressedSize, out _);
                    field = dataOut;
                }

                field = value;
            }
        }

        public long Length { get; set; }
        public bool IsCompressed { get; set; }

        public static int GetBytesToIntVersion(byte[] version)
        {
            if (version.Length != 4) throw new FormatException("Argument: version must have 4 bytes");
            return version[0] | version[1] << 8 | version[2] << 16 | version[3] << 24;
        }
    }
}
