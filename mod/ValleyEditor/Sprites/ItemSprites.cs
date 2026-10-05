using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using ValleyEditor.Server;

namespace ValleyEditor.Sprites;

/// <summary>Renders game sprites (item icons, NPC portraits) to PNG, cached by key.</summary>
internal sealed class ItemSprites
{
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
        Texture2D texture = data.GetTexture();
        return (texture, data.GetSourceRect());
    });

    /// <summary>The neutral portrait (first 64×64 frame) of a villager.</summary>
    public Task<byte[]> GetPortraitPng(string npcName) => this.GetPng("portrait:" + npcName, () =>
    {
        NPC npc = Game1.getCharacterFromName(npcName)
            ?? throw new ApiException(404, $"No NPC named '{npcName}'.");
        Texture2D texture = npc.Portrait ?? throw new ApiException(404, $"{npcName} has no portrait.");
        return (texture, new Rectangle(0, 0, 64, 64));
    });

    private async Task<byte[]> GetPng(string key, Func<(Texture2D Texture, Rectangle Source)> getSource)
    {
        if (this.cache.TryGetValue(key, out byte[]? png))
            return png;

        // textures may only be read on the game thread; encoding can happen anywhere
        var (pixels, width, height) = await this.game.Run(() => ReadPixels(getSource()));
        png = Png.Encode(pixels, width, height);
        this.cache[key] = png;
        return png;
    }

    private static (Color[] Pixels, int Width, int Height) ReadPixels((Texture2D Texture, Rectangle Source) sprite)
    {
        Rectangle source = Rectangle.Intersect(sprite.Source, sprite.Texture.Bounds);
        if (source.IsEmpty)
            throw new ApiException(404, "This sprite is empty.");

        var pixels = new Color[source.Width * source.Height];
        sprite.Texture.GetData(0, source, pixels, 0, pixels.Length);
        return (pixels, source.Width, source.Height);
    }
}
