using MediaBrowser.Model.Plugins;

namespace NapisyAi.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string Provider { get; set; } = "gemini";
        public string TargetLanguage { get; set; } = "pl";
        public string GeminiApiKey { get; set; } = "";
        public string GeminiModel { get; set; } = "gemini-3.5-flash-lite";
        public string AnthropicApiKey { get; set; } = "";
        public string AnthropicModel { get; set; } = "claude-opus-5-5";
        public string OpenAiApiKey { get; set; } = "";
        public string OpenAiBaseUrl { get; set; } = "";
        public string OpenAiModel { get; set; } = "";
        public int RequestsPerMinute { get; set; } = 10;
        public int BatchSize { get; set; } = 60;
        public int ContextSize { get; set; } = 15;
    }
}
