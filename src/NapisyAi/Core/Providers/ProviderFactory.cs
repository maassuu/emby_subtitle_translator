using System.Net.Http;
using NapisyAi.Configuration;

namespace NapisyAi.Core.Providers
{
    public static class ProviderFactory
    {
        private const string NoKey = "Brak klucza API dla wybranego dostawcy";

        public static IAiProvider Create(PluginConfiguration c, HttpClient http)
        {
            switch (c.Provider)
            {
                case "anthropic":
                    Require(c.AnthropicApiKey);
                    return new AnthropicProvider(http, c.AnthropicApiKey, c.AnthropicModel);
                case "openai":
                    if (string.IsNullOrWhiteSpace(c.OpenAiApiKey) && string.IsNullOrWhiteSpace(c.OpenAiBaseUrl))
                    {
                        throw new ProviderException(NoKey);
                    }

                    if (string.IsNullOrWhiteSpace(c.OpenAiModel))
                    {
                        throw new ProviderException("Podaj nazwę modelu");
                    }

                    return new OpenAiCompatProvider(http, c.OpenAiApiKey, c.OpenAiBaseUrl, c.OpenAiModel);
                default:
                    Require(c.GeminiApiKey);
                    return new GeminiProvider(http, c.GeminiApiKey, c.GeminiModel);
            }
        }

        private static void Require(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ProviderException(NoKey);
            }
        }
    }
}
