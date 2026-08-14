namespace ApricotFramework.RazorEngine.AspNetCore;

/// <summary>
/// Renders a Razor view to a string, rather than to the response of the request being handled.
/// </summary>
/// <remarks>
/// This is the shape needed when the rendered markup is not the response: the body of an e-mail, the
/// input to a PDF converter, a fragment cached for later. Nothing about it requires a request to be
/// in flight, so it is equally usable from a background job.
/// </remarks>
public interface IRazorEngine
{
    /// <summary>
    /// Renders a view with the given model and returns the markup it produced.
    /// </summary>
    /// <param name="view">
    /// The view to render, either as a path such as <c>~/Templates/Mail.cshtml</c> or as a view name
    /// resolved by the view engine's own conventions.
    /// </param>
    /// <param name="model">The model the view binds to.</param>
    /// <param name="cancellationToken">Cancels the rendering.</param>
    /// <typeparam name="TModel">The model type.</typeparam>
    /// <returns>The markup the view produced.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="view"/> is null, empty or white space.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no view matches <paramref name="view"/>. The message names the locations that were
    /// searched.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is cancelled.
    /// </exception>
    Task<string> RenderAsync<TModel>(string view, TModel model, CancellationToken cancellationToken = default);
}
