using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.RazorEngine.AspNetCore.Impl;

/// <summary>
/// A razor engine built on the MVC Razor view engine, rendering into a writer of its own instead of
/// the response body.
/// </summary>
/// <remarks>
/// Each render runs against a freshly created service scope and a synthesized
/// <see cref="DefaultHttpContext"/>, so a view may resolve scoped services and read
/// <c>Context.RequestAborted</c> whether a real request is in flight.
/// </remarks>
public class MvcRazorEngine : IRazorEngine
{
    /// <summary>
    /// The view engine that locates and compiles views.
    /// </summary>
    private readonly IRazorViewEngine viewEngine;

    /// <summary>
    /// The provider backing the view's temp data.
    /// </summary>
    private readonly ITempDataProvider tempDataProvider;

    /// <summary>
    /// The provider each render creates its scope from.
    /// </summary>
    private readonly IServiceProvider serviceProvider;

    /// <summary>
    /// Creates a new engine.
    /// </summary>
    /// <param name="viewEngine">The view engine that locates and compiles views.</param>
    /// <param name="tempDataProvider">The provider backing the view's temp data.</param>
    /// <param name="serviceProvider">The provider each render creates its scope from.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public MvcRazorEngine(IRazorViewEngine viewEngine, ITempDataProvider tempDataProvider, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(viewEngine);
        ArgumentNullException.ThrowIfNull(tempDataProvider);
        ArgumentNullException.ThrowIfNull(serviceProvider);

        this.viewEngine = viewEngine;
        this.tempDataProvider = tempDataProvider;
        this.serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public virtual async Task<string> RenderAsync<TModel>(string view, TModel model, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(view);

        cancellationToken.ThrowIfCancellationRequested();

        // A scope per render, rather than an ambient one, is what lets the engine be called with no
        // request in flight. The cost is that a view resolving a scoped service during a request gets
        // a different instance than the request itself holds.
        using var scope = this.serviceProvider.CreateScope();

        var actionContext = this.GetActionContext(scope, cancellationToken);

        var razorView = this.FindView(actionContext, view);

        await using var output = new StringWriter();

        var viewData = new ViewDataDictionary<TModel>(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model,
        };

        var tempData = new TempDataDictionary(actionContext.HttpContext, this.tempDataProvider);

        var viewContext = new ViewContext(actionContext, razorView, viewData, tempData, output, new HtmlHelperOptions());

        // Razor's own RenderAsync takes no token; a template that awaits observes the one carried on
        // the context instead.
        await razorView.RenderAsync(viewContext);

        return output.ToString();
    }

    /// <summary>
    /// Locates the view to render.
    /// </summary>
    /// <param name="actionContext">The context the view-name lookup resolves against.</param>
    /// <param name="view">The view path or view name.</param>
    /// <returns>The located view.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="actionContext"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="view"/> is null, empty or white space.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when no view matches.</exception>
    /// <remarks>
    /// Override this to resolve views from somewhere the view engine does not look.
    /// </remarks>
    protected virtual IView FindView(ActionContext actionContext, string view)
    {
        ArgumentNullException.ThrowIfNull(actionContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(view);

        // A path first, which is how a template living outside the MVC view folders is addressed.
        var byPath = this.viewEngine.GetView(executingFilePath: null, viewPath: view, isMainPage: true);

        if (byPath.Success)
        {
            return byPath.View;
        }

        // Then the view-name conventions. With no route values on the context these reach only the
        // location patterns that need none, in a default configuration /Views/Shared.
        var byName = this.viewEngine.FindView(actionContext, view, isMainPage: true);

        if (byName.Success)
        {
            return byName.View;
        }

        throw new InvalidOperationException(DescribeMissingView(view, byPath, byName));
    }

    /// <summary>
    /// Builds the context a view is rendered against.
    /// </summary>
    /// <param name="scope">The scope this render owns.</param>
    /// <param name="cancellationToken">The token the rendering observes.</param>
    /// <returns>The context to render against.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="scope"/> is null.</exception>
    /// <remarks>
    /// Override this to put route values, features or a user on the context the view sees.
    /// </remarks>
    protected virtual ActionContext GetActionContext(IServiceScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            RequestAborted = cancellationToken,
        };

        return new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
    }

    /// <summary>
    /// Describes a failed lookup in terms of what was searched, so a mistyped view is diagnosable
    /// from the message alone.
    /// </summary>
    /// <param name="view">The view path or view name that was not found.</param>
    /// <param name="byPath">The result of the path lookup.</param>
    /// <param name="byName">The result of the view-name lookup.</param>
    /// <returns>The exception message.</returns>
    private static string DescribeMissingView(string view, ViewEngineResult byPath, ViewEngineResult byName)
    {
        var searched = byPath.SearchedLocations
            .Concat(byName.SearchedLocations)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (searched.Length == 0)
        {
            return $"The view '{view}' was not found, and the view engine reported no locations searched.";
        }

        return $"The view '{view}' was not found. Locations searched:{Environment.NewLine}{string.Join(Environment.NewLine, searched)}";
    }
}
