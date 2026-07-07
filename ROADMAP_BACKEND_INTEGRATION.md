# Roadmap: Integrating Real Backend Systems

Goal: make existing ASP.NET Core backends (layered architecture, `WebApplicationBuilder`-based
DI, EF Core, SignalR, etc.) easy to plug into the hot reloader for testing Scryber templates
against real services and data — ideally with **no changes** to the target project.

Motivated by evaluating `BrokerCommunicationSystem` as a test case
(`C:\Users\USER-PC\source\repos\Azure Projects\BrokerCommunicationSystem`): its DI registration
is correct and idiomatic, but shaped in a way `PluginLoader` doesn't currently recognize
(`WebApplicationBuilder`-based, private/internal methods, no `ConfigureServices(IServiceCollection)`
anywhere). See Phase 1–2 below for the fix.

---

## Phase 1 — Widen the current convention (low effort) — ✅ Done

- `InvokeRegistrar`'s `GetMethod` call uses default binding flags, so it silently misses
  `private`/`internal` methods on `internal partial class Program`-style code. Added
  `BindingFlags.Public | NonPublic | Static | Instance` to the scan (`PluginLoader.cs`,
  `RegistrarMethodFlags`). Instance methods are instantiated via a parameterless constructor.
- When no candidate matches, near-misses are now reported instead of a generic "not found"
  warning — e.g. "found `Program.RegisterBusinessServices(WebApplicationBuilder)` — unsupported
  signature, expected `(IServiceCollection)`" (`PluginLoader.FindNearMisses`). Turns a dead end
  into a one-line fix for the target project's owner.
- `PLUGINS.md` updated to document that registrar methods no longer need to be `public static`.

Note: this alone does not make `BrokerCommunicationSystem` loadable — its registration methods
take `WebApplicationBuilder`, not `IServiceCollection`, so they now surface as a clear near-miss
warning rather than silently failing. Actually wiring it up needs Phase 2.

## Phase 2 — Recognize the `WebApplicationBuilder` shape directly (the real fix) — ✅ Done

Most real ASP.NET Core backends build services on `WebApplicationBuilder` (for
`builder.Configuration`, environment, etc.), not raw `IServiceCollection`. Rather than requiring
every target project to add a Scryber-specific shim:

- `PluginLoader.BuildFromRegistrar` now falls back to `BuildFromWebHostFactory` when no
  `ConfigureServices(IServiceCollection)` method is found. It scans the same candidate types
  (`ScryberPluginRegistrar`/`Program`/`Startup`) for a method that returns something shaped like a
  builder — return type name contains `"Builder"`, 0 or 1 (`string[]`) parameters, and the return
  type itself exposes a parameterless `Build()`. This is entirely duck-typed reflection, so
  `ScryberHotReloader.csproj` needs **no** `FrameworkReference` to `Microsoft.AspNetCore.App`.
- The method is invoked, `.Services` (if present and of type `IServiceCollection`) gets the
  `HttpResults` singleton added *before* `.Build()` is called, then `.Build()` is invoked and
  `.Services` off the result (duck-typed to `IServiceProvider`) becomes the DI container.
  `.Run()`/`MapHub`/middleware are never touched — resolving this way only needs the DI
  container, not the HTTP pipeline, which is what sidesteps Phase 3 for free.
- Near-miss warnings from the `ConfigureServices` scan are only surfaced if **both** fallbacks
  fail — verified in testing: when the builder-factory path succeeds, no confusing
  "found `Program.RegisterBusinessServices(WebApplicationBuilder)` — unsupported signature"
  warning is shown, since the app loaded successfully via a different route.
- **Open design question — resolved as: off by default.** Startup hooks that run *after*
  `.Build()` in the target's own `Program.cs` (e.g. `RunAllMigrations`) are never invoked, since
  only the builder-construction method itself is called. Documented in `PLUGINS.md` as
  intentional — a test tool silently migrating a dev database on every Ctrl+S would be a footgun.
  Revisit later if a real need for opt-in migrations surfaces.

**Verified with a standalone reflection harness** (not the live `BrokerCommunicationSystem`,
which needs a real DB connection): a throwaway plugin assembly mirroring its exact shape
(`internal partial class Program`, `private static FakeBuilder CreateSystemBuilder(string[] args)`,
a builder type with `Services`/`Build()`) was loaded and its `IGreeter` service resolved and
invoked successfully through `PluginLoader.BuildFromRegistrar` with zero changes to the plugin
code. Not yet tried against the real `BrokerCommunicationSystem` project — that needs a decision
about pointing at a real (or dev) SQL connection string first.

