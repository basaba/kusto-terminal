using System;
using KustoTerminal.Language.Models;
using Terminal.Gui.Views;

namespace KustoTerminal.UI.SyntaxHighlighting
{
    public class TextModel : ITextModel
    {
        private readonly TextView _textView;

        public TextModel(TextView textView)
        {
            _textView = textView;
        }

        public string GetText()
        {
            return _textView.Text;
        }
    }
}