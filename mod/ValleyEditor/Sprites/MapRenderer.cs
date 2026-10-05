using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.ItemTypeDefinitions;
using StardewValley.TerrainFeatures;
using xTile.Layers;
using xTile.Tiles;
using SObject = StardewValley.Object;

namespace ValleyEditor.Sprites;

/// <summary>
/// Draws a location at 1× scale (16 px per tile) like the game does, minus characters and effects:
/// the Back and Buildings map layers, then objects, trees and buildings sorted by depth, then the Front layers.
/// Must run on the game thread.
/// </summary>
internal sealed class MapRenderer
{
    public const int TileSize = 16;

    private readonly Dictionary<Texture2D, Color[]> texturePixels = new();
    private readonly Dictionary<string, Texture2D?> tileSheets = new();
    private readonly Color[] canvas;
    private readonly int width;
    private readonly int height;

    private MapRenderer(int width, int height)
    {
        this.width = width;
        this.height = height;
        this.canvas = new Color[width * height];
    }

    /// <summary>The location's size in pixels at 1× scale.</summary>
    public static Point Size(GameLocation location)
    {
        Layer back = location.Map.GetLayer("Back");
        return new Point(back.LayerWidth * TileSize, back.LayerHeight * TileSize);
    }

    /// <summary>Where a building's sprite is drawn, in pixels at 1× scale (Building.draw).</summary>
    public static Rectangle BuildingBounds(Building building)
    {
        Rectangle source = building.getSourceRect();
        Vector2 offset = building.GetData()?.DrawOffset ?? Vector2.Zero;
        int bottom = (building.tileY.Value + building.tilesHigh.Value) * TileSize + (int)offset.Y;
        return new Rectangle(building.tileX.Value * TileSize + (int)offset.X, bottom - source.Height, source.Width, source.Height);
    }

    public static (Color[] Pixels, int Width, int Height) Render(GameLocation location)
    {
        Point size = Size(location);
        var renderer = new MapRenderer(size.X, size.Y);
        renderer.DrawLocation(location);
        return (renderer.canvas, size.X, size.Y);
    }

    private void DrawLocation(GameLocation location)
    {
        Layer[] layers = location.Map.Layers.Where(l => l.Id != "Paths").ToArray();
        static bool IsBelow(Layer l) => l.Id.StartsWith("Back") || l.Id.StartsWith("Buildings");

        foreach (Layer layer in layers.Where(IsBelow).OrderBy(l => l.Id.StartsWith("Buildings") ? 1 : 0))
            this.DrawLayer(layer);

        // things in the world, drawn bottom-most last like the game's depth sort
        var drawables = new List<(int SortY, Action Draw)>();
        foreach ((Vector2 tile, SObject obj) in location.objects.Pairs)
        {
            ParsedItemData data = ItemRegistry.GetDataOrErrorItem(obj.QualifiedItemId);
            Rectangle source = data.GetSourceRect();
            int x = (int)tile.X * TileSize, y = (int)tile.Y * TileSize + TileSize - source.Height;
            drawables.Add(((int)tile.Y * TileSize + TileSize, () => this.Blit(data.GetTexture(), source, x, y)));
        }
        foreach ((Vector2 tile, TerrainFeature feature) in location.terrainFeatures.Pairs)
        {
            if (feature is Tree tree && tree.texture.Value is { } texture)
                drawables.Add(((int)tile.Y * TileSize + TileSize, () => this.DrawTree(tree, texture, tile)));
        }
        foreach (Building building in location.buildings)
        {
            if (building.isUnderConstruction())
                continue; // the UI outlines its footprint instead
            Rectangle bounds = BuildingBounds(building);
            drawables.Add((bounds.Bottom, () => this.Blit(building.texture.Value, building.getSourceRect(), bounds.X, bounds.Y)));
        }
        foreach ((_, Action draw) in drawables.OrderBy(d => d.SortY))
            draw();

        foreach (Layer layer in layers.Where(l => !IsBelow(l)).OrderBy(l => l.Id.StartsWith("AlwaysFront") ? 1 : 0))
            this.DrawLayer(layer);
    }

