using System.Collections.Generic;

namespace Guilds;

public static class GuiThemes
{
    private static readonly Dictionary<string, object> cache = [];

    public static Texture Blank => GetOrCreate("blank", () => Texture.Create("guilds:textures/gui/blank.png"));

    // Nine slice is over y coordinate to display entire thing, so sizing is important here.
    public static Texture Tab => GetOrCreate("tab", () => Texture.Create("guilds:textures/gui/tab40.png"));

    private static T GetOrCreate<T>(string path, Func<T> makeTex)
    {
        if (cache.TryGetValue(path, out object? value))
        {
            return (T)value;
        }
        else
        {
            object tex = makeTex()!;
            cache.Add(path, tex);
            return (T)tex;
        }
    }

    public static void ClearCache()
    {
        foreach (object obj in cache)
        {
            if (obj is IDisposable tex)
            {
                tex.Dispose();
            }
        }

        cache.Clear();
    }
}