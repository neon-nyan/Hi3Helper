using CollapseLauncher.Extension;
using CollapseLauncher.Helper.JsonConverter;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
// ReSharper disable IdentifierTypo

namespace Hi3Helper.Shared.ClassStruct
{
    public class NotificationProp
    {
        public int MsgId { get; set; }
        public bool? IsClosable { get; set; }
        public bool? IsDisposable { get; set; }
        public bool? Show { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public NotifSeverity Severity { get; set; }
        public string RegionProfile { get; set; }
        public string ValidForVerAbove { get; set; }
        public string ValidForVerBelow { get; set; }
#nullable enable
        public object? OtherUIElement { get; set; }
        public DateTime? TimeBegin { get; set; }
        public DateTime? TimeEnd { get; set; }

        [JsonConverter(typeof(NotificationActionConverter))]
        public NotificationActionBase? ActionProperty { get; set; }
        public bool IsForceShowNotificationPanel { get; set; }
#nullable disable
    }

    [JsonConverter(typeof(JsonStringEnumConverter<NotificationActionTypeDesc>))]
    public enum NotificationActionTypeDesc
    {
        ClickLink = 0
    }

    public class NotificationActionBase
    {
        public NotificationActionTypeDesc? ActionType { get; set; }
        public Type BaseClassType { get; set; }

        public virtual FrameworkElement GetFrameworkElement()
        {
            return null;
        }

        public virtual void BuildFrameworkElement(ref Utf8JsonReader reader) { }
    }

    public class NotificationActionClickLink : NotificationActionBase
    {
        public string URL { get; set; }
        public string Description { get; set; }
#nullable enable
        public string? GlyphIcon { get; set; }
        public string? GlyphFont { get; set; }
#nullable disable

        public NotificationActionClickLink()
        {
            // Assign the class type
            BaseClassType = typeof(NotificationActionClickLink);
        }

        public override FrameworkElement GetFrameworkElement() => NotificationPush.GenerateNotificationButton(GlyphIcon, Description, (_, _) =>
        {
            new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = URL
                }
            }.Start();
        }, GlyphFont);

        public override void BuildFrameworkElement(ref Utf8JsonReader reader)
        {
            // Do loop while the reader == true
            while (reader.Read())
            {
                // If it's already at the end, then return
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return;
                }

                // Start reading the property
                string section = reader.GetString();
                switch (section)
                {
                    case "URL":
                        URL = ReadString(ref reader);
                        break;
                    case "Description":
                        Description = ReadString(ref reader);
                        break;
                    case "GlyphIcon":
                        GlyphIcon = ReadString(ref reader);
                        break;
                    case "GlyphFont":
                        GlyphFont = ReadString(ref reader);
                        break;
                }
            }
        }

        private static string ReadString(ref Utf8JsonReader reader)
        {
            reader.Read();
            return reader.TokenType switch
            {
                JsonTokenType.String => reader.GetString(),
                JsonTokenType.Null => null,
                _ => throw new JsonException("Value must be a string type or null!")
            };
        }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<NotifSeverity>))]
    public enum NotifSeverity : uint
    {
        Informational,
        Success,
        Warning,
        Error
    }

    [JsonSourceGenerationOptions(IncludeFields = false, GenerationMode = JsonSourceGenerationMode.Metadata, IgnoreReadOnlyFields = true)]
    [JsonSerializable(typeof(NotificationPush))]
    internal sealed partial class NotificationPushJsonContext : JsonSerializerContext;

    public sealed class NotificationPush
    {
        public List<NotificationProp> AppPush                { get; set; } = [];
        public List<NotificationProp> RegionPush             { get; set; } = [];
        public List<int>              AppPushIgnoreMsgIds    { get; set; } = [];
        public List<int>              RegionPushIgnoreMsgIds { get; set; } = [];
        public List<int>              CurrentShowMsgIds      { get; set; } = [];

        public void AddIgnoredMsgIds(int msgId, bool isAppPush = true)
        {
            if (isAppPush ? AppPushIgnoreMsgIds.Contains(msgId) : RegionPushIgnoreMsgIds.Contains(msgId))
            {
                return;
            }

            if (isAppPush)
            {
                AppPushIgnoreMsgIds.Add(msgId);
            }
            else
            {
                RegionPushIgnoreMsgIds.Add(msgId);
            }
        }

        public void RemoveIgnoredMsgIds(int msgId, bool isAppPush = true)
        {
            if (isAppPush ? !AppPushIgnoreMsgIds.Contains(msgId) : !RegionPushIgnoreMsgIds.Contains(msgId))
            {
                return;
            }

            if (isAppPush)
            {
                AppPushIgnoreMsgIds.Remove(msgId);
            }
            else
            {
                RegionPushIgnoreMsgIds.Remove(msgId);
            }
        }

        public bool IsMsgIdIgnored(int msgId) => AppPushIgnoreMsgIds.Contains(msgId) || RegionPushIgnoreMsgIds.Contains(msgId);

        public void EliminatePushList()
        {
            AppPush?.RemoveAll(x => AppPushIgnoreMsgIds.Any(y => x.MsgId == y));
            RegionPush?.RemoveAll(x => RegionPushIgnoreMsgIds.Any(y => x.MsgId == y));
        }

        public static Button GenerateNotificationButton(string iconGlyph, string text, RoutedEventHandler buttonAction = null, string fontIconName = "FontAwesomeSolid", string buttonStyle = "AccentButtonStyle")
        {
            Button btn =
                UIElementExtensions.CreateButtonWithIcon<Button>(
                    text,
                    iconGlyph,
                    fontIconName,
                    buttonStyle
                )
                .WithMargin(0d, 0d, 0d, 8d);

            if (buttonAction != null) btn.Click += buttonAction;
            return btn;
        }
    }
}
