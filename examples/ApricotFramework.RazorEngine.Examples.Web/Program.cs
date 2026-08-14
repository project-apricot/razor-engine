using ApricotFramework.RazorEngine.AspNetCore;
using ApricotFramework.RazorEngine.AspNetCore.Extensions;

var builder = WebApplication.CreateBuilder(args);

// The only registration this application makes. Note what is absent: no AddControllersWithViews, no
// AddRazorPages, no MVC setup of any kind. This host serves no views of its own — it only renders
// them to strings.
builder.Services.AddRazorEngine();

var app = builder.Build();

// The library's reason for existing: markup as a value, not as a response. This endpoint happens to
// return it, but it could equally be the body of an e-mail or the input to a PDF converter.
app.MapGet("/render", async (IRazorEngine razorEngine, CancellationToken cancellationToken) =>
{
    var html = await razorEngine.RenderAsync<dynamic>("~/Templates/Example.cshtml", new
    {
        Title = "A rendered page",
        Recipient = "Ada",
        RenderedBy = "ApricotFramework.RazorEngine.AspNetCore",
        Reference = Guid.NewGuid().ToString("D"),
    }, cancellationToken);

    return Results.Content(html, "text/html");
});

// A template found by name instead of by path. This one lives in Views/Shared, which is the only
// convention location reachable here: the default patterns are /Views/{controller}/{name}.cshtml and
// /Views/Shared/{name}.cshtml, and there is no controller route value to fill the first one in. A
// template anywhere else — Templates/, as above — has to be addressed by path.
app.MapGet("/by-name", async (IRazorEngine razorEngine, CancellationToken cancellationToken) =>
{
    var html = await razorEngine.RenderAsync("Notice", new { Title = "Resolved by name" }, cancellationToken);

    return Results.Content(html, "text/html");
});

// What a mistyped view name looks like. The message lists every location that was searched, which is
// the difference between a five-second fix and a debugging session.
app.MapGet("/missing", async (IRazorEngine razorEngine, CancellationToken cancellationToken) =>
{
    try
    {
        var html = await razorEngine.RenderAsync("~/Templates/NotThere.cshtml", new { }, cancellationToken);

        return Results.Content(html, "text/html");
    }
    catch (InvalidOperationException exception)
    {
        return Results.Text(exception.Message, "text/plain");
    }
});

app.Run();
