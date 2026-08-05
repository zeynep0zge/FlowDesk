using FlowDesk.Ai.Models;
using FlowDesk.Common;

namespace FlowDesk.Ai.Interfaces;

public interface IUnresolvedTermResearchService
{
    Task<ServiceResult<UnresolvedTermResearchResult>> ResearchAsync(
        IReadOnlyCollection<string> unresolvedTerms,
        CancellationToken cancellationToken = default);
}