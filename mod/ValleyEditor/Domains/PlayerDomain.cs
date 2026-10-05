using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Netcode;
using Newtonsoft.Json.Linq;
using StardewValley;
using StardewValley.Menus;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>The main player's stats, skills and professions.</summary>
internal sealed class PlayerDomain : Domain
{
    private const int MaxSkillLevel = 10;
    private const int ProfessionCount = 30;
    private static readonly string[] SkillKeys = { "farming", "fishing", "foraging", "mining", "combat" };

    public PlayerDomain(GameThreadDispatcher game, EditorState state)
        : base(game, state) { }

    public override void Register(Router router)
    {
        router.Get("/api/player", _ => this.Read(Snapshot));

        router.Patch("/api/player", request =>
        {
            JObject body = request.BodyObject;
            int? money = OptInt(body, "money", 0, int.MaxValue);
            int? qiGems = OptInt(body, "qiGems", 0, int.MaxValue);
            int? walnuts = OptInt(body, "goldenWalnuts", 0, int.MaxValue);
            int? maxHealth = OptInt(body, "maxHealth", 1, 100_000);
            int? health = OptInt(body, "health", 0, 100_000);
            int? maxStamina = OptInt(body, "maxStamina", 1, 100_000);
            int? stamina = OptInt(body, "stamina", 0, 100_000);
            int? masteryExp = OptInt(body, "masteryExp", 0, int.MaxValue);

            return this.Write(() =>
            {
                Farmer player = Game1.player;
                RememberPlayer(player);
                if (money.HasValue)
                    player.Money = money.Value;
                if (qiGems.HasValue)
                    player.QiGems = qiGems.Value;
                if (walnuts.HasValue)
                    Game1.netWorldState.Value.GoldenWalnuts = walnuts.Value;

                // max before current, since current values are clamped to the max
                if (maxHealth.HasValue)
                    player.maxHealth = maxHealth.Value;
                if (health.HasValue)
                    player.health = Math.Min(health.Value, player.maxHealth);
                if (maxStamina.HasValue)
                    player.maxStamina.Value = maxStamina.Value;
                if (stamina.HasValue)
                    player.Stamina = stamina.Value;

                if (masteryExp.HasValue)
                    Game1.stats.Set("MasteryExp", masteryExp.Value);
                return Snapshot();
            });
        });

        router.Put("/api/player/skills/{skill}", request =>
        {
            int skill = Array.IndexOf(SkillKeys, request.Params["skill"].ToLowerInvariant());
            if (skill < 0)
                throw new ApiException(404, $"Unknown skill '{request.Params["skill"]}'. Expected one of: {string.Join(", ", SkillKeys)}.");

            JObject body = request.BodyObject;
            int? level = OptInt(body, "level", 0, MaxSkillLevel);
            int? xp = OptInt(body, "xp", 0, int.MaxValue);
            if (level.HasValue == xp.HasValue)
                throw new ApiException(400, "Send exactly one of 'level' or 'xp'.");

            return this.Write(() =>
            {
                Farmer player = Game1.player;
                RememberPlayer(player);
                if (level.HasValue)
                    SetLevel(player, skill, level.Value);
                else
                    SetExperience(player, skill, xp!.Value);
                return Snapshot();
            });
        });

        router.Put("/api/player/professions", request =>
        {
            JArray ids = request.BodyObject["ids"] as JArray ?? throw new ApiException(400, "'ids' must be an array of profession IDs.");
            int[] professions = ids.Select(id => id.Type == JTokenType.Integer ? id.Value<int>() : -1).Distinct().ToArray();
            if (professions.Any(id => id is < 0 or >= ProfessionCount))
                throw new ApiException(400, $"Profession IDs must be integers between 0 and {ProfessionCount - 1}.");

            return this.Write(() =>
            {
                Farmer player = Game1.player;
                RememberPlayer(player);
                player.professions.Clear();
                foreach (int id in professions)
                    player.professions.Add(id);
                LevelUpMenu.RevalidateHealth(player); // Fighter and Defender add max health
                return Snapshot();
            });
        });
    }

    /// <summary>Let undo put back everything this domain edits: money, stats, skills and professions.</summary>
    private static void RememberPlayer(Farmer player)
    {
        int money = player.Money, qiGems = player.QiGems, walnuts = Game1.netWorldState.Value.GoldenWalnuts;
        int maxHealth = player.maxHealth, health = player.health, maxStamina = player.maxStamina.Value;
        float stamina = player.Stamina;
        uint mastery = Game1.stats.Get("MasteryExp");
        int[] levels = Enumerable.Range(0, SkillKeys.Length).Select(i => SkillLevel(player, i).Value).ToArray();
        int[] experience = player.experiencePoints.ToArray();
        int[] professions = player.professions.ToArray();
        int queuedLevels = player.newLevels.Count;

        UndoCapture.Remember(() =>
        {
            player.Money = money;
            player.QiGems = qiGems;
            Game1.netWorldState.Value.GoldenWalnuts = walnuts;
            player.maxHealth = maxHealth;
            player.health = health;
            player.maxStamina.Value = maxStamina;
            player.Stamina = stamina;
            Game1.stats.Set("MasteryExp", mastery);
            for (int i = 0; i < levels.Length; i++)
                SkillLevel(player, i).Value = levels[i];
            for (int i = 0; i < experience.Length; i++)
                player.experiencePoints[i] = experience[i];
            player.professions.Clear();
            foreach (int id in professions)
                player.professions.Add(id);
            while (player.newLevels.Count > queuedLevels)
                player.newLevels.RemoveAt(player.newLevels.Count - 1); // level-ups queued by the change
        });
    }

