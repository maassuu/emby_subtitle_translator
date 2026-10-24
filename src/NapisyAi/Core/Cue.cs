namespace NapisyAi.Core
{
    /// <summary>One subtitle: times in milliseconds, text with "\n" between lines and SRT tags kept.</summary>
    public sealed record Cue(long StartMs, long EndMs, string Text);
}
