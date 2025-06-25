
# Scryber Hot Reloader Syntax Guide

## HTML Syntax

- The root `<html>` tag **must** include:
  ```html
  <html lang="en" xmlns="http://www.w3.org/1999/xhtml">
  ```
- All self-closing tags **must** end with `/>`, for example:
  ```html
  <img src="image.png" alt="Image description" />
  <br />
  <input type="text" />
  ```
- Tags must be properly nested and closed.
- Attribute values should use double quotes (`""`) but single quotes (`''`) are also allowed.
- Comments use standard HTML syntax:
  ```html
  <!-- This is a comment -->
  ```

## C# Syntax

- Use standard C# syntax as defined by the language specification.
- The model code should define a **public class** with a **parameterless constructor**.
- You can use namespaces, properties, methods, and all regular C# features.
- Example minimal model class:
  ```csharp
  public class MyModel
  {
      public string Title { get; set; } = "Default Title";
  }
  ```
- If you want to use external classes or namespaces, include appropriate `using` statements at the top of your code:
  ```csharp
  using System;
  using System.Collections.Generic;
  ```
- The model is compiled dynamically — syntax errors or missing references will cause compile errors.
- Avoid external dependencies unless they are referenced and available in the application domain.

---

## Additional Notes

- The dynamic compilation references all assemblies loaded in the current application domain.
- Make sure your model class is public and has a parameterless constructor for instantiation.
- Follow standard C# coding conventions for best compatibility.

---

**Happy coding with Scryber Hot Reloader!**
