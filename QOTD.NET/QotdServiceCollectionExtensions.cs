#if USE_MS_EXTENTIONS
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
#if NET5_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif

namespace QOTD.NET;

/// <summary>
/// Dependency injection extensions for registering <see cref="QotdServer"/> and <see cref="QotdClient"/> services.
/// </summary>
public static class QotdServiceCollectionExtensions
{
    private static void AddQotdServerInternals(IServiceCollection services)
    {
        services.AddHostedService<QotdServer>();
    }

    /// <summary>
    /// Adds a <see cref="QotdServer"/> hosted service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The input service collection.</returns>
    public static IServiceCollection AddQotdServer(this IServiceCollection services)
    {
        services.AddOptions();
        AddQotdServerInternals(services);
        return services;
    }

    /// <summary>
    /// Adds a <see cref="QotdServer"/> hosted service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="namedConfigurationSection">Configuration binded to <see cref="QotdServerOptions"/>.</param>
    /// <returns>The input service collection.</returns>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Binding strongly typed objects to configuration values may require generating dynamic code at runtime.")]
#endif
#if NET5_0_OR_GREATER
    [RequiresUnreferencedCode("QotdServerOptions's dependent types may have their members trimmed. Ensure all required members are preserved.")]
#endif
    public static IServiceCollection AddQotdServer(this IServiceCollection services, IConfiguration namedConfigurationSection)
    {
        services.Configure<QotdServerOptions>(namedConfigurationSection);
        AddQotdServerInternals(services);
        return services;
    }

#if NETCOREAPP

    /// <summary>
    /// Adds a <see cref="QotdServer"/> hosted service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configSectionPath">Configuration path binded to <see cref="QotdServerOptions"/>.</param>
    /// <returns>The input service collection.</returns>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Binding strongly typed objects to configuration values may require generating dynamic code at runtime.")]
#endif
#if NET5_0_OR_GREATER
    [RequiresUnreferencedCode("QotdServerOptions's dependent types may have their members trimmed. Ensure all required members are preserved.")]
#endif
    public static IServiceCollection AddQotdServer(this IServiceCollection services, string configSectionPath)
    {
        services.AddOptions<QotdServerOptions>()
                .BindConfiguration(configSectionPath)
#if NET8_0_OR_GREATER
                .ValidateOnStart()
#endif
                ;

        AddQotdServerInternals(services);
        return services;
    }

#endif

    /// <summary>
    /// Adds a <see cref="QotdServer"/> hosted service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Callback to change configuration settings for the server's options.</param>
    /// <returns>The input service collection.</returns>
    public static IServiceCollection AddQotdServer(this IServiceCollection services, Action<QotdServerOptions> configureOptions)
    {
        services.Configure(configureOptions);
        AddQotdServerInternals(services);
        return services;
    }

    /// <summary>
    /// Adds a <see cref="QotdServer"/> hosted service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="userOptions">Settings for the server's options.</param>
    /// <returns>The input service collection.</returns>
    public static IServiceCollection AddQotdServer(this IServiceCollection services, QotdServerOptions userOptions)
    {
        services.AddOptions<QotdServerOptions>()
                .Configure(options => options.UpdateFrom(userOptions));

        AddQotdServerInternals(services);
        return services;
    }
    




    private static void AddQotdClientInternals(IServiceCollection services)
    {
        services.AddTransient<QotdClient>();
    }

    /// <summary>
    /// Adds a <see cref="QotdClient"/> transient service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The input service collection.</returns>
    public static IServiceCollection AddQotdClient(this IServiceCollection services)
    {
        services.AddOptions();
        AddQotdClientInternals(services);
        return services;
    }

    /// <summary>
    /// Adds a <see cref="QotdClient"/> transient service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="namedConfigurationSection">Configuration binded to <see cref="QotdClientOptions"/>.</param>
    /// <returns>The input service collection.</returns>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Binding strongly typed objects to configuration values may require generating dynamic code at runtime.")]
#endif
#if NET5_0_OR_GREATER
    [RequiresUnreferencedCode("QotdClientOptions's dependent types may have their members trimmed. Ensure all required members are preserved.")]
#endif
    public static IServiceCollection AddQotdClient(this IServiceCollection services, IConfiguration namedConfigurationSection)
    {
        services.Configure<QotdClientOptions>(namedConfigurationSection);
        AddQotdClientInternals(services);
        return services;
    }
    
#if NETCOREAPP

