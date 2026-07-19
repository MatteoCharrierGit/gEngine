using System.Text.Json;
using gEngine.Scenes;

namespace gEngine.Tests.Scenes;

/// <summary>
/// Confronto fra due <see cref="Scene"/>, che restituisce l'elenco delle differenze invece
/// di un bool.
///
/// Perché non basta <c>Assert.Equal</c> su due scene: <see cref="Scene"/> è fatta di
/// <see cref="JsonElement"/>, che confronta per <b>riferimento</b> — due elementi con lo
/// stesso contenuto risulterebbero diversi e il test fallirebbe sempre. E perché non basta
/// confrontare il JSON serializzato come testo: vedi <see cref="Canonicalize"/>.
///
/// Restituisce le differenze in chiaro perché un round-trip che fallisce deve dire
/// <i>quale campo</i> non è tornato indietro. "Le scene sono diverse" manderebbe a
/// bisezionare a mano un file di scena.
/// </summary>
internal static class SceneComparison
{
    public static List<string> Differences(Scene expected, Scene actual)
    {
        var differences = new List<string>();

        if (expected.Name != actual.Name)
            differences.Add($"nome della scena: atteso '{expected.Name}', trovato '{actual.Name}'");

        CompareBags("scena", expected.Extra, actual.Extra, differences);

        if (expected.Entities.Count != actual.Entities.Count)
        {
            differences.Add(
                $"numero di entità: attese {expected.Entities.Count}, trovate {actual.Entities.Count}");

            return differences; // oltre qui il confronto per indice non significherebbe niente
        }

        // Il confronto è per INDICE, non per nome, e non è pigrizia: l'ordine delle entità
        // nel file è ciò che decide il diff in git di una scena versionata. Un giro che
        // conserva i dati ma rimescola le righe è una regressione, e per nome non si vedrebbe.
        for (var i = 0; i < expected.Entities.Count; i++)
        {
            var expectedEntity = expected.Entities[i];
            var actualEntity = actual.Entities[i];
            var where = $"entità [{i}] '{expectedEntity.Name ?? "(senza nome)"}'";

            if (expectedEntity.Name != actualEntity.Name)
                differences.Add($"{where}: nome atteso '{expectedEntity.Name}', trovato '{actualEntity.Name}'");

            CompareBags(where, expectedEntity.Components, actualEntity.Components, differences);
            CompareBags(where, expectedEntity.Extra, actualEntity.Extra, differences);
        }

        return differences;
    }

    private static void CompareBags(
        string where,
        IReadOnlyDictionary<string, JsonElement> expected,
        IReadOnlyDictionary<string, JsonElement> actual,
        List<string> differences)
    {
        foreach (var (key, expectedValue) in expected)
        {
            if (!actual.TryGetValue(key, out var actualValue))
            {
                differences.Add($"{where}: '{key}' è sparito nel giro");
                continue;
            }

            var expectedText = Canonicalize(expectedValue);
            var actualText = Canonicalize(actualValue);

            if (expectedText != actualText)
                differences.Add($"{where}: '{key}' atteso {expectedText}, trovato {actualText}");
        }

        foreach (var key in actual.Keys)
        {
            if (!expected.ContainsKey(key))
                differences.Add($"{where}: '{key}' è comparso dal nulla");
        }
    }

    /// <summary>
    /// Il JSON in forma canonica: chiavi degli oggetti ordinate, ricorsivamente.
    ///
    /// ⚠️ Serve perché l'ordine delle chiavi <b>cambia legittimamente</b> nel giro, e
    /// confrontare il testo grezzo darebbe falsi allarmi. Il motivo è preciso:
    /// <c>SceneSerializer</c> scrive i componenti scorrendo <c>World.ComponentStorages</c>,
    /// cioè nell'ordine in cui gli storage sono <b>nati</b>. Nel World costruito a mano
    /// quell'ordine è quello delle chiamate ad <c>AddComponent</c>; nel World reistanziato è
    /// quello delle chiavi nel file. Non coincidono, e non devono: in JSON l'ordine delle
    /// chiavi di un oggetto non è informazione.
    ///
    /// (Effetto collaterale reale, non un difetto del test: il primo salvataggio di una
    /// scena scritta a mano può riordinare i componenti dentro un'entità. Cambia il diff,
    /// non il contenuto — è la stessa nota già scritta per i <c>_comment</c>.)
    /// </summary>
    private static string Canonicalize(JsonElement element)
    {
        var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
            WriteCanonical(element, writer);

        return System.Text.Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void WriteCanonical(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();

                foreach (var property in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(property.Value, writer);
                }

                writer.WriteEndObject();
                break;

            // Gli array NON si riordinano: lì l'ordine è contenuto.
            case JsonValueKind.Array:
                writer.WriteStartArray();

                foreach (var item in element.EnumerateArray())
                    WriteCanonical(item, writer);

                writer.WriteEndArray();
                break;

            default:
                element.WriteTo(writer);
                break;
        }
    }
}
