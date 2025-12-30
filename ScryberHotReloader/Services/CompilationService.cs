using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ScryberHotReloader.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ScryberHotReloader.Services {
    public class CompilationService : ICompilationService {
        private readonly IExternalPackageService _externalPackageService;

        public CompilationService(IExternalPackageService externalPackageService) {
            _externalPackageService = externalPackageService;
        }

        public async Task<CompilationResult> CompileAndInstantiateModelAsync(string sourceCode) {
            return await Task.Run(() => {
                if (string.IsNullOrEmpty(sourceCode)) {
                    return new CompilationResult { Success = true, ModelInstance = null };
                }

                try {
                    SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

                    // Get base references
                    List<MetadataReference> refs = AppDomain.CurrentDomain
                        .GetAssemblies()
                        .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                        .Select(a => MetadataReference.CreateFromFile(a.Location))
                        .Cast<MetadataReference>()
                        .ToList();

                    // Add external package references
                    var externalRefs = _externalPackageService.GetMetadataReferences();
                    refs.AddRange(externalRefs);

                    CSharpCompilation compilation = CSharpCompilation.Create(
                        "DynamicModelAssembly",
                        [syntaxTree],
                        refs,
                        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    );

                    using MemoryStream ms = new();
                    var emitResult = compilation.Emit(ms);

                    if (!emitResult.Success) {
                        string errors = string.Join("\n",
                            emitResult.Diagnostics
                                .Where(d => d.Severity == DiagnosticSeverity.Error)
                                .Select(d => d.ToString())
                        );

                        return new CompilationResult {
                            Success = false,
                            ErrorMessage = $"Model compilation failed:\n\n{errors}"
                        };
                    }

                    ms.Seek(0, SeekOrigin.Begin);
                    Assembly assembly = Assembly.Load(ms.ToArray());

                    Type? modelType = assembly.GetTypes()
                        .FirstOrDefault(t => t.GetConstructor([]) != null);

                    if (modelType == null) {
                        return new CompilationResult {
                            Success = false,
                            ErrorMessage = "No suitable public class with a parameterless constructor was found in the model."
                        };
                    }

                    object? instance = Activator.CreateInstance(modelType);

                    return new CompilationResult {
                        Success = true,
                        ModelInstance = instance
                    };
                } catch (Exception ex) {
                    return new CompilationResult {
                        Success = false,
                        ErrorMessage = $"Compilation error: {ex.Message}"
                    };
                }
            });
        }
    }
}
