using FlowDesk.Ai.Models;
using FlowDesk.Common;

namespace FlowDesk.Ai.Interfaces;

public interface IAnalystRequestRewriteService
{
    Task<ServiceResult<AnalystRequestRewriteResult>> RewriteAsync(
        string originalRequestDescription,
        CancellationToken cancellationToken = default);
}