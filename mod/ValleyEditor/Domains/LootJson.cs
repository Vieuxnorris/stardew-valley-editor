using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using ValleyEditor.Rules;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>Validation and JSON shapes for loot tables (treasure chests, fishing spots, monster drops).</summary>
internal static class LootJson
{
    /// <summary>Parse a list of loot entries from a JSON array, checking every item exists.</summary>
    /// <param name="objectsOnly">Whether only regular objects ((O) items) are allowed, as in monster drops.</param>
    public static List<LootEntry> Parse(JToken? token, string field, bool objectsOnly = false)
    {
        if (token is not JArray array)
            throw new ApiException(400, $"'{field}' must be an array.");

        var entries = new List<LootEntry>();
        foreach (JToken raw in array)
        {
            if (raw is not JObject obj)
                throw new ApiException(400, $"Each entry of '{field}' must be an object.");

            string itemId = obj.Value<string>("itemId")?.Trim() ?? throw new ApiException(400, "Each loot entry needs an 'itemId'.");
            ItemMetadata metadata = ItemRegistry.GetMetadata(itemId);
            if (metadata?.Exists() != true)
                throw new ApiException(400, $"No item with ID '{itemId}'.");
            if (objectsOnly && metadata.TypeIdentifier != ItemRegistry.type_object)
                throw new ApiException(400, $"'{itemId}' isn't a regular object; this table only takes objects.");

            double chance = obj["chance"]?.Type is JTokenType.Float or JTokenType.Integer ? obj.Value<double>("chance") : 1;
            int min = obj["minStack"]?.Type == JTokenType.Integer ? obj.Value<int>("minStack") : 1;
            int max = obj["maxStack"]?.Type == JTokenType.Integer ? obj.Value<int>("maxStack") : min;
            int quality = obj["quality"]?.Type == JTokenType.Integer ? obj.Value<int>("quality") : 0;
            if (chance is < 0 or > 1)
                throw new ApiException(400, "'chance' must be between 0 and 1.");
            if (min < 1 || max < min || max > 999)
                throw new ApiException(400, "Stacks must satisfy 1 ≤ minStack ≤ maxStack ≤ 999.");
            if (!ItemJson.Qualities.Contains(quality))
                throw new ApiException(400, "'quality' must be 0, 1, 2 or 4.");

            entries.Add(new LootEntry { ItemId = metadata.QualifiedItemId, Chance = chance, MinStack = min, MaxStack = max, Quality = quality });
        }
        return entries;
    }

    public static object From(LootEntry entry) => new
    {
        entry.ItemId,
        Name = ItemName(entry.ItemId),
        entry.Chance,
        entry.MinStack,
        entry.MaxStack,
        entry.Quality,
    };

    /// <summary>The display name of an item ID, or the ID itself for queries and unknown items.</summary>
    public static string? ItemName(string? itemId)
    {
        return itemId is null ? null : ItemRegistry.GetData(itemId)?.DisplayName ?? itemId;
    }
}