    /// <summary>Raise or lower a skill level. Gained levels are queued like in vanilla, so the level-up screens (recipes, profession choice) appear when the player sleeps.</summary>
    private static void SetLevel(Farmer player, int skill, int level)
    {
        NetInt field = SkillLevel(player, skill);
        int current = field.Value;

        if (level > current)
        {
            for (int i = current + 1; i <= level; i++)
            {
                var pending = new Point(skill, i);
                if (!player.newLevels.Contains(pending))
                    player.newLevels.Add(pending);
            }
            player.experiencePoints[skill] = Math.Max(player.experiencePoints[skill], Farmer.getBaseExperienceForLevel(level));
        }
        else if (level < current)
        {
            player.experiencePoints[skill] = level == 0 ? 0 : Farmer.getBaseExperienceForLevel(level);
            DropAbove(player, skill, level);
        }

        field.Value = level;
        if (skill == Farmer.combatSkill)
            LevelUpMenu.RevalidateHealth(player);
    }

    /// <summary>Set a skill's experience directly; the level follows it without level-up screens.</summary>
    private static void SetExperience(Farmer player, int skill, int xp)
    {
        int level = 0;
        while (level < MaxSkillLevel && xp >= Farmer.getBaseExperienceForLevel(level + 1))
            level++;

        player.experiencePoints[skill] = xp;
        SkillLevel(player, skill).Value = level;
        DropAbove(player, skill, level);
        if (skill == Farmer.combatSkill)
            LevelUpMenu.RevalidateHealth(player);
    }

    /// <summary>Remove pending level-ups and professions that a skill level no longer reaches.</summary>
    private static void DropAbove(Farmer player, int skill, int level)
    {
        foreach (Point pending in player.newLevels.Where(p => p.X == skill && p.Y > level).ToArray())
            player.newLevels.Remove(pending);
        foreach (int id in player.professions.Where(id => id / 6 == skill && ProfessionTier(id) > level).ToArray())
            player.professions.Remove(id);
    }

    private static NetInt SkillLevel(Farmer player, int skill) => skill switch
    {
        Farmer.farmingSkill => player.farmingLevel,
        Farmer.fishingSkill => player.fishingLevel,
        Farmer.foragingSkill => player.foragingLevel,
        Farmer.miningSkill => player.miningLevel,
        Farmer.combatSkill => player.combatLevel,
        _ => throw new ArgumentOutOfRangeException(nameof(skill)),
    };

    /// <summary>The skill level a profession is chosen at: the first two of each skill at 5, the other four at 10.</summary>
    private static int ProfessionTier(int id) => id % 6 < 2 ? 5 : 10;

    private static object Snapshot()
    {
        Farmer player = Game1.player;
        return new
        {
            player.Name,
            FarmName = player.farmName.Value,
            player.Money,
            player.QiGems,
            GoldenWalnuts = Game1.netWorldState.Value.GoldenWalnuts,
            Health = player.health,
            MaxHealth = player.maxHealth,
            Stamina = (int)player.Stamina,
            MaxStamina = player.maxStamina.Value,
            MasteryExp = (int)Game1.stats.Get("MasteryExp"),
            MasteryLevel = MasteryTrackerMenu.getCurrentMasteryLevel(),
            Skills = SkillKeys.Select((key, i) => new
            {
                Key = key,
                Name = Farmer.getSkillDisplayNameFromIndex(i),
                Level = SkillLevel(player, i).Value,
                Xp = player.experiencePoints[i],
                NextLevelXp = SkillLevel(player, i).Value < MaxSkillLevel ? Farmer.getBaseExperienceForLevel(SkillLevel(player, i).Value + 1) : (int?)null,
                PendingLevelUps = player.newLevels.Count(p => p.X == i),
            }),
            Professions = player.professions.ToArray(),
            ProfessionCatalog = Enumerable.Range(0, ProfessionCount).Select(id => new
            {
                Id = id,
                Skill = SkillKeys[id / 6],
                Tier = ProfessionTier(id),
                // level-10 professions 2-3 follow the first level-5 choice, 4-5 the second
                Parent = ProfessionTier(id) == 10 ? (id / 6) * 6 + (id % 6 - 2) / 2 : (int?)null,
                Name = LevelUpMenu.getProfessionTitleFromNumber(id),
            }),
        };
    }
}
