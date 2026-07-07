# Plugin Configuration Guide

Scryber Hot Reloader can load services from your existing .NET application at preview time, so the C# model you write in the **Model** tab can receive real dependencies — data services, business logic, DB contexts — via constructor injection, exactly as they would in your main app. No changes to your project are required.

**Requirement:** the hot reloader currently targets **.NET 10**. Your app's target framework must be **.NET 10 or earlier** — a plugin assembly built for a *newer* major .NET version than the hot reloader cannot be loaded, full stop (this is a CLR-level limitation: the runtime hosting the process is a fixed major version, and no amount of assembly-resolution logic can load a newer-major-version assembly into an older-major-version process). If your app targets a newer .NET version than the hot reloader, you'll see a `System.Runtime` (or similar core BCL assembly) `FileNotFoundException` — that's this constraint, not a bug in your app.

---

## How It Works

On every **Ctrl+S**:

1. Plugin assemblies are loaded from `scryber-plugins.json` (if present), making your types available to Roslyn and the IntelliSense engine.
2. The **Startup tab** is compiled. If it contains a `ConfigureServices(IServiceCollection)` method, that builds the DI container.
3. If the Startup tab is empty, the hot reloader falls back to the **convention registrar** — scanning loaded assemblies for `ScryberPluginRegistrar`, `Program`, or `Startup` classes for a `ConfigureServices(IServiceCollection)` method.
4. If none of those exist either — the common case for a real ASP.NET Core app — it falls back further to a **builder factory**: any method on those same classes that builds and returns something shaped like `WebApplicationBuilder` (has a `Services` property and a parameterless `Build()`). If found, it's invoked, `.Build()` is called, and `.Services` becomes the DI container. This is how apps that construct their DI graph in `Program.cs` via `WebApplication.CreateBuilder()` work without any changes at all.
5. The Model tab is compiled and instantiated through the DI container — constructor parameters resolve automatically.

---

## Step 1 — Load Your Assemblies

Open **Plugins → Manage Plugins...** to configure which assemblies to load. No manual JSON editing required.

| Field | Description |
|---|---|
| **Assembly Directory** | Your app's build output folder (e.g. `bin\Debug\net9.0`). Transitive dependencies — EF Core, logging, etc. — are resolved from here automatically without listing them. |
| **Assemblies** | Your own DLLs to load. Click **+ Add Assembly...** to browse for them. |
| **Registrar class** | Optional. Fully-qualified class name if you want to point at a specific registrar. Leave blank to use the convention (see Step 2). |

This saves a `scryber-plugins.json` file next to your HTML template. You can also create or edit it by hand if you prefer:

```json
{
  "assemblyDirectory": "C:\\MyApp\\bin\\Debug\\net9.0",
  "assemblies": [
    "MyApp.Interfaces.dll",
    "MyApp.Data.dll",
    "MyApp.Business.dll"
  ]
}
```

---

## Step 2 — Register Your Services

Open the **Startup tab** in the editor and write your service registrations. This is the recommended approach — no changes to your project at all:

```csharp
using Microsoft.Extensions.DependencyInjection;

public class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IDataService, SqlDataService>();
        services.AddTransient<IBusinessService, BusinessService>();
    }
}
```

The Startup tab is compiled fresh on every Ctrl+S. Changes take effect immediately.

### Alternative — Convention registrar

If you would rather keep the registration inside your own codebase, the hot reloader will automatically discover a `ConfigureServices(IServiceCollection)` method on any of these classes in the loaded assemblies, checked in this order:

| Class name | When to use |
|---|---|
| `ScryberPluginRegistrar` | Explicit opt-in — add a new class to any assembly |
| `Program` | Your existing `Program.cs`, including `internal partial class Program` — no visibility changes needed |
| `Startup` | Existing `Startup.cs` in older-style ASP.NET Core projects — works automatically |

The method can be `public` or `private`, `static` or instance (instance registrars are
instantiated via a parameterless constructor). This means an existing
`private static void ConfigureServices(IServiceCollection services)` on your app's `Program`
class is picked up as-is — no changes required.

If a class with a matching name is found but its registration method doesn't match this
signature (e.g. it takes a `WebApplicationBuilder` instead of `IServiceCollection`), the warning
dialog names the method and its actual signature so you know exactly what to change.

The Startup tab always takes priority over the convention registrar.

### Alternative — Builder factory (real ASP.NET Core apps)

