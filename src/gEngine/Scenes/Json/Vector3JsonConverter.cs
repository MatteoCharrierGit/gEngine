using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace gEngine.Scenes.Json;

/// <summary>
/// Serializza/deserializza un <see cref="Vector3"/> come oggetto: <c>{"x":1,"y":2,"z":3}</c>.
/// I campi mancanti valgono 0. Le chiavi sono case-insensitive.
/// </summary>
public sealed class Vector3JsonConverter : JsonConverter<Vector3>
{
    public override Vector3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Atteso un oggetto per Vector3, trovato {reader.TokenType}.");

        float x = 0f, y = 0f, z = 0f;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return new Vector3(x, y, z);

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var name = reader.GetString()!;
            reader.Read();

            switch (name.ToLowerInvariant())
            {
                case "x": x = reader.GetSingle(); break;
                case "y": y = reader.GetSingle(); break;
                case "z": z = reader.GetSingle(); break;
            }
        }

        throw new JsonException("Oggetto Vector3 non terminato.");
    }

    public override void Write(Utf8JsonWriter writer, Vector3 value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteNumber("z", value.Z);
        writer.WriteEndObject();
    }
}
