namespace KitLib.AI.Knowledge;

public interface ICardTagProvider {
    bool AppliesTo(string? cardId);
    IReadOnlyList<AiTag> GetExtraTags(string cardId);
}
