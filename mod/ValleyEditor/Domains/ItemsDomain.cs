using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StardewModdingAPI;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using ValleyEditor.Server;
using ValleyEditor.Sprites;
using SObject = StardewValley.Object;

namespace ValleyEditor.Domains;

/// <summary>An item that can be spawned, as listed in the catalog.</summary>
internal sealed record CatalogEntry(string QualifiedId, string Name, string InternalName, string Type, int Category, string CategoryName, string? ModId, string? ModName);

/// <summary>Search every registered item (vanilla and modded) and serve item icons.</summary>
internal sealed class ItemsDomain : Domain
{
    private const int MaxPageSize = 200;

    private readonly IModRegistry modRegistry;
    private readonly ItemSprites sprites;
    private Task<object?>? catalogLoad;

    public ItemsDomain(GameThreadDispatcher game, EditorState state, IModRegistry modRegistry, ItemSprites sprites)
        : base(game, state)
    {
        this.modRegistry = modRegistry;
        this.sprites = sprites;
    }

    /// <summary>Rebuild the catalog and icons on next use (item data may have changed).</summary>
    public void Invalidate()
    {
        this.catalogLoad = null;
        this.sprites.Clear();
    }

    public override void Register(Router router)
    {
        router.Get("/api/items", async request =>
        {
            CatalogEntry[] catalog = await this.GetCatalog();
            string? query = request.Query["q"]?.Trim();
            string? type = request.Query["type"];
            string? mod = request.Query["mod"];
            int offset = Math.Max(0, ParseInt(request.Query["offset"], 0));
            int limit = Math.Clamp(ParseInt(request.Query["limit"], 60), 1, MaxPageSize);

            IEnumerable<CatalogEntry> matches = catalog;
            if (!string.IsNullOrEmpty(type))
                matches = matches.Where(e => e.Type == type);
            if (!string.IsNullOrEmpty(mod))
                matches = mod == "vanilla" ? matches.Where(e => e.ModId is null) : matches.Where(e => e.ModId == mod);
            if (!string.IsNullOrEmpty(query))
            {
                matches = matches.Where(e =>
                    e.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || e.InternalName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || e.QualifiedId.Equals(query, StringComparison.OrdinalIgnoreCase));
            }

            CatalogEntry[] all = matches.ToArray();
            return new { Total = all.Length, Items = all.Skip(offset).Take(limit) };
        });

        router.Get("/api/items/facets", async _ =>
        {
            CatalogEntry[] catalog = await this.GetCatalog();
            return new
            {
                Types = catalog.GroupBy(e => e.Type).OrderBy(g => g.Key).Select(g => new { Id = g.Key, Count = g.Count() }),
                Mods = catalog.GroupBy(e => (e.ModId, e.ModName)).OrderBy(g => g.Key.ModName).Select(g => new { Id = g.Key.ModId ?? "vanilla", Name = g.Key.ModName, Count = g.Count() }),
            };
        });

        router.Get("/api/sprites/{id}", async request => new BinaryResult(await this.sprites.GetItemPng(request.Params["id"]), "image/png"));
    }

    private async Task<CatalogEntry[]> GetCatalog()
    {
        this.catalogLoad ??= this.Read(this.BuildCatalog);
        try
        {
            return (CatalogEntry[])(await this.catalogLoad)!;
        }
        catch
        {
            this.catalogLoad = null; // retry next time, e.g. once a save is loaded
            throw;
        }
    }

    private object? BuildCatalog()
    {
        // longest IDs first, so 'Author.Mod.Extra' wins over 'Author.Mod'
        IModInfo[] mods = this.modRegistry.GetAll().OrderByDescending(m => m.Manifest.UniqueID.Length).ToArray();

        var entries = new List<CatalogEntry>();
        foreach (IItemDataDefinition type in ItemRegistry.ItemTypes)
        {
            foreach (string id in type.GetAllIds())
            {
                ParsedItemData? data;
                try
                {
                    data = type.GetData(id);
                }
                catch
                {
                    continue; // broken modded item data shouldn't hide the rest of the catalog
                }
                if (data is null || data.IsErrorItem)
                    continue;

                IModInfo? mod = mods.FirstOrDefault(m => IsFromMod(data.ItemId, m.Manifest.UniqueID));
                entries.Add(new CatalogEntry(
                    QualifiedId: data.QualifiedItemId,
                    Name: data.DisplayName,
                    InternalName: data.InternalName,
                    Type: type.Identifier,
                    Category: data.Category,
                    CategoryName: SObject.GetCategoryDisplayName(data.Category),
                    ModId: mod?.Manifest.UniqueID,
                    ModName: mod?.Manifest.Name));
            }
        }

        return entries.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    /// <summary>Content packs conventionally prefix item IDs with their mod ID (e.g. '{{ModId}}_Cheese').</summary>
    private static bool IsFromMod(string itemId, string modId)
    {
        return itemId.Length > modId.Length
            && itemId.StartsWith(modId, StringComparison.OrdinalIgnoreCase)
            && !char.IsLetterOrDigit(itemId[modId.Length]);
    }

    private static int ParseInt(string? value, int fallback) => int.TryParse(value, out int parsed) ? parsed : fallback;
}
