using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.GameData.Locations;
using ValleyEditor.Rules;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>Fishing: catch rules, treasure chest table, fishing spot tables, and the fish collection.</summary>
internal sealed class FishingDomain : Domain
{
    private readonly RulesService rules;

    public FishingDomain(GameThreadDispatcher game, EditorState state, RulesService rules)
        : base(game, state)
    {
        this.rules = rules;
    }

    public override void Register(Router router)
    {
        router.Get("/api/fishing", _ => this.Read(Snapshot));

        router.Patch("/api/fishing", request =>
        {
            JObject body = request.BodyObject;
            int? perCatch = OptInt(body, "fishPerCatch", 1, 999);
            int? quality = OptInt(body, "fishQuality", -1, 4);
            int? treasureMultiplier = OptInt(body, "treasureMultiplier", 1, 999);
            bool? maxSize = OptBool(body, "fishMaxSize");
            bool? replace = OptBool(body, "treasureReplaceVanilla");
            if (quality == 3)
                throw new ApiException(400, "'fishQuality' must be -1 (game's choice), 0, 1, 2 or 4.");

            bool setForced = body.ContainsKey("forcedFishId");
            string? forced = body.Value<string>("forcedFishId");
            if (string.IsNullOrWhiteSpace(forced))
                forced = null;

            return this.Write(() =>
            {
                if (forced != null && ItemRegistry.GetMetadata(forced)?.Exists() != true)
                    throw new ApiException(400, $"No item with ID '{forced}'.");
                this.rules.Update(r =>
                {
                    r.FishPerCatch = perCatch ?? r.FishPerCatch;
                    r.FishQuality = quality ?? r.FishQuality;
                    r.TreasureMultiplier = treasureMultiplier ?? r.TreasureMultiplier;
                    r.FishMaxSize = maxSize ?? r.FishMaxSize;
                    r.TreasureReplaceVanilla = replace ?? r.TreasureReplaceVanilla;
                    if (setForced)
                        r.ForcedFishId = forced is null ? null : ItemRegistry.QualifyItemId(forced);
                });
                return Snapshot();
            });
        });

        router.Put("/api/fishing/treasure", request => this.Write(() =>
        {
            List<LootEntry> loot = LootJson.Parse(request.BodyObject["entries"], "entries");
            this.rules.Update(r => r.TreasureLoot = loot);
            return Snapshot();
        }));

        router.Get("/api/fishing/tables", _ => this.Read(this.Tables));

        // replace the edits of one location's table
        router.Put("/api/fishing/tables/{location}", request => this.Write(() =>
        {
            string location = request.Params["location"];
            JObject body = request.BodyObject;
            var edit = new FishTableEdit();

            if (body["chances"] is JObject chances)
            {
                foreach (var (id, value) in chances)
                {
                    if (value?.Type is JTokenType.Float or JTokenType.Integer)
                    {
                        float chance = value.Value<float>();
                        edit.Chances[id] = chance is >= 0 and <= 1 ? chance : throw new ApiException(400, "Chances must be between 0 and 1.");
                    }
                }
            }
            if (body["removed"] is JArray removed)
                edit.Removed = removed.Select(r => r.Value<string>()).Where(r => !string.IsNullOrEmpty(r)).ToHashSet()!;
            if (body["added"] != null)
                edit.Added = LootJson.Parse(body["added"], "added");

            this.rules.Update(r =>
            {
                if (edit.IsEmpty)
                    r.FishTables.Remove(location);
                else
                    r.FishTables[location] = edit;
            });
            return this.Tables();
        }));

        router.Put("/api/fishing/collection/{fishId}", request =>
        {
            string fishId = ItemRegistry.QualifyItemId(request.Params["fishId"]) ?? request.Params["fishId"];
            int count = OptInt(request.BodyObject, "count", 0, 1_000_000) ?? throw new ApiException(400, "'count' is required.");
            int size = OptInt(request.BodyObject, "maxSize", 0, 10_000) ?? 0;
            return this.Write(() =>
            {
                Farmer player = Game1.player;
                if (count == 0)
                    player.fishCaught.Remove(fishId);
                else
                    player.fishCaught[fishId] = new[] { count, size };
                return Snapshot();
            });
        });

        // mark every fish as caught at least once, at its record size (Master Angler, collections tab)
        router.Post("/api/fishing/collection/catch-all", _ => this.Write(() =>
        {
            Farmer player = Game1.player;
            foreach (string id in DataLoader.Fish(Game1.content).Keys)
            {
                string fishId = "(O)" + id;
                if (ItemRegistry.GetData(fishId) is null)
                    continue;
                int size = FishingService.MaxSize(fishId) ?? 0;
                if (player.fishCaught.TryGetValue(fishId, out int[]? stats) && stats.Length >= 2)
                    player.fishCaught[fishId] = new[] { Math.Max(1, stats[0]), Math.Max(stats[1], size) };
                else
                    player.fishCaught[fishId] = new[] { 1, size };
            }
            return Snapshot();
        }));
    }

