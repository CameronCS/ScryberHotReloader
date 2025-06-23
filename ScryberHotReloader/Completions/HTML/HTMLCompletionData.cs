using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Windows.Media;

namespace ScryberHotReloader.Completions.HTML {
    public class HTMLCompletionData(string text) : ICompletionData {
        public ImageSource? Image => null;

        public string Text { get; private set; } = text;

        public object Content => Text;

        public object Description => $"HTML tag <{Text}>";

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) {
            textArea.Document.Replace(completionSegment, Text);
            int caretOffset = textArea.Caret.Offset;
            textArea.Document.Insert(caretOffset, $"</{Text}>");
            textArea.Caret.Offset = caretOffset;
        }
    }
}