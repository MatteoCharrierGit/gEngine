using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace gEngine.Scenes.Json;

/// <summary>
/// Serializza/deserializza un <see cref="Quaternion"/> come oggetto:
/// <c>{"x":0,"y":0,"z":0,"w":1}</c>. Se assenti, x/y/z valgono 0 e w vale 1,
/// così un oggetto vuoto o parziale produce comunque una rotazione valida
/// (identità). Le chiavi sono case-insensitive.
/// </summary>
public sealed class QuaternionJsonConverter : JsonConverter<Quaternion>
{
    public override Quaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException($"Atteso un oggetto per Quaternion, trovato {reader.TokenType}.");

        float x = 0f, y = 0f, z = 0f, w = 1f;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
                return new Quaternion(x, y, z, w);

            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var name = reader.GetString()!;
            reader.Read();

            switch (name.ToLowerInvariant())
            {
                case "x": x = reader.GetSingle(); break;
                case "y": y = reader.GetSingle(); break;
                case "z": z = reader.GetSingle(); break;
                case "w": w = reader.GetSingle(); break;
            }
        }

        throw new JsonException("Oggetto Quaternion non terminato.");
    }

    public override void Write(Utf8JsonWriter writer, Quaternion value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", value.X);
        writer.WriteNumber("y", value.Y);
        writer.WriteNumber("z", value.Z);
        writer.WriteNumber("w", value.W);
        writer.WriteEndObject();
    }
}
