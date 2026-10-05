using System.Collections.Generic;
using System.Linq;
using StardewValley;
using SObject = StardewValley.Object;

namespace ValleyEditor.Domains;

/// <summary>JSON shapes for item stacks, shared by the inventory and (later) chests.</summary>
internal static class ItemJson
{
    /// <summary>The quality values the game uses: normal, silver, gold, iridium.</summary>
    public static readonly int[] Qualities = { SObject.lowQuality, SObject.medQuality, SObject.highQuality, SObject.bestQuality };

    public static object? From(Item? item)
    {
        if (item is null)
            return null;

        return new
        {
            QualifiedId = item.QualifiedItemId,
            Name = item.DisplayName,
            item.Stack,
            MaxStack = item.maximumStackSize(),
            item.Quality,
            CanHaveQuality = CanHaveQuality(item),
        };
    }

    public static object?[] Slots(IList<Item?> items, int size) => Enumerable.Range(0, size).Select(i => i < items.Count ? From(items[i]) : null).ToArray();

    /// <summary>Only regular objects (crops, fish, flowers, artisan goods...) show quality stars.</summary>
    public static bool CanHaveQuality(Item item) => item.TypeDefinitionId == ItemRegistry.type_object;
}
