namespace KitLib.AI.Core.Schema;

public sealed record ActionResult {
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static ActionResult Ok(string? message = null) => new() { Success = true, Message = message };
    public static ActionResult Fail(string message) => new() { Success = false, Message = message };
}
