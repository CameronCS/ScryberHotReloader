using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Windows.Media;

namespace ScryberHotReloader.Completions.CS;

internal sealed class TypeCompletionData : ICompletionData {
    private readonly Type _type;

    public TypeCompletionData(Type type) => _type = type;

    public ImageSource? Image => null;
    public string Text => _type.Name;
    public object Content => _type.Name;
    public object Description => $"{_type.FullName}\n{KindLabel()}";
    public double Priority => 0.8;

    public void Complete(TextArea textArea, ISegment completionSegment, EventArgs _) =>
        textArea.Document.Replace(completionSegment, _type.Name);

    private string KindLabel() => _type switch {
        { IsInterface: true } => "Interface",
        { IsEnum: true }      => "Enum",
        { IsValueType: true } => "Struct",
        { IsAbstract: true }  => "Abstract class",
        _                     => "Class"
    };
}
