using Microsoft.Extensions.DependencyInjection;
using Translation.Core.Options;
using Translation.Core.Services;

namespace Translation.Core;

public static class DependencyInjection
{
    public static IServiceCollection AddTranslationCore(this IServiceCollection services)
    {
        services.AddOptions<AzureDevOpsOptions>()
            .BindConfiguration(AzureDevOpsOptions.SectionName)
            .Validate(o => !string.IsNullOrWhiteSpace(o.Organization), "AzureDevOps:Organization is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Project), "AzureDevOps:Project is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Repository), "AzureDevOps:Repository is required.")
            .ValidateOnStart();

        services.AddOptions<WorkspaceOptions>()
            .BindConfiguration(WorkspaceOptions.SectionName);

        services.AddOptions<BranchNamingOptions>()
            .BindConfiguration(BranchNamingOptions.SectionName);

        services.AddHttpClient(nameof(AzureDevOpsService));
        services.AddSingleton<BranchNamingService>();
        services.AddSingleton<IGitWorkspaceService, GitWorkspaceService>();
        services.AddSingleton<IResxService, ResxService>();
        services.AddSingleton<IAzureDevOpsService, AzureDevOpsService>();

        return services;
    }
}
