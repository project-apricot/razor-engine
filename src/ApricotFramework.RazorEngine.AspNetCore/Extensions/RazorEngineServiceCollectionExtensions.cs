using ApricotFramework.RazorEngine.AspNetCore.Impl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApricotFramework.RazorEngine.AspNetCore.Extensions;

/// <summary>
/// Registers the razor engine in a service collection.
/// </summary>
public static class RazorEngineServiceCollectionExtensions
{
    /// <summary>
    /// Adds the razor engine, along with the MVC view services it renders through.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> is null.
    /// </exception>
    /// <remarks>
    /// In a web host this is the only call needed: an application that serves no MVC views of its own
    /// still gets a working engine. Everything registered here is additive, so an application that also
    /// calls <c>AddControllersWithViews</c> or <c>AddRazorPages</c> is unaffected by the order of the
    /// calls, and an <see cref="IRazorEngine"/> registered beforehand is left in place.
    /// <para>
    /// A host that is not a web host must additionally supply the two services the Razor view engine
    /// takes from one: a <c>DiagnosticSource</c>, without which the engine cannot be activated, and an
    /// <c>IWebHostEnvironment</c>, without which no compiled view is found. Neither can be given a
    /// meaningful value by a library, so neither is registered here.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddRazorEngine(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // adds a view engine that locates and compiles views, and the temp data provider a ViewContext requires
        services.AddMvcCore().AddRazorViewEngine();

        services.TryAddSingleton<IRazorEngine, MvcRazorEngine>();

        return services;
    }
}
