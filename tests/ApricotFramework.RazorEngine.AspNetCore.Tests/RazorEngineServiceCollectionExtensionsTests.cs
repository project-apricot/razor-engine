using System.Diagnostics;
using ApricotFramework.RazorEngine.AspNetCore.Extensions;
using ApricotFramework.RazorEngine.AspNetCore.Impl;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.RazorEngine.AspNetCore.Tests;

public class RazorEngineServiceCollectionExtensionsTests
{
    /// <summary>
    /// An engine a consumer might register in place of the built-in one.
    /// </summary>
    private sealed class FixedRazorEngine : IRazorEngine
    {
        /// <inheritdoc />
        public Task<string> RenderAsync<TModel>(string view, TModel model, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("<p>replaced</p>");
        }
    }

    /// <summary>
    /// Builds a collection carrying the diagnostics a web host supplies, since the Razor page
    /// activator takes a <see cref="DiagnosticSource"/> dependency that no MVC registration provides.
    /// </summary>
    /// <returns>The service collection.</returns>
    private static ServiceCollection CreateWebHostServices()
    {
        var services = new ServiceCollection();

        var listener = new DiagnosticListener("ApricotFramework.RazorEngine.Tests");
        services.AddSingleton(listener);
        services.AddSingleton<DiagnosticSource>(listener);

        return services;
    }

    /// <summary>
    /// Builds a provider with scope validation on, so a lifetime mistake fails the test.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service provider.</returns>
    private static ServiceProvider Build(IServiceCollection services)
    {
        return services.BuildServiceProvider(validateScopes: true);
    }

    [Fact]
    public void AddRazorEngine_WithNoOtherMvcSetup_ResolvesTheMvcEngine()
    {
        var services = CreateWebHostServices();

        // The claim the library makes: this one call is enough, with no AddControllersWithViews.
        services.AddRazorEngine();

        using var provider = Build(services);

        Assert.IsType<MvcRazorEngine>(provider.GetRequiredService<IRazorEngine>());
    }

    [Fact]
    public void AddRazorEngine_WithNoOtherMvcSetup_ResolvesTheViewServicesTheEngineNeeds()
    {
        var services = CreateWebHostServices();

        services.AddRazorEngine();

        using var provider = Build(services);

        // Both are constructor dependencies of MvcRazorEngine, and neither is registered by the
        // library itself — they come from the MVC view registrations AddRazorEngine pulls in.
        Assert.NotNull(provider.GetRequiredService<IRazorViewEngine>());
        Assert.NotNull(provider.GetRequiredService<ITempDataProvider>());
    }

    [Fact]
    public void AddRazorEngine_AfterAddControllersWithViews_ResolvesTheMvcEngine()
    {
        var services = CreateWebHostServices();

        services.AddControllersWithViews();
        services.AddRazorEngine();

        using var provider = Build(services);

        Assert.IsType<MvcRazorEngine>(provider.GetRequiredService<IRazorEngine>());
    }

    [Fact]
    public void AddRazorEngine_BeforeAddControllersWithViews_ResolvesTheMvcEngine()
    {
        var services = CreateWebHostServices();

        // The reverse order of the test above: the registrations are additive, so neither call has to
        // come first.
        services.AddRazorEngine();
        services.AddControllersWithViews();

        using var provider = Build(services);

        Assert.IsType<MvcRazorEngine>(provider.GetRequiredService<IRazorEngine>());
    }

    [Fact]
    public void AddRazorEngine_CalledTwice_RegistersOneEngine()
    {
        var services = CreateWebHostServices();

        services.AddRazorEngine();
        services.AddRazorEngine();

        Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IRazorEngine));
    }

    [Fact]
    public void AddRazorEngine_EngineAlreadyRegistered_KeepsTheRegisteredEngine()
    {
        var services = CreateWebHostServices();

        services.AddSingleton<IRazorEngine, FixedRazorEngine>();
        services.AddRazorEngine();

        using var provider = Build(services);

        // TryAdd semantics: a consumer's own engine is not displaced by the built-in one.
        Assert.IsType<FixedRazorEngine>(provider.GetRequiredService<IRazorEngine>());
    }

    [Fact]
    public void AddRazorEngine_TheEngineIsASingleton()
    {
        var services = CreateWebHostServices();

        services.AddRazorEngine();

        using var provider = Build(services);

        using var scope = provider.CreateScope();

        Assert.Same(provider.GetRequiredService<IRazorEngine>(), scope.ServiceProvider.GetRequiredService<IRazorEngine>());
    }

    [Fact]
    public void AddRazorEngine_ServicesIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => RazorEngineServiceCollectionExtensions.AddRazorEngine(null!));
    }

    [Fact]
    public void AddRazorEngine_ReturnsTheSameCollectionForChaining()
    {
        var services = CreateWebHostServices();

        Assert.Same(services, services.AddRazorEngine());
    }
}
