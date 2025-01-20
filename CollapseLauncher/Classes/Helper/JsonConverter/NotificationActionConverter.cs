using Hi3Helper.Shared.ClassStruct;
using System;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace CollapseLauncher.Helper.JsonConverter
{
    public class NotificationActionConverter : JsonConverter<NotificationActionBase>
    {
        public override bool CanConvert(Type type)
        {
            return typeof(NotificationActionBase).IsAssignableFrom(type);
        }

        public override NotificationActionBase Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            // Check if the token is a start object
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException();
            }

            NotificationActionBase returnValue = new NotificationActionBase();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return returnValue;
                }

                // Check and get the property name
                string section = reader.GetString();
                reader.Read();

                switch (section)
                {
                    case "ActionType":
                        {
                            section = reader.GetString();
                            // For performance, parse with ignoreCase:false first.
                            if (!Enum.TryParse(section, ignoreCase: false, out NotificationActionTypeDesc actionType) &&
                                !Enum.TryParse(section, ignoreCase: true, out actionType))
                            {
                                // Skip if the action is not supported
                                reader.Skip();
                                reader.Skip();
                                break;
                            }
                            returnValue.ActionType = actionType;
                        }
                        break;
                    case "ActionValue":
                        {
                            switch (returnValue.ActionType)
                            {
                                case NotificationActionTypeDesc.ClickLink:
                                    // Build the UI element
                                    returnValue = new NotificationActionClickLink();
                                    returnValue.BuildFrameworkElement(ref reader);
                                    break;
                                default:
                                    reader.Skip();
                                    break;
                            }
                        }
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            return returnValue;
        }

        public override void Write(
                Utf8JsonWriter writer,
                NotificationActionBase baseType,
                JsonSerializerOptions options)
        {
            throw new JsonException("Serializing is not supported!");
        }
    }
}
