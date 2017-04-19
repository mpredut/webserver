using System;
using System.Collections.Generic;
using System.IO;
using System.Text;


namespace WebServer.Decoder
{

    public class UrlDecoder : IBodyDecoder
    {
   
    public DecodedData Decode(Stream stream, ContentTypeHeader contentType, Encoding encoding)
        {
            if (stream == null || stream.Length == 0)
                return null;

            if (encoding == null)
                encoding = Encoding.UTF8;

            try
            {
                var content = new byte[stream.Length];
                int bytesRead = stream.Read(content, 0, content.Length);
                if (bytesRead != content.Length)
                    throw new Exception("Failed to read all bytes from body stream.");

                return new DecodedData {Parameters = UrlParser.Parse(new BufferReader(content, encoding))};
            }
            catch (ArgumentException err)
            {
                throw new FormatException(err.Message, err);
            }
        }

         public IEnumerable<string> ContentTypes
        {
            get { return new[] {"application/x-www-form-urlencoded"}; }
        }


    }
}