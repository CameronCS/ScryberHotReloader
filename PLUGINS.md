# Plugin Configuration Guide

Scryber Hot Reloader can load services from your existing .NET application at preview time, so the C# model you write in the **Model** tab can receive real dependencies — data services, business logic, DB contexts — via constructor injection, exactly as they would in your main app.

---

## How It Works

1. You drop a `scryber-plugins.json` file next to your HTML template.
2. The hot reloader reads that file on every **Ctrl+S**, loads the listed assemblies into the process, and calls your `ScryberPluginRegistrar.ConfigureServices` method.
3. A DI container is built from those registrations.
4. When your model class is compiled, it is instantiated through that container — constructor parameters are resolved automatically.
5. The loaded types are also indexed for IntelliSense, so interface names and member completions appear in the Model editor immediately.

---

## Step 1 — Create `scryber-plugins.json`

Place this file in the **same directory as your HTML template file** (or in the app's working directory as a fallback).

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

| Field | Required | Description |
|---|---|---|
| `assemblyDirectory` | No | Base path for resolving relative assembly names. Defaults to the folder containing `scryber-plugins.json`. Point this at your app's build output so transitive dependencies (EF Core, etc.) are already present. |
| `assemblies` | Yes | List of DLL filenames (or absolute paths) to load. |
| `registrar` | No | Fully-qualified class name of the registrar, e.g. `"MyApp.Business.ScryberPluginRegistrar"`. Omit to use the convention (see Step 2). |

---

## Step 2 — Add a Registrar Class

Add a class named `ScryberPluginRegistrar` to **any one of the listed assemblies**. The hot reloader discovers it by name — no interface or NuGet reference needed.

```csharp
using Microsoft.Extensions.DependencyInjection;

public class ScryberPluginRegistrar
{
    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IDataService, SqlDataService>();
        services.AddTransient<IBusinessService, BusinessService>();
    }
}
```

The method must be `public static void ConfigureServices(IServiceCollection services)`.

> If you prefer to keep the registrar in a dedicated assembly not listed under `assemblies`, specify it explicitly with the `registrar` field in the config.

---

## Step 3 — Use Services in the Model Tab

Write your model class with constructor injection as you normally would:

```csharp
public class InvoiceModel
{
    public string CustomerName { get; set; }
    public decimal TotalAmount  { get; set; }

    public InvoiceModel(IBusinessService biz)
    {
        var invoice = biz.GetLatestInvoice(customerId: 1);
        CustomerName = invoice.CustomerName;
        TotalAmount  = invoice.Total;
    }
}
```

Hit **Ctrl+S** — the PDF preview renders with live data from your service.

---

## IntelliSense

Once the plugin assemblies are loaded, the Model editor's autocomplete picks them up automatically:

- **Type names** — start typing `IBus` and `IBusinessService` appears in the suggestion list alongside C# keywords.
- **Member access** — type `biz.` and the public members of `IBusinessService` appear (methods with their signatures, properties, fields).

No restart or manual refresh is needed; the type index rebuilds whenever assemblies are loaded.

---

## Reloading Plugins

Plugins are reloaded automatically on every **Ctrl+S**. You can also trigger a manual reload — and see any warnings — via **Plugins → Reload Plugins** in the menu bar.

To open the config file directly in your default editor: **Plugins → Open Config...**

If no `scryber-plugins.json` is found, the hot reloader falls back to the original behaviour (parameterless constructor, no injected services).

---

## Warnings and Errors

Non-fatal issues (e.g. an assembly path that doesn't exist, or a registrar that throws during setup) are shown in a warning dialog after Ctrl+S rather than silently failing, so you can diagnose config problems without leaving the app.

---

## Architecture Notes

### Supported service lifetimes

All three DI lifetimes are supported (`Transient`, `Scoped`, `Singleton`). Because model instantiation creates a fresh DI scope on every save, `Scoped` services (including EF Core `DbContext`) behave correctly.

### Assembly loading

Assemblies are loaded with `Assembly.LoadFrom`, which registers them in the current AppDomain. This means:

- Roslyn picks up the plugin types automatically when compiling your model code (no extra reference configuration needed).
- If you change a plugin DLL on disk and want the hot reloader to pick up the new version, restart the app — .NET does not support unloading individual assemblies in the default AppDomain.

### Multiple registrar classes

Only the first `ScryberPluginRegistrar` found (scanning assemblies in the order listed) is called. If you need registrations split across assemblies, consolidate them into one registrar or use the `registrar` field to point at a specific class.

---

## Example: Layered Architecture

A typical setup for a project with separate Interface, Data, and Business assemblies:

**`scryber-plugins.json`**
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

**Registrar (inside `MyApp.Business.dll`)**
```csharp
using Microsoft.Extensions.DependencyInjection;

public class ScryberPluginRegistrar
{
    public static void ConfigureServices(IServiceCollection services)
    {
        // Data layer
        services.AddTransient<ICustomerRepository, CustomerRepository>();
        services.AddTransient<IInvoiceRepository, InvoiceRepository>();

        // Business layer
        services.AddTransient<ICustomerService, CustomerService>();
        services.AddTransient<IInvoiceService, InvoiceService>();
    }
}
```

**Model tab**
```csharp
public class InvoiceModel
{
    public string CustomerName { get; set; }
    public List<LineItem> Lines { get; set; }

    public InvoiceModel(IInvoiceService invoices)
    {
        var data = invoices.GetDraft();
        CustomerName = data.CustomerName;
        Lines        = data.Lines;
    }
}
```
