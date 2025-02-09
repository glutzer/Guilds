﻿using MareLib;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;

namespace Guilds;

public static class GuiThemes
{
    public static Vector4 ButtonColor => new(0.5f, 0, 0, 1);
    public static Vector4 DarkColor => new(0.1f, 0.1f, 0.1f, 1);
    public static Vector4 ButtonFontColor => new(0.8f, 0.8f, 0.8f, 1);

    private static readonly Dictionary<string, object> cache = new();

    public static Texture Blank => GetOrCreate("blank", () => Texture.Create("guilds:textures/gui/blank.png"));
    public static NineSliceTexture Background => GetOrCreate("background", () => Texture.Create("guilds:textures/gui/background.png").AsNineSlice(14, 14));
    public static NineSliceTexture Button => GetOrCreate("button", () => Texture.Create("guilds:textures/gui/button.png").AsNineSlice(14, 14));
    public static NineSliceTexture ScrollBar => GetOrCreate("scrollbar", () => Texture.Create("guilds:textures/gui/title.png").AsNineSlice(14, 14));
    public static NineSliceTexture Title => GetOrCreate("title", () => Texture.Create("guilds:textures/gui/title.png").AsNineSlice(14, 14));

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