using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.GameData.Objects;
using StardewValley.GameData.Powers;
using StardewValley.ItemTypeDefinitions;
using StardewValley.Locations;
using StardewValley.TokenizableStrings;
using ValleyEditor.Server;
using ValleyEditor.Sprites;
using SObject = StardewValley.Object;

namespace ValleyEditor.Domains;

/// <summary>The collections pages (shipped, fish, artifacts, minerals, cooking), crafting, and the wallet's special items and powers.</summary>
internal sealed class CollectionsDomain : Domain
{
    private static readonly string[] Categories = { "shipped", "fish", "artifacts", "minerals", "cooking", "crafting" };

    /// <summary>Cooking items the collections page skips (CollectionsPage).</summary>
    private static readonly HashSet<string> SkippedDishes = new() { "217", "772", "773", "279", "873" };

    private readonly ItemSprites sprites;

    public CollectionsDomain(GameThreadDispatcher game, EditorState state, ItemSprites sprites)
        : base(game, state)
    {
        this.sprites = sprites;
    }

    public override void Register(Router router)
    {
        router.Get("/api/collections", _ => this.Read(Snapshot));

        router.Post("/api/collections/{category}/complete", request =>
        {
            string category = Categories.FirstOrDefault(c => c == request.Params["category"])
                ?? throw new ApiException(404, $"Unknown collection '{request.Params["category"]}'. Use one of: {string.Join(", ", Categories)}.");
            return this.Write(() =>
            {
                int added = Complete(category);
                return new { Added = added, Collections = Snapshot() };
            });
        });

        router.Put("/api/collections/powers/{id}", request =>
        {
            JToken? token = request.BodyObject["value"];
            bool value = token?.Type == JTokenType.Boolean ? token.Value<bool>() : throw new ApiException(400, "'value' must be true or false.");
            return this.Write(() =>
            {
                Dictionary<string, PowersData> powers = DataLoader.Powers(Game1.content);
                if (!powers.TryGetValue(request.Params["id"], out PowersData? power))
                    throw new ApiException(404, $"No power '{request.Params["id"]}'.");
                if (!TrySetUnlocked(power.UnlockedCondition, value, apply: true))
                    throw new ApiException(409, "This one is unlocked by a condition the editor can't set directly.");
                return Snapshot();
            });
        });

        router.Get("/api/power-sprites/{id}", async request => new BinaryResult(await this.sprites.RenderPng(() =>
        {
            if (!DataLoader.Powers(Game1.content).TryGetValue(request.Params["id"], out PowersData? power))
                throw new ApiException(404, $"No power '{request.Params["id"]}'.");
            Texture2D texture = Game1.content.Load<Texture2D>(power.TexturePath);
            return ItemSprites.ReadPixels(texture, new Rectangle(power.TexturePosition.X, power.TexturePosition.Y, 16, 16));
        }), "image/png"));
    }

    private static object Snapshot()
    {
        Dictionary<string, List<ParsedItemData>> items = CollectionItems();
        Farmer player = Game1.player;
        Dictionary<string, string> crafting = DataLoader.CraftingRecipes(Game1.content);

        var categories = new List<object>();
        foreach (string category in Categories)
        {
            int done, total;
            if (category == "crafting")
            {
                total = crafting.Count;
                done = crafting.Keys.Count(k => player.craftingRecipes.TryGetValue(k, out int crafted) && crafted > 0);
            }
            else
            {
                List<ParsedItemData> list = items[category];
                total = list.Count;
                done = list.Count(item => IsDone(category, item));
            }
            categories.Add(new { Id = category, Done = done, Total = total });
        }

        return new
        {
            Categories = categories,
            Powers = DataLoader.Powers(Game1.content)
                .Select(pair => new
                {
                    Id = pair.Key,
                    Name = TokenParser.ParseText(pair.Value.DisplayName) ?? pair.Key,
                    Description = TokenParser.ParseText(pair.Value.Description) ?? "",
                    Unlocked = GameStateQuery.CheckConditions(pair.Value.UnlockedCondition),
                    Editable = TrySetUnlocked(pair.Value.UnlockedCondition, true, apply: false),
                })
                .ToArray(),
        };
    }

