namespace KustoTerminal.Driver.Input;

/// <summary>
/// State machine for parsing ANSI escape sequences from raw stdin bytes.
/// Handles UTF-8 decoding, CSI sequences, SS3 sequences, SGR mouse reports,
/// and bracketed paste detection.
/// </summary>
internal sealed class AnsiSequenceReader
{
    private readonly byte[] _seqBuffer = new byte[64];
    private int _seqLen;
    private State _state;

    private enum State
    {
        Ground,
        Escape,      // Got ESC
        CsiEntry,    // Got ESC [
        CsiParam,    // Reading CSI parameters
        SsThree,     // Got ESC O
    }

    public enum SequenceType
    {
        None,
        Char,          // Regular character (codepoint in Codepoint)
        CsiSequence,   // CSI sequence (params + final char)
        Ss3Sequence,   // SS3 sequence (final char)
        EscapeOnly,    // Bare escape key
    }

    public struct ParsedSequence
    {
        public SequenceType Type;
        public int Codepoint;          // For Char type
        public byte FinalByte;         // For CSI/SS3
        public ReadOnlySpan<byte> Parameters => _params.AsSpan(0, _paramLen);

        internal byte[] _params;
        internal int _paramLen;
    }

    /// <summary>
    /// Feed bytes from stdin into the parser.
    /// Returns parsed sequences. Call repeatedly until all input is consumed.
    /// </summary>
    public int Parse(ReadOnlySpan<byte> input, Span<ParsedSequence> output)
    {
        int outputCount = 0;
        int i = 0;

        while (i < input.Length && outputCount < output.Length)
        {
            byte b = input[i++];

            switch (_state)
            {
                case State.Ground:
                    if (b == 0x1b)
                    {
                        _state = State.Escape;
                        _seqLen = 0;
                    }
                    else if (b < 0x80)
                    {
                        // ASCII character
                        output[outputCount++] = new ParsedSequence
                        {
                            Type = SequenceType.Char,
                            Codepoint = b
                        };
                    }
                    else
                    {
                        // UTF-8 multi-byte start
                        int needed = GetUtf8Length(b);
                        if (needed > 0 && i + needed - 1 <= input.Length)
                        {
                            int cp = DecodeUtf8(b, input[i..], needed - 1);
                            i += needed - 1;
                            output[outputCount++] = new ParsedSequence
                            {
                                Type = SequenceType.Char,
                                Codepoint = cp
                            };
                        }
                    }
                    break;

                case State.Escape:
                    if (b == '[')
                    {
                        _state = State.CsiEntry;
                    }
                    else if (b == 'O')
                    {
                        _state = State.SsThree;
                    }
                    else
                    {
                        // Alt+key or unrecognized escape
                        _state = State.Ground;
                        output[outputCount++] = new ParsedSequence
                        {
                            Type = SequenceType.EscapeOnly,
                            Codepoint = b
                        };
                    }
                    break;

                case State.CsiEntry:
                case State.CsiParam:
                    if (_seqLen < _seqBuffer.Length)
                        _seqBuffer[_seqLen++] = b;

                    if (b >= 0x40 && b <= 0x7E) // Final byte
                    {
                        _state = State.Ground;
                        var paramBytes = new byte[_seqLen - 1];
                        Array.Copy(_seqBuffer, paramBytes, _seqLen - 1);
                        output[outputCount++] = new ParsedSequence
                        {
                            Type = SequenceType.CsiSequence,
                            FinalByte = b,
                            _params = paramBytes,
                            _paramLen = _seqLen - 1
                        };
                        _seqLen = 0;
                    }
                    else
                    {
                        _state = State.CsiParam;
                    }
                    break;

                case State.SsThree:
                    _state = State.Ground;
                    output[outputCount++] = new ParsedSequence
                    {
                        Type = SequenceType.Ss3Sequence,
                        FinalByte = b
                    };
                    break;
            }
        }

        return outputCount;
    }

    /// <summary>
    /// Call when poll() times out with no input.
    /// If we're in Escape state, emit bare Escape.
    /// </summary>
    public bool FlushPendingEscape(out ParsedSequence seq)
    {
        if (_state == State.Escape)
        {
            _state = State.Ground;
            seq = new ParsedSequence
            {
                Type = SequenceType.EscapeOnly,
                Codepoint = 0
            };
            return true;
        }
        seq = default;
        return false;
    }

    private static int GetUtf8Length(byte first)
    {
        if ((first & 0xE0) == 0xC0) return 2;
        if ((first & 0xF0) == 0xE0) return 3;
        if ((first & 0xF8) == 0xF0) return 4;
        return 0;
    }

    private static int DecodeUtf8(byte first, ReadOnlySpan<byte> rest, int count)
    {
        int cp = count switch
        {
            1 => first & 0x1F,
            2 => first & 0x0F,
            3 => first & 0x07,
            _ => 0
        };

        for (int j = 0; j < count; j++)
            cp = (cp << 6) | (rest[j] & 0x3F);

        return cp;
    }
}