## Phase 3 — Handle hosting-only calls gracefully — ✅ Done (verified, one real bug fixed)

`AddControllers` / `AddSignalR` / etc. are harmless to register (they just add services nothing
will invoke). Phase 2's "build the builder, stop before `.Run()`" approach sidesteps this for
free — confirmed below, no filtering code needed.

**Verification:** the Phase 2 test used a hand-rolled duck-typed `FakeBuilder`/`FakeHost`, which
proved the reflection logic but not whether *real* ASP.NET Core types behave the same way. Built
a second, more faithful scratch plugin (`RealFakePlugin`, `Microsoft.NET.Sdk.Web`, real
`WebApplicationBuilder`) that mirrors `BrokerCommunicationSystem`'s registration shape:
`builder.Services.AddControllers()`, `.AddSignalR()`, `.AddAuthentication().AddJwtBearer()`, a
real EF Core `DbContext` (`UseInMemoryDatabase`, to avoid needing a live SQL connection), plus a
plain business service. Ran it through the actual `PluginLoader.LoadAssembliesOnly` →
`BuildFromRegistrar` path (not a simulation) — result: the DI container built with **zero
warnings**, and both the business service and the `DbContext` resolved and worked correctly.
Confirms the core Phase 3 claim: hosting-only registrations are harmless as long as `.Run()`/
middleware/`Map*` are never invoked.

