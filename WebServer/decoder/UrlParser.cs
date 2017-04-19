using System;
using System.Web;


namespace WebServer.Decoder
{
    
    public static class UrlParser
    {
        public static ParameterCollection Parse(ITextReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");

            var parameters = new ParameterCollection();
            while (!reader.EOF)
            {
                string name = Uri.EscapeDataString(reader.ReadToEnd("&="));
                char current = reader.Current;
                reader.Consume();
                switch (current)
                {
                    case '&':
                        parameters.Add(name, string.Empty);
                        break;
                    case '=':
                        {
                            string value = reader.ReadToEnd("&");
                            reader.Consume();
                            parameters.Add(name, Uri.EscapeDataString(value));
                        }
                        break;
                    default:
                        parameters.Add(name, string.Empty);
                        break;
                }
            }

            return parameters;
        }
        
        public static ParameterCollection Parse(string queryString)
        {
            if (queryString == null)
                throw new ArgumentNullException("queryString");
            if (queryString.Length == 0)
                return new ParameterCollection();

            var reader = new StringReader(queryString);
            return Parse(reader);
        }
    }
}