If your app builds services on `WebApplicationBuilder` (or `HostApplicationBuilder`) inside
`Program.cs`, rather than through a `ConfigureServices(IServiceCollection)` method — the standard
`WebApplication.CreateBuilder(args)` pattern — no changes are needed at all. The hot reloader
looks for a method on `ScryberPluginRegistrar`/`Program`/`Startup` that returns a builder-shaped
object and invokes it directly:

```csharp
internal partial class Program
{
    private static WebApplicationBuilder CreateSystemBuilder(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddTransient<IBusinessService, BusinessService>();
        // ... your existing registration methods ...
        return builder;
    }
}
```

The method can be `private`, `internal`, or `public`, static or instance. The hot reloader calls
it, then `.Build()` on the result, and takes `.Services` as the DI container — it never calls
`.Run()` or touches middleware/hosting setup, so `AddControllers`, `AddSignalR`, authentication,
etc. are harmless to leave in place.

**Note:** startup hooks that run separately from builder construction (e.g. database migrations
run after `.Build()` in your own `Program.cs`) are **not** executed by the hot reloader — only
the builder-construction method itself is called. This is intentional: a test/preview tool
silently migrating a database on every Ctrl+S would be a footgun.

---

## Step 3 — Use Services in the Model Tab

Write your model with constructor injection as normal. Name the class `Model` if you have multiple classes in the tab — it will always be used as the Scryber binding target:

```csharp
public class CustomerFetcher
{
    public string Name    { get; }
    public decimal Total  { get; }

    public CustomerFetcher(IBusinessService biz)
    {
        var c = biz.GetCustomer(1);
        Name  = c.Name;
        Total = c.OutstandingBalance;
    }
}

public class Model
{
    public string  CustomerName { get; set; }
    public decimal Balance      { get; set; }

    public Model(IBusinessService biz)
    {
        var data     = new CustomerFetcher(biz);
        CustomerName = data.Name;
        Balance      = data.Total;
    }
}
```

Hit **Ctrl+S** — the PDF preview renders with live data from your services.

---

## IntelliSense

Once plugin assemblies are loaded, the Model and Startup editors pick them up automatically:

- **Type names** — start typing `IBus` and `IBusinessService` appears in the suggestion list alongside C# keywords.
- **Member access** — type `biz.` and the public members of `IBusinessService` appear (methods with full signatures, properties, fields).

The type index rebuilds automatically whenever assemblies are loaded — no restart or manual refresh needed.

---

## Managing Plugins

| Action | How |
|---|---|
| Configure assemblies | **Plugins → Manage Plugins...** — file browser dialog, no path typing |
| Reload without saving | **Plugins → Reload Plugins** |
| Automatic reload | Happens on every Ctrl+S |

---

## Warnings and Errors

Non-fatal issues (assembly not found, registrar threw an exception, etc.) appear as a warning dialog after Ctrl+S. Startup tab compilation errors appear inline with the full Roslyn diagnostic so you can fix them without leaving the app.

---

## Architecture Notes

### Transitive dependencies

When you set `assemblyDirectory` to your app's build output, the hot reloader registers an `AssemblyResolve` handler pointing at that directory. This means EF Core, its database providers, logging libraries, and any other transitive dependencies are found automatically — you only need to list your own DLLs in the config.

### Supported service lifetimes

All three DI lifetimes work (`Transient`, `Scoped`, `Singleton`). A fresh DI scope is created for each model instantiation, so `Scoped` services including EF Core `DbContext` behave correctly.

### Assembly reload

Assemblies are loaded with `Assembly.LoadFrom` into the current AppDomain. If you rebuild your app's DLLs and want the hot reloader to pick up the new versions, restart the app — .NET does not support unloading individual assemblies from the default AppDomain.

---

## Example: Layered Architecture

**`Plugins → Manage Plugins...`** config:

```json
{
  "assemblyDirectory": "C:\\MyApp\\bin\\Debug\\net9.0",
  "assemblies": [
    "MyApp.Interfaces.dll",
    "MyApp.Data.dll",
    "MyApp.Business.dll"
  ]
}
```

**Startup tab**:

```csharp
using Microsoft.Extensions.DependencyInjection;

public class Startup
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<ICustomerRepository, CustomerRepository>();
        services.AddTransient<IInvoiceRepository, InvoiceRepository>();
        services.AddTransient<ICustomerService, CustomerService>();
        services.AddTransient<IInvoiceService, InvoiceService>();
    }
}
```

**Model tab**:

```csharp
public class Model
{
    public string CustomerName { get; set; }
    public decimal Balance     { get; set; }

    public Model(IInvoiceService invoices)
    {
        var draft    = invoices.GetDraft();
        CustomerName = draft.CustomerName;
        Balance      = draft.Total;
    }
}
```
