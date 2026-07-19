using System.Numerics;
using System.Text.Json;
using gEngine.Assets;
using gEngine.Ecs.Base;
using gEngine.Ecs.Component;
using gEngine.Log;
using gEngine.Physics;
using gEngine.Rendering;
using gEngine.Scenes;
using Raylib_cs;
using Color = gEngine.Rendering.Color;

namespace gEngine.Tests.Scenes;

/// <summary>
/// Il round-trip di serializzazione: <c>World → Scene → World</c>, e confronta.
///
/// Perché proprio questo e non "testare tutto": quel codice regge <b>tre</b> cose insieme —
/// il Salva dell'editor, il Play/Stop (lo snapshot <i>è</i> il serializer) e, domani,
/// l'hot-reload degli script (snapshot → ricompila → reistanzia). Finora è stato verificato
/// guardando dei cubi cadere.
///
/// La forma del confronto è quella che conta: si serializza il World di partenza, si
/// reistanzia in un World <b>nuovo</b>, si riserializza, e si confrontano le due
/// <see cref="Scene"/>. Confrontare i due World sarebbe più debole — gli id delle entità
/// cambiano di proposito, quindi il confronto dovrebbe ignorarli, e finirebbe per ignorare
/// proprio la cosa che il formato deve tradurre (i riferimenti fra entità).
/// </summary>
public class SceneRoundTripTests
{
    [Fact]
    public void UnGiroCompleto_NonPerdeNienteDiCioCheEDatoDAutore()
    {
        var assets = NewAssetManager();
        var registry = NewRegistry();

        var world = BuildFixture(assets);
        var primo = SceneSerializer.ToScene(world, registry, assets, "collaudo");

        var rientro = new World();
        SceneInstantiator.Instantiate(primo, rientro, registry, assets);
        var secondo = SceneSerializer.ToScene(rientro, registry, assets, "collaudo");

        var differenze = SceneComparison.Differences(primo, secondo);

        Assert.True(differenze.Count == 0,
            "Il giro ha perso o alterato qualcosa:" + Environment.NewLine +
            string.Join(Environment.NewLine, differenze));
    }

    /// <summary>
    /// ⚠️⚠️ <b>L'altra metà del round-trip, e non è una comodità: senza, il test qui sopra
    /// passa con il serializer rotto.</b>
    ///
    /// Il giro è cieco a una perdita <b>simmetrica</b>. Se il writer del MeshRenderer smette
    /// di scrivere <c>ModelPath</c>, allora la prima scena non ce l'ha, il World rientrante
    /// non carica nessun modello, e la seconda scena non ce l'ha uguale: le due coincidono,
    /// il confronto è contento, e il modello è stato perso. Misurato, non temuto — quel
    /// sabotaggio è stato provato e i test restavano tutti verdi.
    ///
    /// Vale per ogni campo asimmetrico allo stesso modo (<c>Parent</c> compreso): il
    /// round-trip verifica la <b>stabilità</b>, questo test verifica la <b>fedeltà</b>.
    /// Servono tutti e due.
    /// </summary>
    [Fact]
    public void LaScenaScritta_ContieneDavveroIDatiDAutore()
    {
        var assets = NewAssetManager();
        var registry = NewRegistry();
        var world = BuildFixture(assets);

        var scene = SceneSerializer.ToScene(world, registry, assets, "collaudo");

        Assert.Equal(5, scene.Entities.Count);

        var suolo = Entity(scene, "Suolo");
        Assert.Equal("""{"x":20,"y":1,"z":20}""", Property(suolo, "Transform", "Scale"));
        Assert.Equal("true", Property(suolo, "RigidBody", "IsStatic"));
        Assert.Equal("\"Cube\"", Property(suolo, "MeshRenderer", "Kind"));

        // Il pezzo che il sabotaggio faceva sparire in silenzio: l'handle NON va nel file
        // (è un id di questa esecuzione), il path sì.
        var giocatore = Entity(scene, "Giocatore");
        Assert.Equal("\"models/qualcosa/scene.gltf\"", Property(giocatore, "MeshRenderer", "ModelPath"));
        Assert.Null(Property(giocatore, "MeshRenderer", "Model"));

        // Il riferimento va scritto come NOME: gli id non sopravvivono al reload.
        var torcia = Entity(scene, "Torcia");
        Assert.Equal("\"Giocatore\"", torcia.Components["Parent"].GetRawText());
        Assert.Equal("\"Point\"", Property(torcia, "Light", "Kind"));
        Assert.Equal("2.5", Property(torcia, "Light", "Intensity"));

        Assert.Equal("true", Property(Entity(scene, "Camera principale"), "Camera", "Primary"));

        // Il nome è un campo dell'entità, non un componente: due punti di verità
        // divergerebbero. Vale per tutte, quindi si controlla su tutte.
        Assert.All(scene.Entities, definition => Assert.DoesNotContain("Name", definition.Components.Keys));

        // Il caso degenere: c'è, ed è vuota.
        var anonima = Assert.Single(scene.Entities, definition => definition.Name is null);
        Assert.Empty(anonima.Components);
    }

