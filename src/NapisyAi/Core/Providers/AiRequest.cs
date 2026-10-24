namespace NapisyAi.Core.Providers
{
    /// <summary>One request to the model: system instruction, user message and the JSON schema of the answer.</summary>
    public sealed record AiRequest(string System, string User, string JsonSchema);
}
