using System;

namespace KustoTerminal.Language.Models;

public interface ITextModel
{
    string GetText(bool normalize = false);
}