    /// <summary>
    /// ⚠️ Il test che rende non vacuo quello sopra.
    ///
    /// Un round-trip che confronta due scene con un confronto troppo permissivo passa anche
    /// con il serializer rotto, e non sta verificando niente. Qui si guastano le scene una
    /// alla volta, in sei modi diversi, e si pretende che il confronto se ne accorga —
    /// compreso il <b>riordino</b>, che una comparazione per nome lascerebbe passare.
    /// </summary>
    [Theory]
    [MemberData(nameof(Guasti))]
    public void IlConfronto_SiAccorgeDelGuasto(string descrizione, Action<Scene> guasta)
    {
        var assets = NewAssetManager();
        var registry = NewRegistry();
        var world = BuildFixture(assets);

        var sana = SceneSerializer.ToScene(world, registry, assets, "collaudo");
        var guasta_ = Clone(SceneSerializer.ToScene(world, registry, assets, "collaudo"));
        guasta(guasta_);

        var differenze = SceneComparison.Differences(sana, guasta_);

        Assert.True(differenze.Count > 0,
            $"Il confronto NON si è accorto di: {descrizione}. " +
            "Finché non se ne accorge, il test del round-trip passerebbe anche col serializer rotto.");
    }

    public static TheoryData<string, Action<Scene>> Guasti() => new()
    {
        {
            "un float spostato di un millesimo",
            scene => Patch(Entity(scene, "Giocatore"), "Transform", "Position", """{"x":1.001,"y":0,"z":3}""")
        },
        {
            "un enum cambiato (Cube -> Sphere)",
            scene => Patch(Entity(scene, "Suolo"), "MeshRenderer", "Kind", "\"Sphere\"")
        },
        {
            "un bool ribaltato (la camera non è più Primary)",
            scene => Patch(Entity(scene, "Camera principale"), "Camera", "Primary", "false")
        },
        {
            "un riferimento riappeso a un altro genitore",
            scene => Entity(scene, "Torcia").Components["Parent"] = Json("\"Suolo\"")
        },
        {
            "un componente sparito",
            scene => Entity(scene, "Suolo").Components.Remove("RigidBody")
        },
        {
            // Il caso che una comparazione per nome invece che per indice lascerebbe passare.
            "due entità scambiate di posto",
            scene => (scene.Entities[0], scene.Entities[1]) = (scene.Entities[1], scene.Entities[0])
        }
    };

