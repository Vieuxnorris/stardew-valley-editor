using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using ValleyEditor.Server;

namespace ValleyEditor.Sprites;

/// <summary>Renders game sprites (item icons, NPC portraits, monsters, the world map) to PNG.</summary>
internal sealed class ItemSprites
{
    /// <summary>Monster frame sizes that differ from the AnimatedSprite default of 16×24 (set in each monster class's constructor).</summary>
    private static readonly Dictionary<string, Point> MonsterFrames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Big Slime"] = new(32, 32), ["Serpent"] = new(32, 32), ["Royal Serpent"] = new(32, 32), ["Pepper Rex"] = new(32, 32),
        ["Spider"] = new(32, 32), ["Shadow Sniper"] = new(32, 32),
        ["Blue Squid"] = new(24, 24),
        ["Bug"] = new(16, 16), ["Armored Bug"] = new(16, 16), ["Dwarvish Sentry"] = new(16, 16), ["Lava Lurk"] = new(16, 16),
        ["Metal Head"] = new(16, 16), ["Hot Head"] = new(16, 16), ["Spiker"] = new(16, 16), ["Squid Kid"] = new(16, 16),
        ["Mummy"] = new(16, 32), ["Shadow Brute"] = new(16, 32), ["Shadow Shaman"] = new(16, 32), ["Skeleton"] = new(16, 32),
        ["Skeleton Mage"] = new(16, 32), ["Shadow Girl"] = new(16, 32), ["Shadow Guy"] = new(16, 32),
    };

    /// <summary>Monsters drawn with another monster's texture, and the tint the game applies (slimes are grey sheets coloured at draw time; GreenSlime constructor).</summary>
    private static readonly Dictionary<string, (string Texture, Color Tint)> MonsterLooks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Green Slime"] = ("Green Slime", new Color(40, 220, 30)),
        ["Frost Jelly"] = ("Green Slime", new Color(40, 180, 230)),
        ["Sludge"] = ("Green Slime", new Color(210, 50, 60)),
        ["Shadow Guy"] = ("Shadow Brute", Color.White),
        ["Skeleton Warrior"] = ("Skeleton", Color.White),
    };

    private readonly GameThreadDispatcher game;
    private readonly ConcurrentDictionary<string, byte[]> cache = new();

    public ItemSprites(GameThreadDispatcher game)
    {
        this.game = game;
    }

    /// <summary>Forget rendered sprites, e.g. after textures were reloaded.</summary>
    public void Clear() => this.cache.Clear();

    public Task<byte[]> GetItemPng(string qualifiedItemId) => this.GetPng("item:" + qualifiedItemId, () =>
    {
        ParsedItemData data = ItemRegistry.GetData(qualifiedItemId)
            ?? throw new ApiException(404, $"No item with ID '{qualifiedItemId}'.");
        return ReadPixels(data.GetTexture(), data.GetSourceRect());
    });

    /// <summary>The neutral portrait (first 64×64 frame) of a villager.</summary>
    public Task<byte[]> GetPortraitPng(string npcName) => this.GetPng("portrait:" + npcName, () =>
    {
        NPC npc = Game1.getCharacterFromName(npcName)
            ?? throw new ApiException(404, $"No NPC named '{npcName}'.");
        Texture2D texture = npc.Portrait ?? throw new ApiException(404, $"{npcName} has no portrait.");
        return ReadPixels(texture, new Rectangle(0, 0, 64, 64));
    });

    /// <summary>The first frame of a monster's sprite sheet (facing down), trimmed to its visible pixels.</summary>
    public Task<byte[]> GetMonsterPng(string monsterName) => this.GetPng("monster:" + monsterName, () =>
    {
        (string sheet, Color tint) = MonsterLooks.TryGetValue(monsterName, out var look) ? look : (monsterName, Color.White);
        Texture2D texture;
        try
        {
            texture = Game1.content.Load<Texture2D>("Characters\\Monsters\\" + sheet);
        }
        catch (ContentLoadException)
        {
            throw new ApiException(404, $"No sprite for monster '{monsterName}'.");
        }
        Point frame = MonsterFrames.TryGetValue(sheet, out Point size) ? size : new Point(16, 24);
        var (pixels, width, height) = ReadPixels(texture, new Rectangle(0, 0, frame.X, frame.Y));
        if (tint != Color.White)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color p = pixels[i];
                pixels[i] = new Color(p.R * tint.R / 255, p.G * tint.G / 255, p.B * tint.B / 255, p.A);
            }
        }
        return Trim(pixels, width, height);
    });

    /// <summary>Render pixels produced on the game thread (e.g. a composed map); not cached.</summary>
    public async Task<byte[]> RenderPng(Func<(Color[] Pixels, int Width, int Height)> render)
    {
        var (pixels, width, height) = await this.game.Run(render);
        return Png.Encode(pixels, width, height);
    }

    /// <summary>Read a texture region. Must run on the game thread.</summary>
    public static (Color[] Pixels, int Width, int Height) ReadPixels(Texture2D texture, Rectangle sourceRect)
    {
        Rectangle source = Rectangle.Intersect(sourceRect, texture.Bounds);
        if (source.IsEmpty)
            throw new ApiException(404, "This sprite is empty.");

        var pixels = new Color[source.Width * source.Height];
        texture.GetData(0, source, pixels, 0, pixels.Length);
        return (pixels, source.Width, source.Height);
    }

    private async Task<byte[]> GetPng(string key, Func<(Color[] Pixels, int Width, int Height)> read)
    {
        if (this.cache.TryGetValue(key, out byte[]? png))
            return png;

        // textures may only be read on the game thread; encoding can happen anywhere
        var (pixels, width, height) = await this.game.Run(read);
        png = Png.Encode(pixels, width, height);
        this.cache[key] = png;
        return png;
    }

    /// <summary>Crop fully transparent rows and columns around a sprite.</summary>
    private static (Color[] Pixels, int Width, int Height) Trim(Color[] pixels, int width, int height)
    {
        int left = width, top = height, right = -1, bottom = -1;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (pixels[y * width + x].A == 0)
                    continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }
        if (right < 0)
            return (pixels, width, height);

        int w = right - left + 1, h = bottom - top + 1;
        var cropped = new Color[w * h];
        for (int y = 0; y < h; y++)
            Array.Copy(pixels, (top + y) * width + left, cropped, y * w, w);
        return (cropped, w, h);
    }
}
