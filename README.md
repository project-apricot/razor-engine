# ApricotFramework.RazorEngine.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/ApricotFramework.RazorEngine.AspNetCore.svg?label=ApricotFramework.RazorEngine.AspNetCore)](https://www.nuget.org/packages/ApricotFramework.RazorEngine.AspNetCore/)
[![CI](https://github.com/project-apricot/razor-engine/actions/workflows/ci.yml/badge.svg)](https://github.com/project-apricot/razor-engine/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](https://github.com/project-apricot/razor-engine/blob/main/LICENSE)

Renders a Razor view to a **string** instead of to the response, so templated markup can be produced
where there is no response to write to — an e-mail body, the input to a PDF converter, a background
job.

## Install

```bash
dotnet add package ApricotFramework.RazorEngine.AspNetCore
```

## Usage

```csharp
using ApricotFramework.RazorEngine.AspNetCore.Extensions;

// The only call needed. No AddControllersWithViews, no AddRazorPages.
builder.Services.AddRazorEngine();
```

```csharp
using ApricotFramework.RazorEngine.AspNetCore;

public sealed class WelcomeMailer(IRazorEngine razorEngine)
{
    public Task<string> BuildBodyAsync(Customer customer, CancellationToken cancellationToken)
    {
        return razorEngine.RenderAsync("~/Templates/Welcome.cshtml", customer, cancellationToken);
    }
}
```

Address a template **by path** — `~/Templates/Welcome.cshtml` — and it is found wherever it lives. A
bare **view name** goes through the view engine's conventions instead, and because there is no
controller route value to fill in, the only one reachable is `/Views/Shared/{name}.cshtml`. When
nothing matches, the `InvalidOperationException` lists every location that was searched.

Each render gets **its own service scope**, which is what lets the engine run with no request in
flight — and also means a view resolving a scoped service during a request does not share that
request's instance.

Full documentation: <https://projectapricot.dev/docs/razor-engine>
