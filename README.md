# ScryberHotReloader

A WPF application for live editing of HTML and C# models with real-time PDF rendering using [Scryber](https://github.com/scryber).

---

## Features

- Dual editors using [AvalonEdit](https://github.com/icsharpcode/AvalonEdit) for HTML and C# with syntax highlighting.
- Custom syntax highlighting themes for HTML and C#.
- Autocomplete support for HTML tags and C# keywords.
- Real-time PDF generation and preview from edited HTML and models.
- Supports dynamic C# model compilation and binding in templates.
- Keyboard shortcuts for quick save (`Ctrl+S`) and open (`Ctrl+O`).

---

## Getting Started

### Prerequisites

- .NET 6+ or later
- Visual Studio 2022 or newer (for development)
- Scryber PDF library (included or referenced)

### Building

1. Clone the repository.
2. Open the solution in Visual Studio.
3. Build the solution.
4. Run the app.

---

## Usage

- Edit HTML in the **HTML** tab with live syntax highlighting and autocomplete.
- Edit C# model classes in the **Model** tab with syntax highlighting and autocomplete.
- Press `Ctrl+S` to save and recompile the model, then re-generate the PDF preview.
- Press `Ctrl+O` to open existing HTML files.
- The PDF preview updates automatically on save.

---

## File Structure

- `MainWindow.xaml` & `MainWindow.xaml.cs`: Main UI and logic.
- `CSharpDark.xshd` & `HtmlDark.xshd`: Syntax highlighting definitions.
- `Completions/CSCompletionData.cs`: C# autocomplete keyword provider.
- `Completions/HtmlCompletionData.cs`: HTML autocomplete keyword provider.
- `Models/`: (Optional) Folder for C# model class files.

---

## Known Issues

- Model compilation errors are logged in the output; UI feedback is minimal.
- Only basic syntax highlighting and autocomplete for C# and HTML.
- PDF rendering relies on Scryber; errors may occur for complex documents.

---

## Contributions

Feel free to fork and submit pull requests. Open issues for bugs or feature requests.

---

## License

MIT License. See `LICENSE` file.

---

## Contact

Developed by Cameron Stocks.  
Email: camcstocks@gmail.com  
GitHub: [github.com/CameronCS](https://github.com/CameronCS)

