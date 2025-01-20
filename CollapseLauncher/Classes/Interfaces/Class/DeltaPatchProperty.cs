using System;
using System.IO;
// ReSharper disable InconsistentNaming

namespace CollapseLauncher
{
    internal class DeltaPatchProperty
    {
        internal DeltaPatchProperty(string patchFile)
        {
            ReadOnlySpan<string> strings = Path.GetFileNameWithoutExtension(patchFile).Split('_');
            MD5hash = strings[5];
            ZipHash = strings[4];
            ProfileName = strings[0];
            SourceVer = strings[1];
            TargetVer = strings[2];
            PatchCompr = strings[3];
            PatchPath = patchFile;
        }

        public string ZipHash { get; set; }
        public string MD5hash { get; set; }
        public string ProfileName { get; set; }
        public string SourceVer { get; set; }
        public string TargetVer { get; set; }
        public string PatchCompr { get; set; }
        public string PatchPath { get; set; }
    }
}
