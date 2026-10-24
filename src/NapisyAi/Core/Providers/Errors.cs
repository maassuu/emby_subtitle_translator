using System;

namespace NapisyAi.Core.Providers
{
    /// <summary>The provider cannot handle this job (wrong model, no credit, refusal).</summary>
    public class ProviderException : Exception
    {
        public ProviderException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }

    /// <summary>The API key is missing, wrong or has no access.</summary>
    public sealed class ProviderAuthException : ProviderException
    {
        public ProviderAuthException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }

    /// <summary>The account has no credit left; nothing will work until it is topped up.</summary>
    public sealed class BillingException : ProviderException
    {
        public BillingException(string message, Exception? inner = null) : base(message, inner)
        {
        }
    }

    /// <summary>Per-minute limit or temporary outage; the same request may be retried.</summary>
    public sealed class TransientException : Exception
    {
        public TransientException(TimeSpan? retryAfter, string message) : base(message)
        {
            RetryAfter = retryAfter;
        }

        public TimeSpan? RetryAfter { get; }
    }

    /// <summary>The daily quota is used up.</summary>
    public sealed class DailyQuotaException : Exception
    {
        public DailyQuotaException(string message) : base(message)
        {
        }
    }
}
