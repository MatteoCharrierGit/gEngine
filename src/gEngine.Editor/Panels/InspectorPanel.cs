using System.Numerics;
using System.Reflection;
using gEngine.Ecs.Base;
using gEngine.Ecs.Interfaces;
using ImGuiNET;
using Color = gEngine.Rendering.Color;

namespace gEngine.Editor.Panels;

/// <summary>
/// Mostra e modifica i componenti dell'entità selezionata nell'<see cref="EditorContext"/>.
///
/// È <b>reflection-driven</b> per necessità, non per gusto: l'Inspector deve saper mostrare
/// anche un componente definito fuori dall'engine (es. <c>PlayerComponent</c> di Sandbox),
/// e un pannello con i campi scritti a mano andrebbe modificato dentro l'engine ogni volta
/// che un gioco inventa un componente. È lo stesso vincolo che ha dato la forma al registry
/// delle scene: l'engine non può conoscere l'elenco dei tipi di componente.
///
/// La differenza è che il registry delle scene è esplicito (registri il binder) mentre qui
/// la reflection è automatica: un componente nuovo compare nell'Inspector senza registrare
/// nulla. Il prezzo è che i tipi di campo supportati sono un elenco chiuso — quelli fuori
/// elenco si vedono in sola lettura invece di sparire.
/// </summary>
public class InspectorPanel : IEditorPanel
{
    public void Draw(World world, EditorContext context)
    {
        // Primo avvio: a destra, di fronte alla Hierarchy. Poi comanda imgui.ini.
        var viewport = ImGui.GetMainViewport().Size;
        ImGui.SetNextWindowPos(new Vector2(viewport.X - 360, 40), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(340, 600), ImGuiCond.FirstUseEver);

        if (!ImGui.Begin("Inspector"))
        {
            ImGui.End();
            return;
        }

        if (context.Selected is not { } entity || !world.Exists(entity))
        {
            ImGui.TextDisabled("Nessuna entità selezionata.");
            ImGui.End();
            return;
        }

        ImGui.Text($"Entity {entity.Id}");
        ImGui.Separator();

        // La rimozione è rimandata a dopo il ciclo: togliere un componente mentre si
        // scorrono gli storage è innocuo (gli storage sono per tipo, non per entità),
        // ma applicarla fuori dal ciclo lo rende evidente invece che da dimostrare.
        IComponentStorage? removeFrom = null;

        foreach (var storage in world.ComponentStorages)
        {
            if (!storage.Has(entity.Id))
                continue;

            if (DrawComponent(entity, storage))
                removeFrom = storage;
        }

        removeFrom?.Remove(entity.Id);

        ImGui.End();
    }

    /// <returns>true se l'utente ha chiesto di rimuovere il componente.</returns>
    private static bool DrawComponent(Entity entity, IComponentStorage storage)
    {
        var type = storage.ComponentType;

        var boxed = storage.GetBoxed(entity.Id);
        if (boxed is null)
            return false;

        var removeRequested = false;

        ImGui.PushID(type.FullName ?? type.Name);

        var open = ImGui.CollapsingHeader(DisplayName(type), ImGuiTreeNodeFlags.DefaultOpen);

        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 8f);
        if (ImGui.SmallButton("X"))
            removeRequested = true;

        if (open)
        {
            var changed = false;

            // |= e non ||=: tutti i campi vanno disegnati comunque, non solo fino al primo
            // che cambia.
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                changed |= DrawField(field, boxed);

            // Il write-back. Se il componente è uno struct, `boxed` è una COPIA: le
            // SetValue di sopra hanno mutato la scatola, non lo storage. Senza questa
            // riga l'Inspector sembrerebbe funzionare e non salverebbe niente — è lo
            // stesso gotcha struct/copia dei system. Se invece è una class (es.
            // MeshRendererComponent) la mutazione è già in loco e questa è una riscrittura
            // dello stesso riferimento: innocua, e ci evita di distinguere i due casi.
            if (changed)
                storage.SetBoxed(entity.Id, boxed);
        }

        ImGui.PopID();
        return removeRequested;
    }

    /// <returns>true se il campo è stato modificato.</returns>
    private static bool DrawField(FieldInfo field, object target)
    {
        var type = field.FieldType;
        var label = field.Name;
        var value = field.GetValue(target);

        if (type == typeof(float))
        {
            var v = (float)value!;
            if (!ImGui.DragFloat(label, ref v, 0.05f)) return false;
            field.SetValue(target, v);
            return true;
        }

        if (type == typeof(int))
        {
            var v = (int)value!;
            if (!ImGui.DragInt(label, ref v)) return false;
            field.SetValue(target, v);
            return true;
        }

        if (type == typeof(bool))
        {
            var v = (bool)value!;
            if (!ImGui.Checkbox(label, ref v)) return false;
            field.SetValue(target, v);
            return true;
        }

        if (type == typeof(Vector3))
        {
            var v = (Vector3)value!;
            if (!ImGui.DragFloat3(label, ref v, 0.05f)) return false;
            field.SetValue(target, v);
            return true;
        }

        if (type == typeof(Quaternion))
        {
            // Mostrato in gradi: vedi EulerAngles per convenzione e limiti.
            var euler = EulerAngles.ToDegrees((Quaternion)value!);
            if (!ImGui.DragFloat3($"{label} (°)", ref euler, 1f)) return false;
            field.SetValue(target, EulerAngles.ToQuaternion(euler));
            return true;
        }

        if (type == typeof(Color))
        {
            var c = (Color)value!;
            var v = new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            if (!ImGui.ColorEdit4(label, ref v)) return false;

            // I campi di Color sono readonly: non si mutano uno a uno. Si ricostruisce il
            // valore e si riassegna il campo INTERO del componente — che è comunque la
            // regola generale qui, per struct annidati.
            field.SetValue(target, new Color(ToByte(v.X), ToByte(v.Y), ToByte(v.Z), ToByte(v.W)));
            return true;
        }

        if (type == typeof(string))
        {
            var v = (string?)value ?? string.Empty;
            if (!ImGui.InputText(label, ref v, 128)) return false;
            field.SetValue(target, v);
            return true;
        }

        if (type.IsEnum)
        {
            var names = Enum.GetNames(type);
            var current = Array.IndexOf(names, value!.ToString());
            if (!ImGui.Combo(label, ref current, names, names.Length)) return false;
            field.SetValue(target, Enum.Parse(type, names[current]));
            return true;
        }

        // Fuori elenco (Entity, ModelHandle, ...): mostrato ma non editabile. Sono
        // riferimenti, non numeri — un DragInt sull'id di un'entità è un piede nel
        // fucile, non una feature. Serviranno widget dedicati (un picker).
        // TextWrapped e non TextDisabled: il ToString di un record struct è lungo e
        // sborderebbe dal pannello invece di andare a capo.
        ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]);
        ImGui.TextWrapped($"{label}: {value}");
        ImGui.PopStyleColor();
        return false;
    }

    private static byte ToByte(float normalized)
    {
        return (byte)Math.Clamp(MathF.Round(normalized * 255f), 0f, 255f);
    }

    /// <summary>"MeshRendererComponent" → "MeshRenderer": il suffisso è rumore, qui sono tutti componenti.</summary>
    private static string DisplayName(Type type)
    {
        const string suffix = "Component";
        return type.Name.EndsWith(suffix, StringComparison.Ordinal) && type.Name.Length > suffix.Length
            ? type.Name[..^suffix.Length]
            : type.Name;
    }
}
