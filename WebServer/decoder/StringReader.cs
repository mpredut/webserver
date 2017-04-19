using System;

namespace WebServer.Decoder
{
    
    public class StringReader : ITextReader
    {
        private string _buffer;
        
        public StringReader(string buffer)
        {
            Assign(buffer);
        }
        public StringReader()
        {
        }

        private string GetString(int startIndex, int endIndex)
        {
            return _buffer.Substring(startIndex, endIndex - startIndex);
        }

        private string GetString(int startIndex, int endIndex, bool trimEnd)
        {
            if (trimEnd)
            {
                if (endIndex > 0)
                    --endIndex; // need to move one back to be able to trim.
                while (endIndex > 0 && _buffer[endIndex] == ' ' || _buffer[endIndex] == '\t')
                    --endIndex;
                ++endIndex;
            }
            return _buffer.Substring(startIndex, endIndex - startIndex);
        }

        #region ITextReader Members
        public int LineNumber { get; set; }
        
        public bool EOF
        {
            get { return Index >= Length; }
        }
        
        public bool HasMore
        {
            get { return Index < Length; }
        }
        
        public char Peek
        {
            get { return Index < Length - 1 ? _buffer[Index + 1] : char.MinValue; }
        }
        
        public char Current
        {
            get { return HasMore ? _buffer[Index] : char.MinValue; }
        }
        public int Index { get; set; }
        
        public int Length { get; private set; }
        public int RemainingLength
        {
            get { return Length - Index; }
        }
        
        public void Assign(object buffer, int offset, int count)
        {
            if (!(buffer is string))
                throw new ArgumentException("buffer needs to be of type string", "buffer");

            _buffer = (string) buffer;
            Index = offset;
            Length = count;
        }
        
        public void Assign(object buffer)
        {
            if (!(buffer is string))
                throw new ArgumentException("buffer needs to be of type string", "buffer");
            _buffer = (string) buffer;
            Index = 0;
            Length = _buffer.Length;
        }
        public void Consume()
        {
            ++Index;
        }
        public string ReadLine()
        {
            int startIndex = Index;
            while (HasMore && Current != '\n')
                Consume();

            // EOF? Then we havent enough bytes.
            if (EOF)
            {
                Index = startIndex;
                return null;
            }

            Consume(); // eat \n too.

            string thisLine = _buffer.Substring(startIndex, Index - startIndex - 2);

            // Multi line message?
            if (Current == '\t' || Current == ' ')
            {
                Consume();
                string extra = ReadLine();

                // Multiline isn't complete, wait for more bytes.
                if (extra == null)
                {
                    Index = startIndex;
                    return null;
                }

                return thisLine + " " + extra.TrimStart(' ', '\t');
            }

            return thisLine;
        }
        
        public string ReadQuotedString()
        {
            Consume(' ', '\t');
            if (Current != '\"')
                return null;

            int startPos = Index;
            Consume();
            string buffer = string.Empty;
            while (!EOF)
            {
                switch (Current)
                {
                    case '\\':
                        Consume();
                        buffer += Current;
                        break;
                    case '"':
                        Consume();
                        return buffer;
                    default:
                        buffer += Current;
                        break;
                }
                ++Index;
            }

            Index = startPos;
            return null;
        }
        
        public string ReadToEnd(string delimiters)
        {
            if (EOF)
                return string.Empty;

            int startIndex = Index;

            bool isDelimitersNewLine = delimiters.IndexOfAny(new[] {'\r', '\n'}) != -1;
            while (true)
            {
                if (EOF)
                    return GetString(startIndex, Index);

                if (delimiters.IndexOf(Current) != -1)
                    return GetString(startIndex, Index, true);

                // Delimiter is not new line and we got one.
                if (isDelimitersNewLine && Current == '\r' || Current == '\n')
                    throw new InvalidOperationException("Unexpected new line: " + GetString(startIndex, Index) +
                                                        "[CRLF].");

                ++Index;
            }
        }
        public string ReadToEnd()
        {
            int index = Index;
            Index = Length;
            return _buffer.Substring(index);
        }
        
        public string ReadToEnd(char delimiter)
        {
            if (EOF)
                return string.Empty;

            int startIndex = Index;

            while (true)
            {
                if (EOF)
                    return GetString(startIndex, Index);

                if (Current == delimiter)
                    return GetString(startIndex, Index, true);

                // Delimiter is not new line and we got one.
                if (delimiter != '\r' && delimiter != '\n' && Current == '\r' || Current == '\n')
                    throw new InvalidOperationException("Unexpected new line: " + GetString(startIndex, Index) +
                                                        "[CRLF].");

                ++Index;
            }
        }
        
        public void Consume(params char[] chars)
        {
            while (HasMore)
            {
                bool found = false;
                foreach (var ch in chars)
                {
                    if (Current != ch) continue;
                    found = true;
                    break;
                }
                if (!found)
                    return;

                ++Index;
            }
        }
        public void ConsumeWhiteSpaces()
        {
            Consume('\t', ' ');
        }
        
        public char Read()
        {
            return _buffer[Index++];
        }
        public string ReadUntil(char delimiter)
        {
            if (EOF)
                return null;

            int startIndex = Index;

            while (true)
            {
                if (EOF)
                {
                    Index = startIndex;
                    return null;
                }

                if (Current == delimiter)
                    return GetString(startIndex, Index, true);

                // Delimiter is not new line and we got one.
                if (delimiter != '\r' && delimiter != '\n' && Current == '\r' || Current == '\n')
                    throw new InvalidOperationException("Unexpected new line: " + GetString(startIndex, Index) +
                                                        "[CRLF].");

                ++Index;
            }
        }
        public string ReadUntil(string delimiters)
        {
            if (EOF)
                return null;

            int startIndex = Index;

            bool isDelimitersNewLine = delimiters.IndexOfAny(new[] {'\r', '\n'}) != -1;
            while (true)
            {
                if (EOF)
                {
                    Index = startIndex;
                    return null;
                }

                if (delimiters.IndexOf(Current) != -1)
                    return GetString(startIndex, Index, true);

                // Delimiter is not new line and we got one.
                if (isDelimitersNewLine && Current == '\r' || Current == '\n')
                    throw new InvalidOperationException("Unexpected new line: " + GetString(startIndex, Index) +
                                                        "[CRLF].");

                ++Index;
            }
        }
        
        public string ReadWord()
        {
            return ReadToEnd(" \t\r\n");
        }
        
        public void ConsumeWhiteSpaces(char extraCharacter)
        {
            Consume('\t', ' ', extraCharacter);
        }
        public bool Contains(char ch)
        {
            int index = Index;
            while (index < Length)
            {
                if (ch == _buffer[index])
                    return true;
                ++index;
            }

            return false;
        }

        #endregion
    }
}