using System.Xml.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        var files = EnumerateResourceFiles(root)
            .Where(path => !IsExcludedResource(path))
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

    private IEnumerable<string> EnumerateResourceFiles(string root)
    {
        var configuredPatterns = string.IsNullOrWhiteSpace(_workspace.ResxSearchPattern)
            ? ["*.resx", "*.json"]
            : _workspace.ResxSearchPattern
                .Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return configuredPatterns
            .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
            .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsExcludedResource(string fullPath)
    {
        var name = Path.GetFileName(fullPath);
        var segments = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return name.EndsWith(".Designer.resx", StringComparison.OrdinalIgnoreCase)
            || name.Equals("package.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("package-lock.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("tsconfig.json", StringComparison.OrdinalIgnoreCase)
            || name.Equals("tsconfig.node.json", StringComparison.OrdinalIgnoreCase)
            || segments.Any(IsExcludedDirectory);
    }

    private static bool IsExcludedDirectory(string segment) =>
        segment.Equals("node_modules", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("bin", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("dist", StringComparison.OrdinalIgnoreCase)
        || segment.Equals("build", StringComparison.OrdinalIgnoreCase);

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
        var updated = IsJsonFile(fullPath)
            ? UpdateJsonFile(fullPath, request, cancellationToken)
            : UpdateResxFile(fullPath, request, cancellationToken);

        return new ResxUpdateResult(NormalizeRelative(relativePath), updated);
    }

    private static int UpdateResxFile(
        string fullPath,
        ResxUpdateRequest request,
        CancellationToken cancellationToken)
    {
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
        return updated;
    }

    private static int UpdateJsonFile(
        string fullPath,
        ResxUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var json = File.ReadAllText(fullPath);
        var node = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException("JSON resource root must be an object.");

        var updated = 0;
        foreach (var entry in request.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var setResult = TrySetJsonString(node, entry.Name, entry.Value);
            if (setResult == JsonSetResult.Updated)
            {
                updated++;
                continue;
            }
            if (setResult == JsonSetResult.Unchanged)
                continue;

            if (!node.TryGetPropertyValue(entry.Name, out var existing)
                || existing is not JsonValue value
                || !value.TryGetValue<string>(out var existingValue)
                || !string.Equals(existingValue, entry.Value, StringComparison.Ordinal))
            {
                node[entry.Name] = entry.Value;
                updated++;
            }
        }

        File.WriteAllText(fullPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return updated;
    }

    private async Task<string> ResolveExistingPathAsync(string relativePath, CancellationToken cancellationToken)
    {
        var root = await _git.EnsureRepositoryAsync(cancellationToken);
        var normalized = NormalizeRelative(relativePath);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalized));
        if (!fullPath.StartsWith(Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Invalid resource path.");
        if (!File.Exists(fullPath))
            throw new FileNotFoundException($"Resource file not found: {normalized}", fullPath);
        if (!IsSupportedResourceFile(fullPath))
            throw new InvalidOperationException("Only .resx and .json resource files are supported.");
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
        return IsJsonFile(fullPath) ? ReadJsonEntries(fullPath) : ReadResxEntries(fullPath);
    }

    private static List<ResxEntry> ReadResxEntries(string fullPath)
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

    private static List<ResxEntry> ReadJsonEntries(string fullPath)
    {
        var node = JsonNode.Parse(File.ReadAllText(fullPath)) as JsonObject;
        if (node is null)
            return new List<ResxEntry>();

        var entries = new List<ResxEntry>();
        CollectJsonEntries(node, prefix: null, entries);
        return entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void CollectJsonEntries(JsonObject obj, string? prefix, List<ResxEntry> entries)
    {
        foreach (var (key, value) in obj)
        {
            var name = string.IsNullOrEmpty(prefix) ? key : $"{prefix}.{key}";
            if (value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text))
            {
                entries.Add(new ResxEntry(name, text, null));
                continue;
            }

            if (value is JsonObject child)
                CollectJsonEntries(child, name, entries);
        }
    }

    private static JsonSetResult TrySetJsonString(JsonObject root, string path, string value)
    {
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return JsonSetResult.NotFound;

        var current = root;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (current[parts[i]] is not JsonObject child)
                return JsonSetResult.NotFound;
            current = child;
        }

        var leaf = parts[^1];
        if (current[leaf] is not JsonValue jsonValue || !jsonValue.TryGetValue<string>(out var existing))
            return JsonSetResult.NotFound;
        if (string.Equals(existing, value, StringComparison.Ordinal))
            return JsonSetResult.Unchanged;

        current[leaf] = value;
        return JsonSetResult.Updated;
    }

    private static bool IsJsonFile(string fullPath) =>
        Path.GetExtension(fullPath).Equals(".json", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportedResourceFile(string fullPath) =>
        Path.GetExtension(fullPath).Equals(".resx", StringComparison.OrdinalIgnoreCase) || IsJsonFile(fullPath);

    private enum JsonSetResult
    {
        NotFound,
        Unchanged,
        Updated
    }

    private sealed record ResxEntry(string Name, string Value, string? Comment);
}
