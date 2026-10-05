using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using ValleyEditor.Server;

namespace ValleyEditor.Sprites;

/// <summary>Renders item icons to PNG, cached by qualified item ID.</summary>
internal sealed class ItemSprites
{
    private readonly GameThreadDispatcher game;
    private readonly ConcurrentDictionary<string, byte[]> cache = new();

    public ItemSprites(GameThreadDispatcher game)
    {
        this.game = game;
    }

    /// <summary>Forget rendered icons, e.g. after textures were reloaded.</summary>
    public void Clear() => this.cache.Clear();

    public async Task<byte[]> GetPng(string qualifiedItemId)
    {
        if (this.cache.TryGetValue(qualifiedItemId, out byte[]? png))
            return png;

        // textures may only be read on the game thread; encoding can happen anywhere
        var (pixels, width, height) = await this.game.Run(() => ReadPixels(qualifiedItemId));
        png = Png.Encode(pixels, width, height);
        this.cache[qualifiedItemId] = png;
        return png;
    }

    private static (Color[] Pixels, int Width, int Height) ReadPixels(string qualifiedItemId)
    {
        ParsedItemData data = ItemRegistry.GetData(qualifiedItemId)
            ?? throw new ApiException(404, $"No item with ID '{qualifiedItemId}'.");

        Texture2D texture = data.GetTexture();
        Rectangle source = Rectangle.Intersect(data.GetSourceRect(), texture.Bounds);
        if (source.IsEmpty)
            throw new ApiException(404, $"Item '{qualifiedItemId}' has no sprite.");

        var pixels = new Color[source.Width * source.Height];
        texture.GetData(0, source, pixels, 0, pixels.Length);
        return (pixels, source.Width, source.Height);
    }
}
