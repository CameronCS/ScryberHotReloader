using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Windows.Media;

namespace ScryberHotReloader.Completions.CS {
    public class CSCompletionData(string text) : ICompletionData {
        public ImageSource? Image => null;

        public string Text { get; private set; } = text;

        public object Content => Text;

        public object Description => "C# keyword";

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs) => textArea.Document.Replace(completionSegment, Text);
        
    }
}