    private static object Snapshot()
    {
        RulesData r = RulesService.Current;
        Farmer player = Game1.player;

        return new
        {
            Settings = new
            {
                r.FishPerCatch,
                r.FishQuality,
                r.FishMaxSize,
                r.ForcedFishId,
                r.TreasureMultiplier,
                r.TreasureReplaceVanilla,
                TreasureLoot = r.TreasureLoot.Select(LootJson.From),
            },
            Fish = DataLoader.Fish(Game1.content)
                .Select(pair => (Id: "(O)" + pair.Key, Raw: pair.Value))
                .Where(f => ItemRegistry.GetData(f.Id) != null)
                .Select(f =>
                {
                    player.fishCaught.TryGetValue(f.Id, out int[]? stats);
                    return new
                    {
                        f.Id,
                        Name = LootJson.ItemName(f.Id),
                        CrabPot = f.Raw.Split('/').ElementAtOrDefault(1) == "trap",
                        MaxSize = FishingService.MaxSize(f.Id),
                        Caught = stats?.ElementAtOrDefault(0) ?? 0,
                        RecordSize = stats?.ElementAtOrDefault(1) ?? 0,
                    };
                })
                .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
        };
    }

    private object Tables()
    {
        // reading Data/Locations makes sure the baselines were captured
        Dictionary<string, LocationData> locations = DataLoader.Locations(Game1.content);
        RulesData r = RulesService.Current;

        return this.rules.FishBaselines
            .Select(pair =>
            {
                r.FishTables.TryGetValue(pair.Key, out FishTableEdit? edit);
                return new
                {
                    Location = pair.Key,
                    DisplayName = locations.TryGetValue(pair.Key, out LocationData? data) ? TokenizedName(data) ?? pair.Key : pair.Key,
                    Entries = pair.Value.Select(fish => new
                    {
                        fish.Id,
                        fish.ItemId,
                        Name = LootJson.ItemName(fish.ItemId) ?? string.Join(" | ", fish.RandomItemId ?? new List<string>()),
                        BaseChance = fish.Chance,
                        Chance = fish.Id != null && edit?.Chances.TryGetValue(fish.Id, out float c) == true ? c : fish.Chance,
                        Removed = fish.Id != null && edit?.Removed.Contains(fish.Id) == true,
                        Season = fish.Season?.ToString().ToLowerInvariant(),
                        fish.Condition,
                        fish.IsBossFish,
                    }),
                    Added = edit?.Added.Select(LootJson.From) ?? Enumerable.Empty<object>(),
                };
            })
            .OrderBy(t => t.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string? TokenizedName(LocationData data)
    {
        return string.IsNullOrEmpty(data.DisplayName) ? null : StardewValley.TokenizableStrings.TokenParser.ParseText(data.DisplayName);
    }

    private static bool? OptBool(JObject body, string name)
    {
        JToken? token = body[name];
        if (token is null || token.Type == JTokenType.Null)
            return null;
        return token.Type == JTokenType.Boolean ? token.Value<bool>() : throw new ApiException(400, $"'{name}' must be true or false.");
    }
}
