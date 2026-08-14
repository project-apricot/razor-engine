using ApricotFramework.RazorEngine.AspNetCore.Impl;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.RazorEngine.AspNetCore.Tests;

public class MvcRazorEngineTests
{
    /// <summary>
    /// Builds an engine over the given view engine, with scope validation on so a lifetime mistake
    /// fails the test.
    /// </summary>
    /// <param name="viewEngine">The view engine to render through.</param>
    /// <param name="provider">The provider the engine creates its scopes from.</param>
    /// <returns>The engine.</returns>
    private static MvcRazorEngine CreateEngine(StubRazorViewEngine viewEngine, out ServiceProvider provider)
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopeMarker>();

        provider = services.BuildServiceProvider(validateScopes: true);

        return new MvcRazorEngine(viewEngine, new StubTempDataProvider(), provider);
    }

    /// <summary>
    /// Builds a view engine that resolves the given view by path.
    /// </summary>
    /// <param name="view">The view to return.</param>
    /// <returns>The view engine.</returns>
    private static StubRazorViewEngine EngineFinding(StubView view)
    {
        return new StubRazorViewEngine
        {
            PathResult = ViewEngineResult.Found(view.Path, view),
        };
    }

    [Fact]
    public async Task RenderAsync_ViewResolvedByPath_ReturnsWhatTheViewWrote()
    {
        var view = new StubView("~/Templates/Mail.cshtml", "<p>rendered</p>");
        var viewEngine = EngineFinding(view);

        var engine = CreateEngine(viewEngine, out var provider);
        await using var _ = provider;

        var result = await engine.RenderAsync("~/Templates/Mail.cshtml", new { }, TestContext.Current.CancellationToken);

        Assert.Equal("<p>rendered</p>", result);

        // The path lookup is asked for a main page, with no executing file to resolve against.
        var lookup = Assert.Single(viewEngine.PathLookups);
        Assert.Null(lookup.ExecutingFilePath);
        Assert.Equal("~/Templates/Mail.cshtml", lookup.ViewPath);
        Assert.True(lookup.IsMainPage);

        // A path that resolves must not also cost a name lookup.
        Assert.Empty(viewEngine.NameLookups);
    }

    [Fact]
    public async Task RenderAsync_PathLookupFails_FallsBackToTheViewNameLookup()
    {
        var view = new StubView("/Views/Shared/Notice.cshtml", "<p>by name</p>");
        var viewEngine = new StubRazorViewEngine
        {
            PathResult = ViewEngineResult.NotFound("Notice", ["/nowhere/Notice.cshtml"]),
            NameResult = ViewEngineResult.Found("Notice", view),
        };

        var engine = CreateEngine(viewEngine, out var provider);
        using var _ = provider;

        var result = await engine.RenderAsync("Notice", new { }, TestContext.Current.CancellationToken);

        Assert.Equal("<p>by name</p>", result);

        var lookup = Assert.Single(viewEngine.NameLookups);
        Assert.Equal("Notice", lookup.ViewName);
        Assert.True(lookup.IsMainPage);
    }

    [Fact]
    public async Task RenderAsync_NoViewMatches_ThrowsNamingTheViewAndEverySearchedLocation()
    {
        var viewEngine = new StubRazorViewEngine
        {
            PathResult = ViewEngineResult.NotFound("Missing", ["/Templates/Missing.cshtml", "/Shared/Missing.cshtml"]),
            NameResult = ViewEngineResult.NotFound("Missing", ["/Views/Shared/Missing.cshtml", "/Shared/Missing.cshtml"]),
        };

        var engine = CreateEngine(viewEngine, out var provider);
        using var _ = provider;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RenderAsync("Missing", new { }, TestContext.Current.CancellationToken));

        Assert.Contains("Missing", exception.Message, StringComparison.Ordinal);

        // One location per line, so the lines are what to count. Comparing whole lines rather than
        // substrings matters here: "/Views/Shared/Missing.cshtml" contains "/Shared/Missing.cshtml".
        var lines = exception.Message.Split(Environment.NewLine);

        Assert.Contains("/Templates/Missing.cshtml", lines);
        Assert.Contains("/Views/Shared/Missing.cshtml", lines);

        // A location reported by both lookups is listed once, not twice.
        Assert.Single(lines, line => string.Equals(line, "/Shared/Missing.cshtml", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RenderAsync_NoViewMatchesAndNoLocationsReported_StillNamesTheView()
    {
        var viewEngine = new StubRazorViewEngine
        {
            PathResult = ViewEngineResult.NotFound("Missing", []),
            NameResult = ViewEngineResult.NotFound("Missing", []),
        };

        var engine = CreateEngine(viewEngine, out var provider);
        using var _ = provider;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => engine.RenderAsync("Missing", new { }, TestContext.Current.CancellationToken));

        Assert.Contains("Missing", exception.Message, StringComparison.Ordinal);
        Assert.Contains("no locations searched", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenderAsync_ViewIsNullOrWhiteSpace_ThrowsWithoutConsultingTheViewEngine(string? view)
    {
        var viewEngine = new StubRazorViewEngine();

        var engine = CreateEngine(viewEngine, out var provider);
        using var _ = provider;

        // Null gives ArgumentNullException and white space gives ArgumentException; both are the same
        // contract to a caller.
        await Assert.ThrowsAnyAsync<ArgumentException>(
            () => engine.RenderAsync(view!, new { }, TestContext.Current.CancellationToken));

        Assert.Empty(viewEngine.PathLookups);
        Assert.Empty(viewEngine.NameLookups);
    }

    [Fact]
    public async Task RenderAsync_ModelSupplied_ReachesTheViewAsTypedViewData()
    {
        var view = new StubView("~/Templates/Mail.cshtml", string.Empty);
        var viewEngine = EngineFinding(view);

        var engine = CreateEngine(viewEngine, out var provider);
        using var _ = provider;

        var model = new ModelUnderTest("Ada");

        await engine.RenderAsync("~/Templates/Mail.cshtml", model, TestContext.Current.CancellationToken);

        var context = view.LastContext;
        Assert.NotNull(context);
        Assert.Same(model, context.ViewData.Model);
        Assert.Equal(typeof(ModelUnderTest), context.ViewData.ModelMetadata.ModelType);
    }

    [Fact]
    public async Task RenderAsync_ModelIsNull_RendersRatherThanThrowing()
    {
        var view = new StubView("~/Templates/Mail.cshtml", "<p>no model needed</p>");
        var viewEngine = EngineFinding(view);

        var engine = CreateEngine(viewEngine, out var provider);
        using var _ = provider;

        var result = await engine.RenderAsync<ModelUnderTest?>("~/Templates/Mail.cshtml", null, TestContext.Current.CancellationToken);

        Assert.Equal("<p>no model needed</p>", result);

        var context = view.LastContext;
        Assert.NotNull(context);
        Assert.Null(context.ViewData.Model);
    }

    [Fact]
    public async Task RenderAsync_TokenSupplied_IsVisibleToTheViewAsRequestAborted()
    {
        var view = new StubView("~/Templates/Mail.cshtml", string.Empty);
        var viewEngine = EngineFinding(view);

        var engine = CreateEngine(viewEngine, out var provider);
        using var _ = provider;

        using var source = new CancellationTokenSource();

        await engine.RenderAsync("~/Templates/Mail.cshtml", new { }, source.Token);

        var context = view.LastContext;
        Assert.NotNull(context);
        Assert.Equal(source.Token, context.HttpContext.RequestAborted);
    }

    [Fact]
    public async Task RenderAsync_TokenAlreadyCancelled_ThrowsWithoutConsultingTheViewEngine()
    {
        var viewEngine = new StubRazorViewEngine();

        var engine = CreateEngine(viewEngine, out var provider);
        using var _ = provider;

        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => engine.RenderAsync("~/Templates/Mail.cshtml", new { }, source.Token));

        Assert.Empty(viewEngine.PathLookups);
    }

    [Fact]
    public async Task RenderAsync_CalledTwice_GivesEachRenderItsOwnScope()
    {
        var view = new StubView("~/Templates/Mail.cshtml", string.Empty);
        var viewEngine = EngineFinding(view);

        var engine = CreateEngine(viewEngine, out var provider);
        using var _ = provider;

        // Resolved during the render, because the scope is disposed by the time RenderAsync returns.
        var markers = new List<ScopeMarker>();
        view.OnRender = context => markers.Add(context.HttpContext.RequestServices.GetRequiredService<ScopeMarker>());

        await engine.RenderAsync("~/Templates/Mail.cshtml", new { }, TestContext.Current.CancellationToken);
        await engine.RenderAsync("~/Templates/Mail.cshtml", new { }, TestContext.Current.CancellationToken);

        // A scope per render is what lets the engine run with no request in flight, and is why a view
        // does not share the scoped instances of a surrounding request.
        Assert.Equal(2, markers.Count);
        Assert.NotSame(markers[0], markers[1]);
    }

    [Fact]
    public async Task RenderAsync_ViewResolvesAScopedService_DoesNotSeeTheRootProvider()
    {
        var view = new StubView("~/Templates/Mail.cshtml", string.Empty);
        var viewEngine = EngineFinding(view);

        var engine = CreateEngine(viewEngine, out var provider);
        using var _ = provider;

        IServiceProvider? seen = null;
        view.OnRender = context => seen = context.HttpContext.RequestServices;

        await engine.RenderAsync("~/Templates/Mail.cshtml", new { }, TestContext.Current.CancellationToken);

        Assert.NotNull(seen);
        Assert.NotSame(provider, seen);
    }

    [Fact]
    public void Constructor_ViewEngineIsNull_Throws()
    {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Throws<ArgumentNullException>(
            () => new MvcRazorEngine(null!, new StubTempDataProvider(), provider));
    }

    [Fact]
    public void Constructor_TempDataProviderIsNull_Throws()
    {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Throws<ArgumentNullException>(
            () => new MvcRazorEngine(new StubRazorViewEngine(), null!, provider));
    }

    [Fact]
    public void Constructor_ServiceProviderIsNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new MvcRazorEngine(new StubRazorViewEngine(), new StubTempDataProvider(), null!));
    }

    /// <summary>
    /// A model with a property, so the typed view data can be checked for more than null.
    /// </summary>
    /// <param name="Recipient">The recipient the template would greet.</param>
    private sealed record ModelUnderTest(string Recipient);
}
