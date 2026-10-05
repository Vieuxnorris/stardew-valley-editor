using System;
using System.Linq;
using Netcode;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Network;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>Unlocks and long-term progress: special items, mines, Community Center, museum, raw mail/event flags.</summary>
internal sealed class ProgressionDomain : Domain
{
    /// <summary>Mail flags behind the wallet's special items (Farmer.hasRustyKey etc.) and other one-off unlocks.</summary>
    private static readonly string[] Unlocks =
    {
        "HasRustyKey", "HasSkullKey", "HasClubCard", "HasDarkTalisman", "HasMagicInk", "HasMagnifyingGlass",
        "HasSpecialCharm", "HasTownKey", "HasDwarvishTranslationGuide", "HasUnlockedSkullDoor", "willyBoatFixed",
    };

    /// <summary>Community Center rooms with bundles (CommunityCenter.AREA_*).</summary>
    private const int CommunityCenterAreas = 6;

    private const int MinesBottom = 120;

    public ProgressionDomain(GameThreadDispatcher game, EditorState state)
        : base(game, state) { }

    public override void Register(Router router)
    {
        router.Get("/api/progression", _ => this.Read(Snapshot));

        router.Put("/api/progression/unlocks/{id}", request =>
        {
            string id = Unlocks.FirstOrDefault(u => u.Equals(request.Params["id"], StringComparison.OrdinalIgnoreCase))
                ?? throw new ApiException(404, $"Unknown unlock '{request.Params["id"]}'.");
            bool value = RequireBool(request.BodyObject, "value");
            return this.Write(() =>
            {
                SetMail(id, value);
                return Snapshot();
            });
        });

        router.Patch("/api/progression/mines", request =>
        {
            JObject body = request.BodyObject;
            int? mines = OptInt(body, "minesLevel", 0, MinesBottom);
            int? skull = OptInt(body, "skullCavernLevel", 0, 10_000);
            return this.Write(() =>
            {
                Farmer player = Game1.player;
                if (mines.HasValue)
                {
                    // the elevator reads the world's lowest level; the player's deepest level drives other checks
                    Game1.netWorldState.Value.LowestMineLevel = mines.Value;
                    if (player.deepestMineLevel <= MinesBottom)
                        player.deepestMineLevel = mines.Value;
                }
                if (skull.HasValue)
                    player.deepestMineLevel = skull.Value > 0 ? MinesBottom + skull.Value : Math.Min(player.deepestMineLevel, MinesBottom);
                return Snapshot();
            });
        });

        router.Post("/api/progression/community-center/{area}", request =>
        {
            int area = int.TryParse(request.Params["area"], out int parsed) && parsed is >= 0 and < CommunityCenterAreas
                ? parsed
                : throw new ApiException(400, $"The area must be between 0 and {CommunityCenterAreas - 1}.");
            return this.Write(() =>
            {
                CompleteArea(area);
                return Snapshot();
            });
        });

        router.Post("/api/progression/museum/donate-all", _ => this.Write(() =>
        {
            int added = DonateAll();
            return new { Added = added, Progress = Snapshot() };
        }));

        router.Get("/api/progression/flags/{kind}", request =>
        {
            string kind = request.Params["kind"];
            return this.Read(() => Flags(kind).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray());
        });

        router.Put("/api/progression/flags/{kind}", request =>
        {
            string kind = request.Params["kind"];
            JObject body = request.BodyObject;
            string id = body.Value<string>("id")?.Trim() is { Length: > 0 } trimmed ? trimmed : throw new ApiException(400, "'id' is required.");
            bool value = RequireBool(body, "value");
            return this.Write(() =>
            {
                NetStringHashSet flags = Flags(kind);
                if (value)
                    flags.Add(id);
                else
                {
                    flags.Remove(id);
                    if (kind == "events")
                        Game1.eventsSeenSinceLastLocationChange.Remove(id);
                }
                return flags.OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            });
        });
    }

    private static object Snapshot()
    {
        Farmer player = Game1.player;
        CommunityCenter cc = Game1.RequireLocation<CommunityCenter>("CommunityCenter");
        LibraryMuseum museum = Game1.RequireLocation<LibraryMuseum>("ArchaeologyHouse");

        return new
        {
            Unlocks = Unlocks.Select(id => new { Id = id, Value = Game1.MasterPlayer.mailReceived.Contains(id) }),
            Mines = new
            {
                MinesLevel = Math.Min(Game1.netWorldState.Value.LowestMineLevel, MinesBottom),
                SkullCavernLevel = Math.Max(0, player.deepestMineLevel - MinesBottom),
            },
            CommunityCenter = Enumerable.Range(0, CommunityCenterAreas).Select(i => new
            {
                Area = i,
                Name = CommunityCenter.getAreaDisplayNameFromNumber(i),
                Complete = cc.areasComplete[i] || player.mailReceived.Contains(AreaMail(i)),
            }),
            Museum = new
            {
                Donated = museum.museumPieces.Length,
                Donatable = ItemRegistry.GetObjectTypeDefinition().GetAllIds().Count(id => LibraryMuseum.IsItemSuitableForDonation("(O)" + id, checkDonatedItems: false)),
            },
        };
    }

    private static string AreaMail(int area) => "cc" + CommunityCenter.getAreaNameFromNumber(area);

    /// <summary>Complete a room like the game's debug command does, plus its bundles and their rewards.</summary>
    private static void CompleteArea(int area)
    {
        string areaName = CommunityCenter.getAreaNameFromNumber(area);
        NetWorldState world = Game1.netWorldState.Value;

        foreach (string key in world.BundleData.Keys)
        {
            string[] parts = key.Split('/');
            if (parts[0] != areaName || !int.TryParse(parts[1], out int bundle) || !world.Bundles.ContainsKey(bundle))
                continue;
            NetArray<bool, NetBool> slots = world.Bundles.FieldDict[bundle];
            for (int i = 0; i < slots.Count; i++)
                slots[i] = true;
            world.BundleRewards[bundle] = true; // reward waits at the room's plaque
        }

        SetMail(AreaMail(area), true);
        CommunityCenter cc = Game1.RequireLocation<CommunityCenter>("CommunityCenter");
        cc.areasComplete[area] = true;
    }

    /// <summary>Place every donatable item not yet in the museum on a free display tile.</summary>
    private static int DonateAll()
    {
        LibraryMuseum museum = Game1.RequireLocation<LibraryMuseum>("ArchaeologyHouse");
        int added = 0;
        foreach (string id in ItemRegistry.GetObjectTypeDefinition().GetAllIds())
        {
            string qualifiedId = "(O)" + id;
            if (!LibraryMuseum.IsItemSuitableForDonation(qualifiedId))
                continue;

            var spot = museum.getFreeDonationSpot();
            if (!museum.isTileSuitableForMuseumPiece((int)spot.X, (int)spot.Y))
                break; // the museum is full
            museum.museumPieces.Add(spot, id);
            added++;
        }
        return added;
    }

    private static NetStringHashSet Flags(string kind) => kind switch
    {
        "mail" => Game1.player.mailReceived,
        "events" => Game1.player.eventsSeen,
        _ => throw new ApiException(404, "Flag kind must be 'mail' or 'events'."),
    };

    private static void SetMail(string id, bool value)
    {
        if (value)
            Game1.player.mailReceived.Add(id);
        else
            Game1.player.mailReceived.Remove(id);
    }

    private static bool RequireBool(JObject body, string name)
    {
        return body[name]?.Type == JTokenType.Boolean ? body.Value<bool>(name) : throw new ApiException(400, $"'{name}' must be true or false.");
    }
}