**Real bug found and fixed along the way:** `FindNearMisses` (Phase 1) and `FindBuilderFactory`
(Phase 2) called `m.GetParameters()` / `m.ReturnType` on *every* method of a candidate type with
no exception handling. Reflecting over a method whose signature references an assembly that
isn't resolvable in the host process throws (`FileNotFoundException`/`TypeLoadException`) — and
with a real multi-package ASP.NET Core app, some method somewhere on `Program` almost always
triggers this. Before the fix, this crashed the entire plugin-load flow with an unhandled
exception instead of falling through to a warning. Fixed by wrapping per-method reflection access
in `PluginLoader.cs` (`SafeGetMethods` + try/catch around each method's signature inspection) so
an unresolvable method is skipped rather than crashing the scan. This mirrors the existing
`ReflectionTypeLoadException` handling around `assembly.GetTypes()` — same category of problem,
now handled consistently at the method level too.

**Hard blocker found (not a bug, a real constraint) — read before testing the real BCS:**
Loading a plugin assembly is only possible when the *host process's* .NET major version is ≥ the
plugin's target major version. Attempting to reflectively load a `net10.0`-targeted assembly into
a running `net9.0` process fails immediately on core BCL assemblies (e.g.
`System.Runtime, Version=10.0.0.0` "cannot find the file") — this is a fundamental CLR/CoreCLR
limitation (the runtime itself is a fixed major version per process), not something any
`AssemblyResolve` handler can work around. Confirmed by retargeting the test harness from
`net9.0-windows` to `net10.0-windows`: the identical plugin then loaded successfully.

**Concrete implication — ✅ Fixed 2026-07-07:** `ScryberHotReloader.csproj` retargeted from
`net9.0-windows` to `net10.0-windows` (matches `BrokerCommunicationSystem`'s `net10.0` and the
`Microsoft.WindowsDesktop.App 10.0.9` already installed on this machine; the `Microsoft.Extensions.*`
package references were already pinned to `10.0.0`). Build succeeds with 0 errors. Re-ran the
`RealFakePlugin` verification harness against the actual rebuilt `ScryberHotReloader.dll` (not
just a simulation) — same clean result as before: DI container built with zero warnings,
business service and DbContext both resolved correctly.

This was also independently confirmed the hard way: while this was still open, the user hit the
exact predicted `System.Runtime, Version=10.0.0.0` `FileNotFoundException` testing a real Model
tab against BCS's `IUserService`. Fixed a related robustness gap found at the same time:
`MainWindow.CompileAndRunModel`'s `assembly.GetTypes()` (line ~544) had the same unguarded-reflection
issue as the Phase 3 bug in `PluginLoader` (see above) but had never been fixed there — it crashed
the whole app with an unhandled `ReflectionTypeLoadException` instead of showing the usual warning
dialog. Fixed with the same catch-and-fall-back-to-partial-types pattern, now showing a
`ClipboardMessageBox` warning naming the loader exceptions instead of crashing.

**Still open:** the working-directory/`appsettings.json` caveat from `startup.txt` — BCS's
`WebApplication.CreateBuilder(args)` resolves `appsettings.json` relative to the *host process's*
current directory, not BCS's own folder, so connection strings won't load without a fix (e.g.
temporarily `Directory.SetCurrentDirectory()` to `assemblyDirectory` around the factory
invocation). Not yet implemented.

## Phase 4 — Plugin Manager UX for ambiguity

Layered apps often have multiple partial `Program` files or several candidate registration
methods. Instead of "first match wins," let the user pick from a discovered list in
`PluginManagerWindow` when more than one candidate is found, instead of silently wiring up the
wrong one.

## Phase 5 — Testing workflow (already mostly in place)

Once a provider is in hand, the existing `IScryberRunner` + constructor-injection +
`ActivatorUtilities.CreateInstance` path (`MainWindow.xaml.cs: CompileAndRunModel`) is the actual
"test with real data" harness — a runner takes an injected service (`IMessageService`,
`IExcelService`, a DbContext-backed repository, etc.) and renders a PDF from live data. No new
work needed here; it already generalizes once Phases 1–2 land.

---

## Priority

Start with **Phase 1** (near-zero risk, unblocks `BrokerCommunicationSystem` today) then
**Phase 2** (generalizes to any similarly-shaped ASP.NET Core project, not just this one).
Phases 3–5 follow naturally once 1–2 are in place.

---

## Changelog

### 2026-07-06 — Phase 1

- `ScryberHotReloader/PluginLoader.cs`
  - Added `RegistrarMethodFlags` constant
    (`Public | NonPublic | Static | Instance`) and used it in both the explicit-registrar and
    convention-registrar lookups in `InvokeRegistrar`, so `private`/`internal` methods are
    discoverable.
  - `InvokeRegistrar` now instantiates the declaring type via a parameterless constructor when
    the matched method is an instance method, instead of assuming `static`.
  - Added `FindNearMisses(Type)` — detects methods with a single `*ServiceCollection*`/`*Builder*`
    parameter that didn't match the expected signature, and surfaces them as a warning naming the
    method and its actual signature.
- `PLUGINS.md`
  - Updated the "Alternative — Convention registrar" section: registrar methods can now be
    private/instance, `Program` no longer needs to be `public`, and signature mismatches produce
    a specific warning instead of a generic failure.

### 2026-07-06 — Phase 2

- `ScryberHotReloader/PluginLoader.cs`
  - Extracted `FindCandidateTypes(assemblies, explicitRegistrar)` — shared candidate-type
    resolution (explicit type, or `ScryberPluginRegistrar`/`Program`/`Startup` convention) used by
    both the `ConfigureServices` scan and the new builder-factory scan.
  - `InvokeRegistrar` refactored to iterate `FindCandidateTypes` instead of duplicating the
    explicit-vs-convention branching inline; behavior unchanged.
  - Added `BuildFromWebHostFactory` + `FindBuilderFactory` — the `WebApplicationBuilder`-shaped
    fallback described above.
  - `BuildFromRegistrar` now chains: `ConfigureServices` convention → builder-factory fallback →
    combined warning if both fail.
- `PLUGINS.md`
  - Documented the builder-factory fallback as step 4 of "How It Works" and added an
    "Alternative — Builder factory (real ASP.NET Core apps)" section with a `Program.cs` example
    mirroring `BrokerCommunicationSystem`'s actual shape.

### 2026-07-06 — Phase 3

- `ScryberHotReloader/PluginLoader.cs`
  - Added `SafeGetMethods(Type)` and wrapped the per-method signature inspection in both
    `FindNearMisses` and `FindBuilderFactory` in try/catch, so a method whose parameter/return
    type can't be resolved in the host process is skipped instead of crashing the whole scan.
    Found via testing against a real (not duck-typed) ASP.NET Core plugin assembly.
- No other code changes — Phase 3's original premise (hosting-only registrations are harmless
  once `.Run()`/`Map*` are never called) held up under a real `WebApplicationBuilder` with
  `AddControllers`/`AddSignalR`/`AddAuthentication().AddJwtBearer()`/EF Core `DbContext`.
- **Not yet done, needs a decision:** upgrade `ScryberHotReloader.csproj`'s `TargetFramework`
  from `net9.0-windows` to `net10.0-windows` to match `BrokerCommunicationSystem`. Discovered to
  be a hard blocker during Phase 3 verification (see above) — the host process's major .NET
  version must be ≥ the target plugin's, which is a CLR-level constraint, not a `PluginLoader`
  bug.
