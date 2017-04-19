namespace WebServer.Decoder
{
    
    public interface ITextReader
    {
        char Current { get; }
        bool EOF { get; }
        bool HasMore { get; }
        int Index { get; set; }
        int Length { get; }
        int LineNumber { get; set; }
        
        char Peek { get; }
        int RemainingLength { get; }
        
        void Assign(object buffer, int offset, int count);
        
        void Assign(object buffer);
        void Consume();
        
        void Consume(params char[] chars);
        void ConsumeWhiteSpaces();
        
        void ConsumeWhiteSpaces(char extraCharacter);
        bool Contains(char ch);
        
        char Read();
        string ReadLine();
        
        string ReadQuotedString();
        
        string ReadToEnd(string delimiters);
        string ReadToEnd();
        string ReadToEnd(char delimiter);
        string ReadUntil(char delimiter);
        string ReadUntil(string delimiters);
        
        string ReadWord();
    }
}