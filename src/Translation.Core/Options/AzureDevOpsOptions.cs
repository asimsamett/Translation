namespace Translation.Core.Options;

public sealed class AzureDevOpsOptions
{
    public const string SectionName = "AzureDevOps";

    public string Organization { get; set; } = string.Empty;
    public string Project { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string DefaultBranch { get; set; } = "main";
    public string? PersonalAccessToken { get; set; }

    /// <summary>
    /// Azure DevOps'tan kopyalanan HTTPS clone URL (varsa Organization/Project/Repository yerine kullanılır).
    /// </summary>
    public string? CloneUrl { get; set; }

    /// <summary>Varsayılan: https://dev.azure.com</summary>
    public string? GitBaseUrl { get; set; }

    /// <summary>HTTPS git işlemlerinde kullanıcı adı (varsayılan: pat).</summary>
    public string GitUsername { get; set; } = "pat";

    /// <summary>Clone URL içine PAT göm (LibGit2Sharp uyumluluğu için önerilir).</summary>
    public bool EmbedPatInCloneUrl { get; set; } = true;
}
