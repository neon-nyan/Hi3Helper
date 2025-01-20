using CollapseLauncher.Helper.Metadata;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable SwitchStatementMissingSomeEnumCasesNoDefault

namespace CollapseLauncher.ShortcutUtils
{
    public sealed class SteamShortcutParser
    {
        private readonly string _path;
        private List<VdfObject> _shortcuts = [];

        public SteamShortcutParser(string path)
        {
            _path = path;
            Load();
        }

        private int FindIndex(SteamShortcut shortcut)
        {
            return _shortcuts.FindIndex(x =>
            {
                if (x.Value.ObjectValue.TryGetValue("appid", out var appid))
                    return appid.Value.IntValue == shortcut.AppID;
                return false;
            });
        }

        internal SteamShortcut Insert(PresetConfig preset, bool play = false)
        {
            var shortcut = new SteamShortcut(_path, preset, play);
            var index = FindIndex(shortcut);
            if (index == -1)
            {
                Dictionary<string, VdfObject> shortcutDic = new()
                {
                    ["appid"] = shortcut.AppID,
                    ["AppName"] = shortcut.AppName,
                    ["Exe"] = shortcut.Exe,
                    ["StartDir"] = shortcut.StartDir,
                    ["icon"] = shortcut.Icon,
                    ["ShortcutPath"] = "",
                    ["LaunchOptions"] = shortcut.LaunchOptions,
                    ["IsHidden"] = 0,
                    ["AllowDesktopConfig"] = 1,
                    ["AllowOverlay"] = 1,
                    ["OpenVR"] = 0,
                    ["Devkit"] = 0,
                    ["DevkitGameID"] = "",
                    ["DevkitOverrideAppID"] = 0,
                    ["LastPlayTime"] = 0,
                    ["FlatpakAppID"] = "",
                    ["tags"] = new Dictionary<string, VdfObject>()
                };

                var obj = new VdfObject
                {
                    Type = VdfType.Object,
                    Value = new VdfValue
                    {
                        ObjectValue = shortcutDic
                    }
                };
                _shortcuts.Add(obj);
            }
            else
            {
                Dictionary<string, VdfObject> shortcutDic = _shortcuts[index].Value.ObjectValue;
                shortcutDic["appid"] = shortcut.AppID;
                shortcutDic["AppName"] = shortcut.AppName;
                shortcutDic["Exe"] = shortcut.Exe;
                shortcutDic["StartDir"] = shortcut.StartDir;
                shortcutDic["icon"] = shortcut.Icon;
                shortcutDic["LaunchOptions"] = shortcut.LaunchOptions;
            }

            return shortcut;
        }

        private void Load()
        {
            _shortcuts.Clear();

            if (!File.Exists(_path))
                return;

            using var                     fs             = File.OpenRead(_path);
            Dictionary<string, VdfObject> shortcutObject = ReadObject(fs);
            if (shortcutObject.TryGetValue("shortcuts", out var shortcuts))
            {
                _shortcuts = shortcuts.Value.ObjectValue.Values.ToList();
            }
        }

        public void Save()
        {
            if (File.Exists(_path))
                File.Move(_path, _path + "_old", true);

            using var fs = File.OpenWrite(_path);
            Dictionary<string, VdfObject> shortcutObject = new()
            {
                {
                    "shortcuts", new VdfObject
                    {
                        Type = VdfType.Object,
                        Value = new VdfValue
                        {
                            ObjectValue = _shortcuts.Index().ToDictionary(x => x.Index.ToString(), x => x.Item)
                        }
                    }
                }
            };
            WriteObject(fs, shortcutObject);
        }

        #region Serializer and Deserializer
        private enum VdfType
        {
            Object,
            String,
            Int,
            ObjectEnd = 8
        }

        private class VdfValue
        {
            public string StringValue;
            public uint IntValue;
            public Dictionary<string, VdfObject> ObjectValue;
        }

        private class VdfObject
        {
            public VdfType Type;
            public VdfValue Value;

            public static implicit operator VdfObject(string value)
            {
                return new VdfObject
                {
                    Type = VdfType.String,
                    Value = new VdfValue
                    {
                        StringValue = value
                    }
                };
            }

            public static implicit operator VdfObject(uint value)
            {
                return new VdfObject
                {
                    Type = VdfType.Int,
                    Value = new VdfValue
                    {
                        IntValue = value
                    }
                };
            }

            public static implicit operator VdfObject(Dictionary<string, VdfObject> value)
            {
                return new VdfObject
                {
                    Type = VdfType.Object,
                    Value = new VdfValue
                    {
                        ObjectValue = value
                    }
                };
            }
        }

        private static VdfType ReadType(FileStream reader)
        {
            return (VdfType)reader.ReadByte();
        }

        private static string ReadString(FileStream reader)
        {
            List<byte> buffer = [];
            while (reader.Position < reader.Length)
            {
                var read = (byte)reader.ReadByte();
                if (read == 0) break;
                buffer.Add(read);
            }
            return Encoding.UTF8.GetString(buffer.ToArray());
        }

        private static uint ReadInt(FileStream reader)
        {
            var buffer = new byte[4];
            reader.ReadExactly(buffer);
            return BitConverter.ToUInt32(buffer);
        }

        private static Dictionary<string, VdfObject> ReadObject(FileStream reader)
        {
            Dictionary<string, VdfObject> result = new();

            while (reader.Position < reader.Length)
            {
                var type = ReadType(reader);
                if (type == VdfType.ObjectEnd)
                    goto finish;

                var name = ReadString(reader);
                var subObject = new VdfObject
                {
                    Type = type,
                    Value = new VdfValue()
                };

                switch (type)
                {
                    case VdfType.Object:
                    {
                        subObject.Value.ObjectValue = ReadObject(reader);
                        break;
                    }
                    case VdfType.String:
                    {
                        subObject.Value.StringValue = ReadString(reader);
                        break;
                    }
                    case VdfType.Int:
                    {
                        subObject.Value.IntValue = ReadInt(reader);
                        break;
                    }
                    default:
                    {
                        throw new InvalidDataException($"Unknown type {type:X}. Is it supposed to be in shortcuts.vdf?");
                    }
                }

                result.Add(name, subObject);
            }
            finish:
            return result;
        }

        private static void WriteType(FileStream writer, VdfType value)
        {
            writer.WriteByte((byte)value);
        }

        private static void WriteString(FileStream writer, string value)
        {
            writer.Write(Encoding.UTF8.GetBytes(value));
            writer.WriteByte(0);
        }

        private static void WriteInt(FileStream writer, uint value)
        {
            writer.Write(BitConverter.GetBytes(value));
        }

        private static void WriteObject(FileStream writer, Dictionary<string, VdfObject> value)
        {
            foreach (var (name, subObject) in value)
            {
                var type = subObject.Type;
                WriteType(writer, type);
                WriteString(writer, name);
                switch (type)
                {
                    case VdfType.Object:
                    {
                        WriteObject(writer, subObject.Value.ObjectValue);
                        break;
                    }
                    case VdfType.String:
                    {
                        WriteString(writer, subObject.Value.StringValue);
                        break;
                    }
                    case VdfType.Int:
                    {
                        WriteInt(writer, subObject.Value.IntValue);
                        break;
                    }
                }
            }
            WriteType(writer, VdfType.ObjectEnd);
        }
        #endregion
    }
}
