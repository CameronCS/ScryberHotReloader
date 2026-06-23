# Plugin Configuration Guide

Scryber Hot Reloader can load services from your existing .NET application at preview time, so the C# model you write in the **Model** tab can receive real dependencies — data services, business logic, DB contexts — via constructor injection, exactly as they would in your main app. No changes to your project are required.

---

## How It Works

On every **Ctrl+S**:

1. Plugin assemblies are loaded from `scryber-plugins.json` (if present), making your types available to Roslyn and the IntelliSense engine.
2. The **Startup tab** is compiled. If it contains a `ConfigureServices(IServiceCollection)` method, that builds the DI container.
3. If the Startup tab is empty, the hot reloader falls back to the **convention registrar** — scanning loaded assemblies for `ScryberPluginRegistrar`, `Program`, or `Startup` classes.
4. The Model tab is compiled and instantiated through the DI container — constructor parameters resolve automatically.

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

If you would rather keep the registration inside your own codebase, the hot reloader will automatically discover a `ConfigureServices(IServiceCollection)` static method on any of these classes in the loaded assemblies, checked in this order:

| Class name | When to use |
|---|---|
| `ScryberPluginRegistrar` | Explicit opt-in — add a new class to any assembly |
| `Program` | Add a `public static partial class Program` block to your existing `Program.cs` |
| `Startup` | Existing `Startup.cs` in older-style ASP.NET Core projects — works automatically |

The Startup tab always takes priority over the convention registrar.

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
