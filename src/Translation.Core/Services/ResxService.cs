using System.Xml.Linq;
using Microsoft.Extensions.Options;
using Translation.Core.Models;
using Translation.Core.Options;

namespace Translation.Core.Services;

public sealed class ResxService : IResxService
{
    private readonly WorkspaceOptions _workspace;
    private readonly IGitWorkspaceService _git;

    public ResxService(IOptions<WorkspaceOptions> workspace, IGitWorkspaceService git)
    {
        _workspace = workspace.Value;
        _git = git;
    }

    public async Task<IReadOnlyList<ResxFileSummary>> ListResxFilesAsync(CancellationToken cancellationToken = default)
    {
        var root = await _git.EnsureRepositoryAsync(cancellationToken);
        if (!Directory.Exists(root))
            return Array.Empty<ResxFileSummary>();

        var pattern = string.IsNullOrWhiteSpace(_workspace.ResxSearchPattern)
            ? "*.resx"
            : _workspace.ResxSearchPattern;

        var files = Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories)
            .Where(path => !IsExcludedResx(path))
            .Select(path => ToRelative(root, path))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var summaries = new List<ResxFileSummary>(files.Count);
        foreach (var relative in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fileName = Path.GetFileName(relative);
            var fullPath = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            var entries = ReadEntries(fullPath);
            summaries.Add(new ResxFileSummary(
                relative,
                fileName,
                ResxCultureHelper.TryGetCulture(fileName),
                entries.Count,
                File.GetLastWriteTimeUtc(fullPath)));
        }

        return summaries;
    }

    private static bool IsExcludedResx(string fullPath)
    {
        var name = Path.GetFileName(fullPath);
        return name.EndsWith(".Designer.resx", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<ResxFileDetail> GetFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var fullPath = await ResolveExistingPathAsync(relativePath, cancellationToken);
        var entries = ReadEntries(fullPath)
            .Select(e => new ResxEntryDto(e.Name, e.Value, e.Comment))
            .ToList();
        return new ResxFileDetail(NormalizeRelative(relativePath), entries);
    }

    public async Task<ResxUpdateResult> UpdateFileAsync(
        string relativePath,
        ResxUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var fullPath = await ResolveExistingPathAsync(relativePath, cancellationToken);
        var doc = XDocument.Load(fullPath);
        var root = doc.Root ?? throw new InvalidOperationException("RESX root element missing.");
        var dataElements = root.Elements("data").ToDictionary(e => (string)e.Attribute("name")!, e => e);

        var updated = 0;
        foreach (var entry in request.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!dataElements.TryGetValue(entry.Name, out var dataElement))
            {
                dataElement = new XElement("data",
                    new XAttribute("name", entry.Name),
                    new XAttribute(XNamespace.Xml + "space", "preserve"),
                    new XElement("value", entry.Value));
                if (!string.IsNullOrWhiteSpace(entry.Comment))
                    dataElement.Add(new XElement("comment", entry.Comment));
                root.Add(dataElement);
                dataElements[entry.Name] = dataElement;
                updated++;
                continue;
            }

            var valueElement = dataElement.Element("value");
            if (valueElement is null)
            {
                dataElement.Add(new XElement("value", entry.Value));
                updated++;
            }
            else if (!string.Equals(valueElement.Value, entry.Value, StringComparison.Ordinal))
            {
                valueElement.SetValue(entry.Value);
                updated++;
            }

            if (entry.Comment is null)
                continue;

            var commentElement = dataElement.Element("comment");
            if (commentElement is null)
            {
                dataElement.Add(new XElement("comment", entry.Comment));
                updated++;
            }
            else if (!string.Equals(commentElement.Value, entry.Comment, StringComparison.Ordinal))
            {
                commentElement.SetValue(entry.Comment);
                updated++;
            }
        }

        doc.Save(fullPath);
        return new ResxUpdateResult(NormalizeRelative(relativePath), updated);
    }

    private async Task<string> ResolveExistingPathAsync(string relativePath, CancellationToken cancellationToken)
    {
        var root = await _git.EnsureRepositoryAsync(cancellationToken);
        var normalized = NormalizeRelative(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalized));
        if (!fullPath.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid RESX path.");
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"RESX not found: {normalized}", fullPath);
        return fullPath;
    }

    private static string NormalizeRelative(string relativePath) =>
        relativePath.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);

    private static string ToRelative(string root, string fullPath)
    {
        var relative = Path.GetRelativePath(root, fullPath);
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    private static List<ResxEntry> ReadEntries(string fullPath)
    {
        var doc = XDocument.Load(fullPath);
        var root = doc.Root;
        if (root is null)
            return new List<ResxEntry>();

        return root.Elements("data")
            .Select(e => new ResxEntry(
                (string)e.Attribute("name")!,
                e.Element("value")?.Value ?? string.Empty,
                e.Element("comment")?.Value))
            .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record ResxEntry(string Name, string Value, string? Comment);
}
