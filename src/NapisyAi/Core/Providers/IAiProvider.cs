using System.Threading;
using System.Threading.Tasks;

namespace NapisyAi.Core.Providers
{
    public interface IAiProvider
    {
        /// <summary>Sends one request and returns the model's raw text.</summary>
        Task<string> CompleteAsync(AiRequest request, CancellationToken cancellationToken);
    }
}