    /// <summary>Tree.draw at 1×: saplings from the bottom row of the sheet, grown trees as stump plus leafy top.</summary>
    private void DrawTree(Tree tree, Texture2D texture, Vector2 tile)
    {
        int x = (int)tile.X * TileSize, y = (int)tile.Y * TileSize;
        if (tree.growthStage.Value < Tree.treeStage)
        {
            Rectangle source = tree.growthStage.Value switch
            {
                0 => new Rectangle(32, 128, 16, 16),
                1 => new Rectangle(0, 128, 16, 16),
                2 => new Rectangle(16, 128, 16, 16),
                _ => new Rectangle(0, 96, 16, 32),
            };
            this.Blit(texture, source, x, y + TileSize - source.Height);
            return;
        }
        Rectangle stump = Tree.stumpSourceRect;
        if (tree.hasMoss.Value)
            stump.X += 96;
        this.Blit(texture, stump, x, y - TileSize);
        if (!tree.stump.Value)
            this.Blit(texture, new Rectangle(tree.hasMoss.Value ? 96 : 0, 0, 48, 96), x - TileSize, y - 5 * TileSize);
    }

    private void DrawLayer(Layer layer)
    {
        for (int ty = 0; ty < layer.LayerHeight; ty++)
        {
            for (int tx = 0; tx < layer.LayerWidth; tx++)
            {
                Tile? tile = layer.Tiles[tx, ty];
                if (tile is AnimatedTile animated)
                    tile = animated.TileFrames.FirstOrDefault();
                if (tile?.TileSheet is not { } sheet || this.TileSheet(sheet) is not { } texture)
                    continue;
                xTile.Dimensions.Rectangle bounds = sheet.GetTileImageBounds(tile.TileIndex);
                this.Blit(texture, new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height), tx * TileSize, ty * TileSize);
            }
        }
    }

    /// <summary>Load a tile sheet's texture the way the map display device does (by its image source asset name).</summary>
    private Texture2D? TileSheet(TileSheet sheet)
    {
        if (!this.tileSheets.TryGetValue(sheet.ImageSource, out Texture2D? texture))
        {
            try
            {
                texture = Game1.content.Load<Texture2D>(sheet.ImageSource);
            }
            catch
            {
                texture = null; // a sheet that can't load is skipped rather than failing the whole map
            }
            this.tileSheets[sheet.ImageSource] = texture;
        }
        return texture;
    }

    /// <summary>Alpha-blend a texture region onto the canvas (game textures are premultiplied).</summary>
    private void Blit(Texture2D texture, Rectangle source, int destX, int destY)
    {
        if (!this.texturePixels.TryGetValue(texture, out Color[]? pixels))
        {
            pixels = new Color[texture.Width * texture.Height];
            texture.GetData(pixels);
            this.texturePixels[texture] = pixels;
        }
        source = Rectangle.Intersect(source, texture.Bounds);

        for (int y = 0; y < source.Height; y++)
        {
            int cy = destY + y;
            if (cy < 0 || cy >= this.height)
                continue;
            for (int x = 0; x < source.Width; x++)
            {
                int cx = destX + x;
                if (cx < 0 || cx >= this.width)
                    continue;
                Color src = pixels[(source.Y + y) * texture.Width + source.X + x];
                if (src.A == 0)
                    continue;
                ref Color dst = ref this.canvas[cy * this.width + cx];
                if (src.A == 255)
                {
                    dst = src;
                    continue;
                }
                float keep = 1 - src.A / 255f;
                dst = new Color(
                    (byte)Math.Min(255, src.R + dst.R * keep),
                    (byte)Math.Min(255, src.G + dst.G * keep),
                    (byte)Math.Min(255, src.B + dst.B * keep),
                    (byte)Math.Min(255, src.A + dst.A * keep));
            }
        }
    }
}
