using Terminal.Gui.Drivers;

namespace KustoTerminal.Driver;

/// <summary>
/// Fallback no-op ANSI response parser for when the internal AnsiResponseParser
/// cannot be instantiated via reflection.
/// </summary>
internal sealed class NoOpAnsiResponseParser : IAnsiResponseParser
{
    public AnsiResponseParserState State => AnsiResponseParserState.Normal;

    public void ExpectResponse(string? terminator, Action<string?> response, Action? abandoned, bool persistent = false)
    {
    }

    public bool IsExpecting(string? terminator) => false;

    public void StopExpecting(string? requestTerminator, bool persistent = false)
    {
    }
}
