using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace ApricotFramework.RazorEngine.AspNetCore.Tests;

/// <summary>
/// A view that writes fixed markup and records the context it was rendered against.
/// </summary>
/// <param name="path">The path reported by the view.</param>
/// <param name="markup">The markup the view writes.</param>
internal sealed class StubView(string path, string markup) : IView
{
    /// <inheritdoc />
    public string Path { get; } = path;

    /// <summary>
    /// Gets the context of the last render, so a test can inspect what the view was given.
    /// </summary>
    public ViewContext? LastContext { get; private set; }

    /// <summary>
    /// Runs during the render, which is the only point at which the render's scope is still alive.
    /// </summary>
    public Action<ViewContext>? OnRender { get; set; }

    /// <inheritdoc />
    public Task RenderAsync(ViewContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        this.LastContext = context;

        this.OnRender?.Invoke(context);

        return context.Writer.WriteAsync(markup);
    }
}

/// <summary>
/// A view engine returning results a test dictates, and recording how it was asked for them.
/// </summary>
internal sealed class StubRazorViewEngine : IRazorViewEngine
{
    /// <summary>
    /// The result returned from a path lookup.
    /// </summary>
    public ViewEngineResult PathResult { get; set; } = ViewEngineResult.NotFound("unset", []);

    /// <summary>
    /// The result returned from a view-name lookup.
    /// </summary>
    public ViewEngineResult NameResult { get; set; } = ViewEngineResult.NotFound("unset", []);

    /// <summary>
    /// Gets the arguments of each path lookup.
    /// </summary>
    public List<(string? ExecutingFilePath, string ViewPath, bool IsMainPage)> PathLookups { get; } = [];

    /// <summary>
    /// Gets the arguments of each view-name lookup.
    /// </summary>
    public List<(ActionContext Context, string ViewName, bool IsMainPage)> NameLookups { get; } = [];

    /// <inheritdoc />
    public ViewEngineResult GetView(string? executingFilePath, string viewPath, bool isMainPage)
    {
        this.PathLookups.Add((executingFilePath, viewPath, isMainPage));

        return this.PathResult;
    }

    /// <inheritdoc />
    public ViewEngineResult FindView(ActionContext context, string viewName, bool isMainPage)
    {
        this.NameLookups.Add((context, viewName, isMainPage));

        return this.NameResult;
    }

    /// <inheritdoc />
    public string? GetAbsolutePath(string? executingFilePath, string? pagePath)
    {
        return pagePath;
    }

    /// <inheritdoc />
    public RazorPageResult GetPage(string executingFilePath, string pagePath)
    {
        // The engine renders views, never bare pages.
        throw new NotSupportedException();
    }

    /// <inheritdoc />
    public RazorPageResult FindPage(ActionContext context, string pageName)
    {
        throw new NotSupportedException();
    }
}

/// <summary>
/// A temp data provider backed by a dictionary, so no data protection or session is needed.
/// </summary>
internal sealed class StubTempDataProvider : ITempDataProvider
{
    /// <inheritdoc />
    public IDictionary<string, object> LoadTempData(HttpContext context)
    {
        return new Dictionary<string, object>(StringComparer.Ordinal);
    }

    /// <inheritdoc />
    public void SaveTempData(HttpContext context, IDictionary<string, object> values)
    {
        // Nothing renders through a real store in these tests.
    }
}

/// <summary>
/// A scoped service, so a test can tell one render's scope from another's.
/// </summary>
internal sealed class ScopeMarker;
