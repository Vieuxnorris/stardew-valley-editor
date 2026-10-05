using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.GameData.Characters;
using ValleyEditor.Server;
using ValleyEditor.Sprites;

namespace ValleyEditor.Domains;

/// <summary>Friendships with villagers: hearts, gifts, dating, plus portraits.</summary>
internal sealed class NpcsDomain : Domain
{
    private const int PointsPerHeart = NPC.friendshipPointsPerHeartLevel;
    private const int MaxGiftsPerWeek = 2;

    private readonly ItemSprites sprites;

    public NpcsDomain(GameThreadDispatcher game, EditorState state, ItemSprites sprites)
        : base(game, state)
    {
        this.sprites = sprites;
    }

    public override void Register(Router router)
    {
        router.Get("/api/npcs", _ => this.Read(Snapshot));

        router.Patch("/api/npcs/{name}", request =>
        {
            string name = request.Params["name"];
            JObject body = request.BodyObject;
            int? points = OptInt(body, "points", 0, 14 * PointsPerHeart + PointsPerHeart - 1);
            int? hearts = OptInt(body, "hearts", 0, 14);
            int? giftsThisWeek = OptInt(body, "giftsThisWeek", 0, MaxGiftsPerWeek);
            int? giftsToday = OptInt(body, "giftsToday", 0, 1);
            bool? talkedToday = body["talkedToToday"]?.Type == JTokenType.Boolean ? body.Value<bool>("talkedToToday") : null;
            bool? dating = body["dating"]?.Type == JTokenType.Boolean ? body.Value<bool>("dating") : null;

            return this.Write(() =>
            {
                NPC npc = FindVillager(name);
                Friendship friendship = GetOrCreateFriendship(npc);

                // dating first, since it raises the heart cap of marriage candidates from 8 to 10
                if (dating.HasValue)
                    SetDating(npc, friendship, dating.Value);
                if (hearts.HasValue)
                    friendship.Points = Math.Min(hearts.Value * PointsPerHeart, MaxPoints(npc));
                if (points.HasValue)
                    friendship.Points = Math.Min(points.Value, MaxPoints(npc));
                if (giftsThisWeek.HasValue)
                    friendship.GiftsThisWeek = giftsThisWeek.Value;
                if (giftsToday.HasValue)
                    friendship.GiftsToday = giftsToday.Value;
                if (talkedToday.HasValue)
                    friendship.TalkedToToday = talkedToday.Value;
                return Snapshot();
            });
        });

        // the same change for every villager: max hearts, reset gifts, or mark everyone as talked to today
        router.Post("/api/npcs/bulk", request =>
        {
            string action = request.BodyObject.Value<string>("action") ?? throw new ApiException(400, "'action' is required.");
            Action<NPC, Friendship> apply = action switch
            {
                "maxHearts" => (npc, f) => f.Points = MaxPoints(npc),
                "resetGifts" => (_, f) =>
                {
                    f.GiftsThisWeek = 0;
                    f.GiftsToday = 0;
                },
                "talkToAll" => (_, f) => f.TalkedToToday = true,
                _ => throw new ApiException(400, "'action' must be maxHearts, resetGifts or talkToAll."),
            };

            return this.Write(() =>
            {
                foreach (NPC npc in Villagers())
                    apply(npc, GetOrCreateFriendship(npc));
                return Snapshot();
            });
        });

        router.Get("/api/portraits/{name}", async request => new BinaryResult(await this.sprites.GetPortraitPng(request.Params["name"]), "image/png", CacheSeconds: 3600));
    }

    /// <summary>Villagers the player can befriend, as shown on the social tab.</summary>
    private static List<NPC> Villagers()
    {
        var villagers = new List<NPC>();
        Utility.ForEachVillager(npc =>
        {
            if (npc.CanSocialize && !villagers.Any(v => v.Name == npc.Name))
                villagers.Add(npc);
            return true;
        });
        return villagers;
    }

    private static NPC FindVillager(string name)
    {
        return Villagers().FirstOrDefault(npc => npc.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ApiException(404, $"No villager named '{name}' that can be befriended.");
    }

    /// <summary>Villagers the player hasn't met yet have no friendship entry; create it like the game does on first meeting.</summary>
    private static Friendship GetOrCreateFriendship(NPC npc)
    {
        if (!Game1.player.friendshipData.TryGetValue(npc.Name, out Friendship friendship))
        {
            friendship = new Friendship();
            Game1.player.friendshipData.Add(npc.Name, friendship);
        }
        return friendship;
    }

    /// <summary>The cap the game itself applies in Farmer.changeFriendship.</summary>
    private static int MaxPoints(NPC npc) => (Utility.GetMaximumHeartsForCharacter(npc) + 1) * PointsPerHeart - 1;

    private static void SetDating(NPC npc, Friendship friendship, bool dating)
    {
        if (friendship.Status is not (FriendshipStatus.Friendly or FriendshipStatus.Dating))
            throw new ApiException(409, $"{npc.displayName} is engaged, married or divorced; the editor doesn't change that yet.");
        if (dating && !npc.datable.Value)
            throw new ApiException(409, $"{npc.displayName} can't be dated.");

        friendship.Status = dating ? FriendshipStatus.Dating : FriendshipStatus.Friendly;
        friendship.Points = Math.Min(friendship.Points, MaxPoints(npc));
    }

    private static object Snapshot()
    {
        return Villagers()
            .Select(npc =>
            {
                Game1.player.friendshipData.TryGetValue(npc.Name, out Friendship? friendship);
                CharacterData? data = npc.GetData();
                int points = friendship?.Points ?? 0;
                return new
                {
                    npc.Name,
                    DisplayName = npc.displayName,
                    Met = friendship != null,
                    Points = points,
                    Hearts = points / PointsPerHeart,
                    MaxHearts = Utility.GetMaximumHeartsForCharacter(npc),
                    MaxPoints = MaxPoints(npc),
                    Datable = npc.datable.Value,
                    Status = (friendship?.Status ?? FriendshipStatus.Friendly).ToString(),
                    GiftsThisWeek = friendship?.GiftsThisWeek ?? 0,
                    GiftsToday = friendship?.GiftsToday ?? 0,
                    TalkedToToday = friendship?.TalkedToToday ?? false,
                    BirthSeason = data?.BirthSeason?.ToString().ToLowerInvariant(),
                    BirthDay = data?.BirthDay,
                    Location = npc.currentLocation?.DisplayName ?? npc.currentLocation?.Name,
                };
            })
            .OrderBy(n => n.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }
}
