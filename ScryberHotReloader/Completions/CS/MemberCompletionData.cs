using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Reflection;
using System.Windows.Media;

namespace ScryberHotReloader.Completions.CS;

internal sealed class MemberCompletionData : ICompletionData {
    private readonly string _insertText;
    private readonly string _displayText;
    private readonly string _description;

    public MemberCompletionData(MemberInfo member) =>
        (_displayText, _insertText, _description) = Format(member);

    public ImageSource? Image => null;
    // Text drives filtering as user types after the dot — just the identifier, no signature
    public string Text => _insertText;
    public object Content => _displayText;
    public object Description => _description;
    public double Priority => 1.0;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs _) =>
        textArea.Document.Replace(completionSegment, _insertText);

    private static (string display, string insert, string description) Format(MemberInfo member) {
        switch (member) {
            case MethodInfo m: {
                string paramList = string.Join(", ", m.GetParameters()
                    .Select(p => $"{Friendly(p.ParameterType)} {p.Name}"));
                return (
                    $"{m.Name}({paramList})",
                    m.Name,  // Insert just the name — user types ( to complete the call
                    $"{Friendly(m.ReturnType)} {m.Name}({paramList})"
                );
            }
            case PropertyInfo p:
                return (p.Name, p.Name, $"{Friendly(p.PropertyType)} {p.Name}");

            case FieldInfo f:
                return (f.Name, f.Name, $"{Friendly(f.FieldType)} {f.Name}");

            default:
                return (member.Name, member.Name, member.Name);
        }
    }

    private static string Friendly(Type t) {
        if (!t.IsGenericType) return t.Name;
        string baseName = t.Name[..t.Name.IndexOf('`')];
        string args = string.Join(", ", t.GetGenericArguments().Select(Friendly));
        return $"{baseName}<{args}>";
    }
}
