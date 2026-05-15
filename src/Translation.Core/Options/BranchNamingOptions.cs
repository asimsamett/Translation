namespace Translation.Core.Options;

public sealed class BranchNamingOptions
{
    public const string SectionName = "BranchNaming";

  /// <summary>
  /// Placeholders: {user}, {date} (yyyyMMdd), {datetime} (yyyyMMdd-HHmm), {suffix}
  /// </summary>
    public string Pattern { get; set; } = "translation/{user}/{date}";

    public int MaxLength { get; set; } = 60;
}
