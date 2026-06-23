# 🚀 Scryber Hot Reloader

Welcome to **Scryber Hot Reloader** — your handy WPF app for live HTML editing, PDF previewing, and C# model binding with syntax highlighting and IntelliSense!  
Designed to speed up your workflow by letting you edit HTML and C# models side-by-side with instant PDF rendering. Perfect for developers who want a smooth, no-fuss editing experience. ✨

---

## 📚 Table of Contents

- [✨ Features](#-features)  
- [🚀 How to Use](#-how-to-use)  
- [💡 Quick Tips](#-quick-tips)  
- [📥 Installation](#-installation)  
- [🔌 Plugin Configuration](#-plugin-configuration)  
- [🤝 Contributing](#-contributing)  
- [📜 License](#-license)  
- [📬 Contact & Thanks](#-contact--thanks)

---

## ✨ Features

- 📝 **Three Editors**: Separate tabs for HTML, C# model, and service Startup code — all with syntax highlighting.  
- 💡 **IntelliSense Autocomplete**: Smart suggestions for HTML tags, C# keywords, plugin type names, and `.` member access.  
- 📄 **Live PDF Preview**: Instantly see your HTML rendered as a PDF on every Ctrl+S.  
- 🔗 **Model Binding**: Use your C# classes as dynamic models to inject data into HTML via `{{ model.Property }}`.  
- 🔌 **Plugin System**: Load services from your existing .NET app at preview time — constructor injection, real DB contexts, zero project changes.  
- 🚀 **Startup Tab**: Write `ConfigureServices` registrations directly in the tool — no new files, no project modifications.  
- 🔍 **Find & Replace**: Ctrl+F to find, Ctrl+H to find and replace, with match count and keyboard navigation.  
- 💾 **File Operations**: Open, save, and save-as for HTML with keyboard shortcuts (Ctrl+S / Ctrl+O).  
- 🌙 **Dark Syntax Themes**: Easy-on-the-eyes coloring for code editors.  
- 🧹 **Automatic Cleanup**: Closes and deletes old PDF previews to keep things tidy.

---

## 🚀 How to Use

1. 🔓 **Open or create** your HTML file in the HTML editor tab.  
2. 👨‍💻 **Edit your C# model** in the Model tab — define a class named `Model` (or any public class) with public members to bind into your HTML.  
3. ✍️ Use the template syntax in your HTML, e.g., `{{ model.Name }}`, to inject model properties dynamically.  
4. 🔌 **Optionally load plugins**: open **Plugins → Manage Plugins...** to point at your app's build output and select DLLs. Then write service registrations in the **Startup tab** — the model will receive real dependencies automatically.  
5. 💾 Save your work with **Ctrl+S** or the Save buttons — the PDF preview updates automatically.  
6. 👀 Preview your rendered PDF in the built-in viewer.

---

## 💡 Quick Tips

- 🔧 Use familiar C# syntax to define your model classes. Name the class `Model` if you have multiple classes in the Model tab.  
- ⚡ Auto-completion helps speed up writing both HTML tags and C# keywords, including types and members from your loaded plugins.  
- 🔍 Press **Ctrl+F** to open Find, **Ctrl+H** to open Find & Replace. Use **Enter** / **Shift+Enter** to navigate matches.  
- 🔌 Plugin types appear in IntelliSense as soon as you load assemblies via **Plugins → Manage Plugins...**  
- 🧹 Closing the app will clean up any temporary PDF files created during your session.

---

## 📥 Installation

- 📂 Clone this repo  
- 🏗️ Build the WPF solution in Visual Studio (.NET 6 or higher recommended)  
- ▶️ Run the app, and start editing!

---

## 📚 Syntax Guide

For detailed instructions on how to write **HTML** and **C#** code for Scryber Hot Reloader, including required HTML tags like `<html lang='en' xmlns='http://www.w3.org/1999/xhtml'>` 🌐, self-closing tags `/>` ✂️, and standard C# syntax 💻, please check out the [Syntax Guide](SyntaxRules.md).

This guide also explains how to include external C# classes using `using` statements 🧩.

---

## 🔌 Plugin Configuration

Scryber Hot Reloader can load services from your existing .NET application so your model code can use real dependencies — data services, business logic, DB contexts — via constructor injection. **No changes to your project are required.**

For the full setup guide, see [PLUGINS.md](PLUGINS.md). The short version:

1. Open **Plugins → Manage Plugins...** and point at your app's build output folder. Browse for your DLLs — transitive dependencies (EF Core, etc.) resolve automatically.
2. Open the **Startup tab** and write your `ConfigureServices` registrations directly in the tool.
3. Write your model with constructor injection — services resolve automatically on every Ctrl+S.

Plugin types appear in IntelliSense in the Model and Startup editors (type names and `.` member access) with no extra configuration.

---

## 🤝 Contributing

Found a bug? 🐞 Have ideas for new features? 💡 Feel free to open issues or pull requests on GitHub. Contributions are very welcome! 🙌

---

## 📜 License

This project is licensed under the [MIT License](LICENSE) — feel free to use, modify, and share with attribution. 🚀

---

## 📬 Contact & Thanks

Crafted with care by **Cameron Stocks** — a passionate coder on a mission to simplify your dev life.  

- 📧 Email: camcstocks@gmail.com  
- 🔗 GitHub: [github.com/CameronCS](https://github.com/CameronCS)  

Thanks for checking this out! Keep coding, keep learning, and have fun! 🎉

---

*Happy coding!* 💻✨
