using System.Text.Json;
using System.Text.Json.Serialization;
using gEngine.Rendering;

namespace gEngine.Scenes.Json;

/// <summary>
/// Serializza/deserializza un <see cref="Color"/> come oggetto:
/// <c>{"r":255,"g":128,"b":0,"a":255}</c>. Serve un converter dedicato perché
/// <see cref="Color"/> ha campi readonly e va costruito via costruttore.
/// Se assente, l'alpha vale 255 (opaco). Le chiavi sono case-insensitive.
/// </summary>
public sealed class ColorJsonConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Atteso un oggetto per Color, trovato {reader.TokenType}.");

        byte r = 0, g = 0, b = 0, a = 255;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return new Color(r, g, b, a);

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var name = reader.GetString()!;
            reader.Read();

            switch (name.ToLowerInvariant())
            {
                case "r": r = reader.GetByte(); break;
                case "g": g = reader.GetByte(); break;
                case "b": b = reader.GetByte(); break;
                case "a": a = reader.GetByte(); break;
            }
        }

        throw new JsonException("Oggetto Color non terminato.");
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("r", value.R);
        writer.WriteNumber("g", value.G);
        writer.WriteNumber("b", value.B);
        writer.WriteNumber("a", value.A);
        writer.WriteEndObject();
    }
}
