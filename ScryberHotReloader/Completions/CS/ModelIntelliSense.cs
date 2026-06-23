using ICSharpCode.AvalonEdit.CodeCompletion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Reflection;

namespace ScryberHotReloader.Completions.CS;

internal static class ModelIntelliSense {
    // Rebuilt automatically whenever a new assembly is loaded into the AppDomain
    // (e.g. when the user reloads plugins or Ctrl+S compiles a model).
    private static Type[]? _typeCache;

    static ModelIntelliSense() {
        AppDomain.CurrentDomain.AssemblyLoad += (_, _) => _typeCache = null;
    }

    /// <summary>
    /// Public types from all loaded assemblies whose name starts with <paramref name="prefix"/>.
    /// </summary>
    public static IEnumerable<ICompletionData> GetTypeCompletions(string prefix) {
        if (string.IsNullOrEmpty(prefix)) return [];

        return AllTypes()
            .Where(t => t.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(t => t.FullName)
            .Select(t => (ICompletionData)new TypeCompletionData(t));
    }

    /// <summary>
    /// Public instance members of the type that <paramref name="expressionName"/> resolves to
    /// inside <paramref name="source"/>, resolved via Roslyn syntax + reflection.
    /// </summary>
    public static IEnumerable<ICompletionData> GetMemberCompletions(string source, string expressionName) {
        if (string.IsNullOrEmpty(expressionName)) return [];

        Type? type = ResolveType(source, expressionName);
        if (type == null) return [];

        return type
            .GetMembers(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m is MethodInfo or PropertyInfo or FieldInfo)
            // Skip compiler-generated property accessors (get_X / set_X)
            .Where(m => !m.Name.StartsWith("get_", StringComparison.Ordinal)
                     && !m.Name.StartsWith("set_", StringComparison.Ordinal))
            .DistinctBy(m => m.Name)
            .Select(m => (ICompletionData)new MemberCompletionData(m));
    }

    // -------------------------------------------------------------------------
    // Type resolution via Roslyn syntax tree + reflection

    private static Type? ResolveType(string source, string name) {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();

        // 1. Field / local variable declarations:  TypeName varName  or  var varName = ...
        foreach (var decl in root.DescendantNodes().OfType<VariableDeclarationSyntax>()) {
            foreach (var v in decl.Variables) {
                if (v.Identifier.Text != name) continue;
                string typeName = decl.Type.ToString().TrimEnd('?');
                if (typeName == "var") {
                    // Try to infer from object creation: var x = new SomeType(...)
                    if (v.Initializer?.Value is ObjectCreationExpressionSyntax oc)
                        typeName = oc.Type.ToString().TrimEnd('?');
                    else continue;
                }
                var found = FindType(typeName);
                if (found != null) return found;
            }
        }

        // 2. Constructor / method parameters:  TypeName paramName
        foreach (var param in root.DescendantNodes().OfType<ParameterSyntax>()) {
            if (param.Identifier.Text != name) continue;
            var found = FindType((param.Type?.ToString() ?? "").TrimEnd('?'));
            if (found != null) return found;
        }

        // 3. Properties:  TypeName PropName { get; set; }
        foreach (var prop in root.DescendantNodes().OfType<PropertyDeclarationSyntax>()) {
            if (prop.Identifier.Text != name) continue;
            var found = FindType(prop.Type.ToString().TrimEnd('?'));
            if (found != null) return found;
        }

        return null;
    }

    private static Type? FindType(string typeName) {
        if (string.IsNullOrWhiteSpace(typeName)) return null;
        // Strip generic arity suffix if present (e.g. List`1)
        string simple = typeName.Split('.').Last();
        return AllTypes().FirstOrDefault(t =>
            t.Name == typeName  ||
            t.FullName == typeName ||
            t.Name == simple);
    }

    private static Type[] AllTypes() {
        return _typeCache ??= AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic)
            .SelectMany(a => { try { return a.GetExportedTypes(); } catch { return []; } })
            .ToArray();
    }
}
