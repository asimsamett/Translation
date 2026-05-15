using Translation.Core.Models;

namespace Translation.Core.Services;

public interface IResxService
{
    Task<IReadOnlyList<ResxFileSummary>> ListResxFilesAsync(CancellationToken cancellationToken = default);
    Task<ResxFileDetail> GetFileAsync(string relativePath, CancellationToken cancellationToken = default);
    Task<ResxUpdateResult> UpdateFileAsync(string relativePath, ResxUpdateRequest request, CancellationToken cancellationToken = default);
}
