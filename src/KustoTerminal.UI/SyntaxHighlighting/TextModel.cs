using System;
using KustoTerminal.Language.Models;
using Terminal.Gui.Views;

namespace KustoTerminal.UI.SyntaxHighlighting
{
    public class TextModel : ITextModel
    {
        private readonly string _text;

        public TextModel(TextView textView)
        {
            _text = textView.Text;
        }

        public TextModel(string text)
        {
            _text = text;
        }

        public string GetText(bool normalize = false)
        {
            var text = _text;
            if (normalize)
            {
                text = text.Replace("\r\n", "\n");
            }

            return text;
        }
    }
}