    /// <summary>
    /// Adds a <see cref="QotdClient"/> transient service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configSectionPath">Configuration path binded to <see cref="QotdClientOptions"/>.</param>
    /// <returns>The input service collection.</returns>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Binding strongly typed objects to configuration values may require generating dynamic code at runtime.")]
#endif
#if NET5_0_OR_GREATER
    [RequiresUnreferencedCode("QotdClientOptions's dependent types may have their members trimmed. Ensure all required members are preserved.")]
#endif
    public static IServiceCollection AddQotdClient(this IServiceCollection services, string configSectionPath)
    {
        services.AddOptions<QotdClientOptions>()
                .BindConfiguration(configSectionPath)
#if NET8_0_OR_GREATER
                .ValidateOnStart()
#endif
                ;

        AddQotdClientInternals(services);
        return services;
    }

#endif

    /// <summary>
    /// Adds a <see cref="QotdClient"/> transient service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Callback to change configuration settings for the client's options.</param>
    /// <returns>The input service collection.</returns>
    public static IServiceCollection AddQotdClient(this IServiceCollection services, Action<QotdClientOptions> configureOptions)
    {
        services.Configure(configureOptions);
        AddQotdClientInternals(services);
        return services;
    }

    /// <summary>
    /// Adds a <see cref="QotdClient"/> transient service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="userOptions">Settings for the client's options.</param>
    /// <returns>The input service collection.</returns>
    public static IServiceCollection AddQotdClient(this IServiceCollection services, QotdClientOptions userOptions)
    {
        services.AddOptions<QotdClientOptions>()
                .Configure(options => options.UpdateFrom(userOptions));

        AddQotdClientInternals(services);
        return services;
    }
    




    private static void AddDailyQuoteProviderInternals(IServiceCollection services)
    {
        services.AddTransient<IQuoteProvider, DailyQuoteProvider>();
    }

    /// <summary>
    /// Adds a <see cref="DailyQuoteProvider"/> transient implementation of <see cref="IQuoteProvider"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The input service collection.</returns>
    public static IServiceCollection AddDailyQuoteProvider(this IServiceCollection services)
    {
        services.AddOptions();
        AddDailyQuoteProviderInternals(services);
        return services;
    }

    /// <summary>
    /// Adds a <see cref="DailyQuoteProvider"/> transient implementation of <see cref="IQuoteProvider"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="namedConfigurationSection">Configuration binded to <see cref="DailyQuoteProviderOptions"/>.</param>
    /// <returns>The input service collection.</returns>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Binding strongly typed objects to configuration values may require generating dynamic code at runtime.")]
#endif
#if NET5_0_OR_GREATER
    [RequiresUnreferencedCode("DailyQuoteProviderOptions's dependent types may have their members trimmed. Ensure all required members are preserved.")]
#endif
    public static IServiceCollection AddDailyQuoteProvider(this IServiceCollection services, IConfiguration namedConfigurationSection)
    {
        services.Configure<DailyQuoteProviderOptions>(namedConfigurationSection);
        AddDailyQuoteProviderInternals(services);
        return services;
    }
    
#if NETCOREAPP

    /// <summary>
    /// Adds a <see cref="DailyQuoteProvider"/> transient implementation of <see cref="IQuoteProvider"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configSectionPath">Configuration path binded to <see cref="DailyQuoteProviderOptions"/>.</param>
    /// <returns>The input service collection.</returns>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("Binding strongly typed objects to configuration values may require generating dynamic code at runtime.")]
#endif
#if NET5_0_OR_GREATER
    [RequiresUnreferencedCode("DailyQuoteProviderOptions's dependent types may have their members trimmed. Ensure all required members are preserved.")]
#endif
    public static IServiceCollection AddDailyQuoteProvider(this IServiceCollection services, string configSectionPath)
    {
        services.AddOptions<DailyQuoteProviderOptions>()
                .BindConfiguration(configSectionPath)
#if NET8_0_OR_GREATER
                .ValidateOnStart()
#endif
                ;

        AddDailyQuoteProviderInternals(services);
        return services;
    }

#endif

    /// <summary>
    /// Adds a <see cref="DailyQuoteProvider"/> transient implementation of <see cref="IQuoteProvider"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureOptions">Callback to change configuration settings for the provider's options.</param>
    /// <returns>The input service collection.</returns>
    public static IServiceCollection AddDailyQuoteProvider(this IServiceCollection services, Action<DailyQuoteProviderOptions> configureOptions)
    {
        services.Configure(configureOptions);
        AddDailyQuoteProviderInternals(services);
        return services;
    }

    /// <summary>
    /// Adds a <see cref="DailyQuoteProvider"/> transient implementation of <see cref="IQuoteProvider"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="userOptions">Settings for the provider's options.</param>
    /// <returns>The input service collection.</returns>
    public static IServiceCollection AddDailyQuoteProvider(this IServiceCollection services, DailyQuoteProviderOptions userOptions)
    {
        services.AddOptions<DailyQuoteProviderOptions>()
                .Configure(options => options.UpdateFrom(userOptions));

        AddDailyQuoteProviderInternals(services);
        return services;
    }
}

#endif