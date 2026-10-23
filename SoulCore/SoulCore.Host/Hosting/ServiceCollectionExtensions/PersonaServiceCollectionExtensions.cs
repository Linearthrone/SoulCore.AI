using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SoulCore.Config;
using SoulCore.Core.Persona;
using SoulCore.Host.Persona;

namespace SoulCore.Host.Hosting.ServiceCollectionExtensions;

internal static class PersonaServiceCollectionExtensions
{
    internal static IServiceCollection AddPersonaRuntime(this IServiceCollection services)
    {
        services.AddSingleton<IPersonaPackStore, PersonaPackStore>();
        services.AddSingleton<PersonaStoreHub>();
        services.AddSingleton<IPersonaStoreHub>(sp => sp.GetRequiredService<PersonaStoreHub>());
        services.AddSingleton<HostChatMode>(sp =>
        {
            var root = (sp.GetRequiredService<IOptions<PersonaOptions>>().Value
                ?? new PersonaOptions()).ResolveRootDirectory();
            return new HostChatMode(root);
        });
        services.AddSingleton<IHostChatMode>(sp => sp.GetRequiredService<HostChatMode>());
        services.AddSingleton<ForegroundToolLoopGate>();
        services.AddSingleton<IForegroundToolLoopGate>(sp => sp.GetRequiredService<ForegroundToolLoopGate>());
        services.AddSingleton<ActivePersonaSession>();
        services.AddSingleton<IPersonaSession>(sp => sp.GetRequiredService<ActivePersonaSession>());
        services.AddSingleton<IPersonaToolPathsResolver, PersonaToolPathsResolver>();
        services.AddHostedService<PersonaSessionHostedService>();
        return services;
    }
}
