using Raylib_cs;

namespace gEngine.Assets;

public class AssetManager(string rootDirectory, string assetsDir)
{
    private readonly string _rootDir =  Path.Combine(rootDirectory, assetsDir);
    
    private readonly Dictionary<string, Texture2D> _textures2D = new();
    private readonly Dictionary<string, Sound> _sounds = new();
    private readonly Dictionary<string, Music> _musics = new();

    public Texture2D LoadTexture2D(string relPath)
    {
        if (_textures2D.TryGetValue(Path.Combine(_rootDir, relPath), out var cached))
            return cached;

        var t = Raylib.LoadTexture(Path.Combine(_rootDir, relPath));
        _textures2D.Add(Path.Combine(_rootDir, relPath), t);
        return t;
    }

    public Sound LoadSound(string relPath)
    {
        if (_sounds.TryGetValue(Path.Combine(_rootDir, relPath), out var cached))
            return cached;
        
        var s = Raylib.LoadSound(Path.Combine(_rootDir, relPath));
        _sounds.Add(Path.Combine(_rootDir, relPath), s);
        return s;
    }

    public Music LoadMusicStream(string relPath)
    {
        if (_musics.TryGetValue(Path.Combine(_rootDir, relPath), out var cached))
            return cached;
        
        var m = Raylib.LoadMusicStream(Path.Combine(_rootDir, relPath));
        _musics.Add(Path.Combine(_rootDir, relPath), m);
        return m;
    }
    
    public void UnloadAll()
    {
        foreach (var texture in _textures2D.Values)
            Raylib.UnloadTexture(texture);
        
        foreach (var sound in _sounds.Values)
            Raylib.UnloadSound(sound);

        foreach (var music in _musics.Values)
        {
            Raylib.UnloadMusicStream(music);
        }

        _textures2D.Clear();
        _sounds.Clear();
        _musics.Clear();
    }
    
}