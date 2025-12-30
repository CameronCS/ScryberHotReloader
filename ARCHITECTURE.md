# Scryber Hot Reloader - Architecture Documentation

## Overview

This document describes the refactored architecture of the Scryber Hot Reloader application, which now follows proper separation of concerns principles with a clean MVVM pattern and dependency injection.

## Project Structure

```
ScryberHotReloader/
├── Services/              # Business logic and external services
│   ├── IFileService.cs
│   ├── FileService.cs
│   ├── ICompilationService.cs
│   ├── CompilationService.cs
│   ├── IPdfService.cs
│   ├── PdfService.cs
│   ├── IExternalPackageService.cs
│   └── ExternalPackageService.cs
├── Models/                # Data structures
│   ├── CompilationResult.cs
│   ├── PdfGenerationResult.cs
│   ├── ScryberConfiguration.cs
│   └── ExternalPackageConfig.cs
├── ViewModels/            # MVVM view models
│   ├── ViewModelBase.cs
│   └── MainViewModel.cs
├── Controls/              # User controls
│   ├── ConfigurationTab.xaml
│   └── ConfigurationTab.xaml.cs
├── Styles/                # XAML resource dictionaries
│   ├── Colors.xaml
│   ├── WindowStyles.xaml
│   ├── MenuStyles.xaml
│   └── TabStyles.xaml
├── Completions/           # IntelliSense providers
│   ├── CS/
│   └── HTML/
├── XSHDFiles/             # Syntax highlighting definitions
├── Assets/                # Static resources
├── App.xaml               # Application entry point
├── App.xaml.cs            # DI configuration
├── MainWindow.xaml        # Main UI
├── MainWindow.xaml.cs     # Main UI code-behind
└── appsettings.json       # Application configuration
```

## Architecture Patterns

### 1. Separation of Concerns

**Services Layer**: Contains all business logic
- `FileService`: File I/O operations (open, save, cleanup)
- `CompilationService`: C# code compilation and model instantiation
- `PdfService`: PDF generation from HTML and models
- `ExternalPackageService`: Loading external assemblies and NuGet packages

**Models Layer**: Data structures and DTOs
- `ScryberConfiguration`: Page settings, fonts, margins
- `CompilationResult`: Result of C# compilation
- `PdfGenerationResult`: Result of PDF generation
- `ExternalPackageConfig`: External package configuration

**ViewModels Layer**: MVVM pattern
- `MainViewModel`: Main application view model with bindable properties

**Views Layer**: XAML UI
- `MainWindow`: Main application window
- `ConfigurationTab`: Configuration user control

### 2. Dependency Injection

The application uses **Microsoft.Extensions.DependencyInjection** for IoC:

```csharp
// In App.xaml.cs
private void ConfigureServices(IServiceCollection services) {
    services.AddSingleton<IFileService, FileService>();
    services.AddSingleton<IExternalPackageService, ExternalPackageService>();
    services.AddSingleton<ICompilationService, CompilationService>();
    services.AddSingleton<IPdfService, PdfService>();
    services.AddSingleton<MainViewModel>();
    services.AddTransient<MainWindow>();
}
```

### 3. MVVM Pattern

The application follows the Model-View-ViewModel pattern:
- **Model**: Data structures in `Models/`
- **View**: XAML files
- **ViewModel**: `MainViewModel` with `INotifyPropertyChanged`

### 4. Resource Dictionaries

Styles are organized into separate resource dictionaries:
- `Colors.xaml`: Color definitions
- `WindowStyles.xaml`: Window control button styles
- `MenuStyles.xaml`: Menu and menu item styles
- `TabStyles.xaml`: Tab control styles

These are merged in `App.xaml` for application-wide use.

## New Features

### 1. Configuration Tab

A new tab in the main window allows users to configure:

**Page Settings:**
- Page size (A4, Letter, Legal, A3, A5, Custom)
- Page orientation (Portrait/Landscape)
- Custom page dimensions (width/height in points)
- Margins (top, bottom, left, right in points)

**Font Settings:**
- Custom font paths (TTF, OTF files)
- One path per line in the text area

**External Packages:**
- View loaded assemblies
- Assemblies configured in `appsettings.json`

### 2. External Package System

You can now include external .NET assemblies and services in your C# models!

#### Configuration

Edit `appsettings.json` in the application directory:

