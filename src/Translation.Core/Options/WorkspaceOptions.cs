namespace Translation.Core.Options;

public sealed class WorkspaceOptions
{
    public const string SectionName = "Workspace";

    public string RootPath { get; set; } = Path.Combine(Path.GetTempPath(), "translation-workspaces");
    public string ResxSearchPattern { get; set; } = "*.resx";
}
