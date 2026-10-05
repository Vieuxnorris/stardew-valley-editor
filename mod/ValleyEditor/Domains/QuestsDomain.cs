using System;
using System.Linq;
using StardewValley;
using StardewValley.Quests;
using ValleyEditor.Server;

namespace ValleyEditor.Domains;

/// <summary>The quest log: list, complete, remove, and add any quest from Data/Quests.</summary>
internal sealed class QuestsDomain : Domain
{
    public QuestsDomain(GameThreadDispatcher game, EditorState state)
        : base(game, state) { }

    public override void Register(Router router)
    {
        router.Get("/api/quests", _ => this.Read(Snapshot));

        // quests that can be added; Data/Quests values are 'type/title/description/...'
        router.Get("/api/quests/catalog", _ => this.Read(() => DataLoader.Quests(Game1.content)
            .Select(pair => new { Id = pair.Key, Title = pair.Value.Split('/').ElementAtOrDefault(1) ?? pair.Key })
            .OrderBy(q => q.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray()));

        router.Post("/api/quests", request =>
        {
            string id = request.BodyObject.Value<string>("id") ?? throw new ApiException(400, "'id' is required.");
            return this.Write(() =>
            {
                if (!DataLoader.Quests(Game1.content).ContainsKey(id))
                    throw new ApiException(404, $"No quest with ID '{id}'.");
                if (Game1.player.hasQuest(id))
                    throw new ApiException(409, "The player already has this quest.");
                Game1.player.addQuest(id);
                return Snapshot();
            });
        });

        router.Post("/api/quests/{index}/complete", request =>
        {
            int index = ParseIndex(request);
            return this.Write(() =>
            {
                Quest quest = Find(index);
                if (!quest.completed.Value)
                    quest.questComplete();
                return Snapshot();
            });
        });

        router.Delete("/api/quests/{index}", request =>
        {
            int index = ParseIndex(request);
            return this.Write(() =>
            {
                Game1.player.questLog.Remove(Find(index));
                return Snapshot();
            });
        });
    }

    private static object Snapshot()
    {
        return Game1.player.questLog.Select((quest, i) => new
        {
            Index = i,
            Id = quest.id.Value,
            Name = quest.GetName(),
            Completed = quest.completed.Value,
            DaysLeft = quest.daysLeft.Value > 0 ? quest.daysLeft.Value : (int?)null,
        }).ToArray();
    }

    /// <summary>Quests are addressed by position: generated ones (deliveries, slaying...) have no ID.</summary>
    private static Quest Find(int index)
    {
        return index < Game1.player.questLog.Count ? Game1.player.questLog[index] : throw new ApiException(404, $"No quest at position {index}.");
    }

    private static int ParseIndex(ApiRequest request)
    {
        return int.TryParse(request.Params["index"], out int index) && index >= 0 ? index : throw new ApiException(400, "Invalid quest position.");
    }
}
