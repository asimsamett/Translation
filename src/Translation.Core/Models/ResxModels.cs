namespace Translation.Core.Models;

public sealed record ResxFileSummary(
    string RelativePath,
    string FileName,
    string? Culture,
    int EntryCount,
    DateTime LastModifiedUtc);

public sealed record ResxEntryDto(string Name, string Value, string? Comment);

public sealed record ResxFileDetail(string RelativePath, IReadOnlyList<ResxEntryDto> Entries);

public sealed record ResxUpdateRequest(IReadOnlyList<ResxEntryDto> Entries);

public sealed record ResxUpdateResult(string RelativePath, int UpdatedCount);