```json
{
  "ExternalPackages": {
    "AssemblyPaths": [
      "C:\\MyProjects\\MyLibrary\\bin\\Debug\\net9.0\\MyLibrary.dll",
      "lib\\CustomPackage.dll"
    ],
    "NuGetPackages": [
      "Newtonsoft.Json",
      "System.Text.Json"
    ]
  }
}
```

#### How It Works

1. **Assembly Paths**: Provide absolute or relative paths to DLL files
2. **NuGet Packages**: Reference packages by name (loaded from global NuGet cache)
3. **Automatic Loading**: Packages are loaded on application startup
4. **Compilation Support**: Loaded assemblies are available for C# model compilation

#### Example Usage

After configuring `Newtonsoft.Json` in `appsettings.json`:

```csharp
using Newtonsoft.Json;
using System.Collections.Generic;

public class MyModel {
    public string GetJsonData() {
        var data = new Dictionary<string, string> {
            { "name", "John" },
            { "email", "john@example.com" }
        };
        return JsonConvert.SerializeObject(data);
    }
}
```

### 3. Improved Styling System

**Before**: All styles defined inline in `MainWindow.xaml` (150+ lines)

**After**: Organized into separate, reusable resource dictionaries:
- Better maintainability
- Easier theming
- Reusable across controls
- Centralized color management

### 4. Service-Oriented Architecture

**Before**: All logic in `MainWindow.xaml.cs` (332 lines, monolithic)

**After**: Separated into focused services:
- `FileService`: 70 lines
- `CompilationService`: 80 lines
- `PdfService`: 60 lines
- `ExternalPackageService`: 120 lines
- `MainViewModel`: 160 lines
- `MainWindow`: 300 lines (mostly UI event handling)

Benefits:
- Testable (can unit test services)
- Maintainable (single responsibility)
- Extensible (easy to add new services)
- Reusable (services can be used in other views)

## Configuration Reference

### appsettings.json

```json
{
  "ExternalPackages": {
    "AssemblyPaths": [
      // Absolute or relative paths to DLLs
    ],
    "NuGetPackages": [
      // NuGet package names
    ]
  },
  "Scryber": {
    "DefaultPageSize": "A4",
    "DefaultOrientation": "Portrait",
    "CustomFonts": [
      // Paths to custom font files
    ]
  }
}
```

### Page Sizes

Standard sizes:
- **A4**: 595 x 842 points (210 x 297 mm)
- **Letter**: 612 x 792 points (8.5 x 11 inches)
- **Legal**: 612 x 1008 points (8.5 x 14 inches)
- **A3**: 842 x 1191 points (297 x 420 mm)
- **A5**: 420 x 595 points (148 x 210 mm)

Note: 72 points = 1 inch

## Development Guidelines

### Adding a New Service

1. Create interface in `Services/IYourService.cs`
2. Implement in `Services/YourService.cs`
3. Register in `App.xaml.cs`:
   ```csharp
   services.AddSingleton<IYourService, YourService>();
   ```
4. Inject into ViewModel or other services

### Adding a New Configuration Option

1. Add property to `ScryberConfiguration.cs`
2. Add UI controls in `ConfigurationTab.xaml`
3. Update `GetConfiguration()` in `ConfigurationTab.xaml.cs`
4. Use in `PdfService.ApplyConfiguration()`

### Adding a New Style

1. Create new `.xaml` file in `Styles/`
2. Define styles with proper resource keys
3. Merge in `App.xaml`:
   ```xaml
   <ResourceDictionary Source="Styles/YourStyle.xaml"/>
   ```

## Benefits of New Architecture

1. **Testability**: Services can be unit tested independently
2. **Maintainability**: Clear separation of concerns
3. **Extensibility**: Easy to add new features
4. **Reusability**: Services and styles can be reused
5. **Configurability**: External packages and settings
6. **Professional**: Follows industry best practices

## Migration Notes

### Breaking Changes

- `MainWindow` now requires `MainViewModel` injection
- Startup sequence changed (no more `StartupUri` in `App.xaml`)
- All business logic moved to services

### Preserved Functionality

All original features still work:
- HTML/C# editing with syntax highlighting
- IntelliSense for HTML and C#
- Real-time PDF preview
- File operations (Open, Save, Save As)
- Custom window chrome
- Keyboard shortcuts (Ctrl+S, Ctrl+O)

## Future Enhancements

Potential areas for expansion:
- More PDF configuration options
- Theme switching (light/dark)
- Recent files list
- Export configuration profiles
- Plugin system for custom renderers
- Advanced IntelliSense with reflection