    /// <summary>The items on each collections page, classified like CollectionsPage does.</summary>
    private static Dictionary<string, List<ParsedItemData>> CollectionItems()
    {
        var result = Categories.ToDictionary(c => c, _ => new List<ParsedItemData>());
        foreach (string id in ItemRegistry.GetObjectTypeDefinition().GetAllIds())
        {
            ParsedItemData item = ItemRegistry.GetDataOrErrorItem("(O)" + id);
            string? category = item.ObjectType switch
            {
                "Arch" => "artifacts",
                "Fish" => item.RawData is ObjectData { ExcludeFromFishingCollection: true } ? null : "fish",
                "Minerals" => "minerals",
                _ when item.Category == SObject.mineralsCategory => "minerals",
                _ when item.ObjectType == "Cooking" || item.Category == SObject.CookingCategory => SkippedDishes.Contains(id) ? null : "cooking",
                _ => SObject.isPotentialBasicShipped(id, item.Category, item.ObjectType) ? "shipped" : null,
            };
            if (category != null)
                result[category].Add(item);
        }
        return result;
    }

    private static bool IsDone(string category, ParsedItemData item)
    {
        Farmer player = Game1.player;
        return category switch
        {
            "shipped" => player.basicShipped.ContainsKey(item.ItemId),
            "fish" => player.fishCaught.ContainsKey(item.QualifiedItemId),
            "artifacts" or "minerals" => LibraryMuseum.HasDonatedArtifact(item.ItemId),
            "cooking" => player.recipesCooked.ContainsKey(item.ItemId),
            _ => false,
        };
    }

    private static int Complete(string category)
    {
        Farmer player = Game1.player;
        int added = 0;

        if (category == "crafting")
        {
            foreach (string recipe in DataLoader.CraftingRecipes(Game1.content).Keys)
            {
                if (!player.craftingRecipes.TryGetValue(recipe, out int crafted) || crafted <= 0)
                {
                    player.craftingRecipes[recipe] = Math.Max(crafted, 1);
                    added++;
                }
            }
            return added;
        }

        if (category == "cooking")
        {
            // know every recipe, so the page shows them all
            foreach (string recipe in DataLoader.CookingRecipes(Game1.content).Keys)
                player.cookingRecipes.TryAdd(recipe, 0);
        }

        LibraryMuseum museum = Game1.RequireLocation<LibraryMuseum>("ArchaeologyHouse");
        foreach (ParsedItemData item in CollectionItems()[category].Where(i => !IsDone(category, i)))
        {
            switch (category)
            {
                case "shipped":
                    player.basicShipped[item.ItemId] = 1;
                    break;
                case "fish":
                    player.fishCaught[item.QualifiedItemId] = new[] { 1, 0 };
                    break;
                case "cooking":
                    player.recipesCooked[item.ItemId] = 1;
                    break;
                case "artifacts":
                case "minerals":
                    if (category == "artifacts")
                        player.archaeologyFound.TryAdd(item.ItemId, new[] { 1, 0 });
                    else
                        player.mineralsFound.TryAdd(item.ItemId, 1);
                    // the page counts donations, so place it in the museum
                    Vector2 spot = museum.getFreeDonationSpot();
                    if (!museum.isTileSuitableForMuseumPiece((int)spot.X, (int)spot.Y))
                        throw new ApiException(409, $"The museum is full after {added} donations.");
                    museum.museumPieces.Add(spot, item.ItemId);
                    break;
            }
            added++;
        }
        return added;
    }

    /// <summary>
    /// Set what an unlock condition checks, for the simple ones the wallet uses: received mail and stats (e.g. power books read).
    /// With <paramref name="apply"/> false, only report whether every part of the condition is settable.
    /// </summary>
    private static bool TrySetUnlocked(string? condition, bool value, bool apply)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return false;
        string[] queries = condition.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        foreach (string query in queries)
        {
            string[] args = ArgUtility.SplitBySpaceQuoteAware(query);
            bool settable = args.Length >= 3 && (args[0], args[1]) switch
            {
                ("PLAYER_HAS_MAIL", "Current" or "Host" or "Any" or "All") => true,
                ("PLAYER_STAT", "Current" or "Host" or "Any" or "All") => args.Length >= 4 && uint.TryParse(args[3], out _),
                _ => false,
            };
            if (!settable)
                return false;
            if (!apply)
                continue;

            if (args[0] == "PLAYER_HAS_MAIL")
            {
                if (value)
                    Game1.player.mailReceived.Add(args[2]);
                else
                    Game1.player.mailReceived.Remove(args[2]);
            }
            else
                Game1.player.stats.Set(args[2], value ? uint.Parse(args[3]) : 0u);
        }
        return true;
    }
}