    /// <summary>
    /// Il passaggio dal <b>disco</b>: la stessa andata e ritorno, ma con il file JSON in
    /// mezzo. Non è ridondante rispetto al primo test — lì la <see cref="Scene"/> resta in
    /// memoria e i <see cref="JsonElement"/> sono quelli originali; qui vengono riscritti e
    /// riparsati, che è ciò che succede davvero a un File > Save seguito da File > Open.
    /// </summary>
    [Fact]
    public void PassandoDalFile_IlGiroRestaEsatto()
    {
        var assets = NewAssetManager();
        var registry = NewRegistry();
        var world = BuildFixture(assets);

        var primo = SceneSerializer.ToScene(world, registry, assets, "collaudo");

        var path = Path.Combine(Path.GetTempPath(), $"gengine-roundtrip-{Guid.NewGuid():N}.json");
        try
        {
            JsonSceneLoader.Save(primo, path);
            var riletta = JsonSceneLoader.Load(path);

            var rientro = new World();
            SceneInstantiator.Instantiate(riletta, rientro, registry, assets);
            var secondo = SceneSerializer.ToScene(rientro, registry, assets, "collaudo");

            var differenze = SceneComparison.Differences(primo, secondo);

            Assert.True(differenze.Count == 0,
                "Il giro attraverso il file ha perso o alterato qualcosa:" + Environment.NewLine +
                string.Join(Environment.NewLine, differenze));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Lo stato di runtime non deve finire nel file: salvare un <see cref="BodyId"/> vuol
    /// dire salvare un id valido solo per questa esecuzione, che al reload punterebbe a un
    /// corpo a caso o a niente.
    ///
    /// ⚠️ Sta qui e non altrove perché è una regola che si può violare <b>senza accorgersene</b>:
    /// basta dimenticare <c>[RuntimeState]</c> su un componente nuovo e il file si sporca in
    /// silenzio, restando per giunta caricabile.
    /// </summary>
    [Fact]
    public void LoStatoDiRuntime_NonEntraNelFile()
    {
        var assets = NewAssetManager();
        var registry = NewRegistry();
        var world = BuildFixture(assets);

        // Precondizione: il fixture ce l'ha davvero. Senza questa riga il test passerebbe
        // anche se un domani il PhysicsBody sparisse dal fixture — cioè non verificherebbe niente.
        Assert.Contains(world.AllEntities, entity => world.HasComponent<PhysicsBodyComponent>(entity));

        var scene = SceneSerializer.ToScene(world, registry, assets, "collaudo");

        Assert.All(scene.Entities, definition =>
            Assert.DoesNotContain(definition.Components.Keys,
                key => key.Contains("Physics", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// I <c>_comment</c> scritti a mano nel file sopravvivono a un salvataggio, se gli si
    /// passa la scena d'origine. È il contratto dichiarato da <c>EntityDefinition.Extra</c>,
    /// e la ragione per cui esiste: senza, il primo Save cancellerebbe la documentazione
    /// scritta dentro le scene.
    /// </summary>
    [Fact]
    public void ICommentiDelFile_SopravvivonoAlSalvataggio()
    {
        var assets = NewAssetManager();
        var registry = NewRegistry();

        var origine = new Scene
        {
            Name = "collaudo",
            Extra = { ["_comment"] = Json("\"scena di collaudo\"") },
            Entities =
            {
                new EntityDefinition
                {
                    Name = "Suolo",
                    Components = { ["Transform"] = Json("""{"Position":{"x":0,"y":0,"z":0},"Rotation":{"x":0,"y":0,"z":0,"w":1},"Scale":{"x":1,"y":1,"z":1}}""") },
                    Extra = { ["_comment"] = Json("\"il pavimento, non toccare\"") }
                }
            }
        };

        var world = new World();
        SceneInstantiator.Instantiate(origine, world, registry, assets);

        var risalvata = SceneSerializer.ToScene(world, registry, assets, "collaudo", source: origine);

        Assert.Equal("\"scena di collaudo\"", risalvata.Extra["_comment"].GetRawText());
        Assert.Equal("\"il pavimento, non toccare\"", risalvata.Entities[0].Extra["_comment"].GetRawText());
    }

    /// <summary>
    /// Il rovescio dichiarato del test qui sopra: <b>senza</b> la scena d'origine i commenti
    /// si perdono. Non è un bug ed è per questo che è scritto — è il motivo per cui
    /// <c>ToScene</c> ha un parametro <c>source</c>, e chi un domani lo togliesse
    /// "perché tanto è opzionale" deve vedere fallire qualcosa.
    /// </summary>
    [Fact]
    public void SenzaLaScenaDOrigine_ICommentiSiPerdono()
    {
        var assets = NewAssetManager();
        var registry = NewRegistry();

        var origine = new Scene
        {
            Name = "collaudo",
            Entities =
            {
                new EntityDefinition
                {
                    Name = "Suolo",
                    Components = { ["Transform"] = Json("""{"Position":{"x":0,"y":0,"z":0},"Rotation":{"x":0,"y":0,"z":0,"w":1},"Scale":{"x":1,"y":1,"z":1}}""") },
                    Extra = { ["_comment"] = Json("\"il pavimento, non toccare\"") }
                }
            }
        };

        var world = new World();
        SceneInstantiator.Instantiate(origine, world, registry, assets);

        var risalvata = SceneSerializer.ToScene(world, registry, assets, "collaudo");

        Assert.Empty(risalvata.Entities[0].Extra);
    }

    /// <summary>
    /// Un riferimento verso un'entità <b>senza nome</b> non è scrivibile, e il salvataggio
    /// deve fallire dicendolo — non scrivere un file che si ricarica monco. Verifica anche
    /// che il messaggio contenga di che ritrovare il colpevole.
    /// </summary>
    [Fact]
    public void UnGenitoreSenzaNome_FaFallireIlSalvataggioConUnMessaggioUtile()
    {
        var assets = NewAssetManager();
        var registry = NewRegistry();

        var world = new World();
        var genitore = world.CreateEntity(); // di proposito senza NameComponent
        var figlio = world.CreateEntity();
        world.AddComponent(figlio, new NameComponent { Value = "Figlio" });
        world.AddComponent(figlio, new ParentComponent { Parent = genitore });

        var errore = Assert.Throws<InvalidOperationException>(
            () => SceneSerializer.ToScene(world, registry, assets, "collaudo"));

        Assert.Contains("Parent", errore.Message);
        Assert.Contains(genitore.Id.ToString(), errore.Message);

        // ⚠️ Questo messaggio finisce sotto ImGui (`MainMenuBar._status` quando il Salva
        // fallisce), e il font di default copre solo Latin-1: tutto il resto esce come '?'.
        // Non è teorico — qui dentro c'era una lineetta lunga '—' (U+2014), che si scrive
        // senza pensarci, ed è stata trovata scandendo i sorgenti invece che rileggendoli.
        Assert.DoesNotContain(errore.Message, character => character > 0xFF);
    }

    // ------ FIXTURE -----------------------------------------------------------------------

    /// <summary>
    /// Un World che contiene <b>un caso per ogni asimmetria</b> del formato, perché sono
    /// quelle che il round-trip può rompere:
    /// <list type="bullet">
    ///   <item><c>Parent</c>: nome → Entity in lettura, Entity → nome in scrittura;</item>
    ///   <item><c>MeshRenderer</c>: <c>ModelPath</c> → handle e ritorno, passando
    ///   dall'AssetManager;</item>
    ///   <item><c>Name</c>: campo dell'entità in un verso, <c>NameComponent</c> nell'altro;</item>
    ///   <item><c>[RuntimeState]</c>: c'è nel World e non deve esserci nel file;</item>
    ///   <item>un'entità <b>senza nome e senza componenti</b>, che è il caso degenere e
    ///   quello che si dimentica.</item>
    /// </list>
    /// I componenti simmetrici (Light, RigidBody, Camera) ci sono lo stesso: costano una riga
    /// e coprono i converter di Vector3/Quaternion/Color e degli enum come stringa.
    /// </summary>
    private static World BuildFixture(AssetManager assets)
    {
        var world = new World();

        var suolo = world.CreateEntity();
        world.AddComponent(suolo, new NameComponent { Value = "Suolo" });
        world.AddComponent(suolo, new TransformComponent
        {
            Position = Vector3.Zero,
            Rotation = Quaternion.Identity,
            Scale = new Vector3(20f, 1f, 20f)
        });
        world.AddComponent(suolo, new MeshRendererComponent
        {
            Kind = MeshKind.Cube,
            Size = Vector3.One,
            Tint = Color.White,
            Visible = true,
            Layer = RenderLayer.Opaque
        });
        world.AddComponent(suolo, new RigidBodyComponent
        {
            Shape = ColliderShape.Box,
            Size = new Vector3(20f, 1f, 20f),
            Mass = 0f,
            IsStatic = true
        });
        // Stato di runtime: nel World sì, nel file no.
        world.AddComponent(suolo, new PhysicsBodyComponent { Body = new BodyId(42) });

        var giocatore = world.CreateEntity();
        world.AddComponent(giocatore, new NameComponent { Value = "Giocatore" });
        world.AddComponent(giocatore, new TransformComponent
        {
            // Una rotazione non banale: l'identità passerebbe anche con un converter che
            // scrive quattro zeri e un uno a caso.
            Position = new Vector3(1f, 0f, 3f),
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.Normalize(new Vector3(1f, 2f, 3f)), 0.7f),
            Scale = Vector3.One
        });
        world.AddComponent(giocatore, new MeshRendererComponent
        {
            Kind = MeshKind.Model,
            Size = Vector3.One,
            Tint = new Color(200, 180, 160, 255),
            Unlit = true,
            Visible = true,
            Layer = RenderLayer.Opaque,
            SortingOrder = 3,
            Model = assets.LoadModel("models/qualcosa/scene.gltf")
        });

        // Figlia del giocatore: è qui che si verifica il giro del riferimento.
        var torcia = world.CreateEntity();
        world.AddComponent(torcia, new NameComponent { Value = "Torcia" });
        world.AddComponent(torcia, new TransformComponent
        {
            Position = new Vector3(0f, 1.8f, 0.2f),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        });
        world.AddComponent(torcia, new LightComponent
        {
            Kind = LightKind.Point,
            Color = new Color(255, 240, 200, 255),
            Intensity = 2.5f
        });
        world.AddComponent(torcia, new ParentComponent { Parent = giocatore });

        var camera = world.CreateEntity();
        world.AddComponent(camera, new NameComponent { Value = "Camera principale" });
        world.AddComponent(camera, new TransformComponent
        {
            Position = new Vector3(0f, 5f, -10f),
            Rotation = Quaternion.Identity,
            Scale = Vector3.One
        });
        world.AddComponent(camera, new CameraComponent
        {
            FovY = 60f,
            Near = 0.01f,
            Far = 1000f,
            Projection = CameraProjection.Perspective,
            Primary = true
        });

        // Il caso degenere: esiste, non ha nome, non ha componenti. Deve tornare indietro
        // uguale — cioè come una riga vuota nel file, non sparire.
        world.CreateEntity();

        return world;
    }

    private static SceneComponentRegistry NewRegistry()
    {
        var registry = new SceneComponentRegistry();
        registry.RegisterEngineDefaults();
        return registry;
    }

    // Un Logger senza sink è il logger nullo: non ha bisogno di un tipo apposta, perché
    // "senza sink non lancia e scarta tutto" è già una decisione presa (vedi Logger). Qui
    // serve proprio quello — questi test guardano la serializzazione, non il log.
    private static AssetManager NewAssetManager() =>
        new(Path.GetTempPath(), "assets", new FakeAssetBackend(), new Logger());

    // ------ HELPER DEI GUASTI -------------------------------------------------------------

    private static EntityDefinition Entity(Scene scene, string name) =>
        scene.Entities.Single(entity => entity.Name == name);

    private static JsonElement Json(string raw) => JsonDocument.Parse(raw).RootElement.Clone();

    /// <summary>Il JSON grezzo di una proprietà dentro un componente, o <c>null</c> se non c'è.</summary>
    private static string? Property(EntityDefinition entity, string component, string property)
    {
        if (!entity.Components.TryGetValue(component, out var data))
            return null;

        return data.TryGetProperty(property, out var value) ? value.GetRawText() : null;
    }

    /// <summary>Riscrive una sola proprietà dentro un componente, lasciando intatto il resto.</summary>
    private static void Patch(EntityDefinition entity, string component, string property, string rawValue)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(entity.Components[component].GetRawText())!.AsObject();
        node[property] = System.Text.Json.Nodes.JsonNode.Parse(rawValue);
        entity.Components[component] = Json(node.ToJsonString());
    }

    /// <summary>
    /// Copia profonda, perché i guasti mutano le collezioni della scena e le due copie
    /// arrivano dallo stesso World: senza, il guasto toccherebbe anche il termine di
    /// paragone e il test passerebbe per il motivo sbagliato.
    /// </summary>
    private static Scene Clone(Scene scene)
    {
        var copia = new Scene { Name = scene.Name };

        foreach (var (key, value) in scene.Extra)
            copia.Extra[key] = value;

        foreach (var entity in scene.Entities)
        {
            var definizione = new EntityDefinition { Name = entity.Name };

            foreach (var (key, value) in entity.Components)
                definizione.Components[key] = value;

            foreach (var (key, value) in entity.Extra)
                definizione.Extra[key] = value;

            copia.Entities.Add(definizione);
        }

        return copia;
    }
}
