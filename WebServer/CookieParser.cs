/**
 * File include cookie parser routines
 * As input it request the string type
 * and all the jobs is done by constructors
 * it is got from : https://github.com/n4t/mono/blob/master/mcs/class/System/System.Net/CookieParser.cs
 **/

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using System.Collections;
using System.Globalization;
using System.ComponentModel;


namespace WebServer
{
    class CookieParser
    {

        string header;
        int pos;
        int length;

        public CookieParser(string header)
            : this(header, 0)
        {
        }

        public CookieParser(string header, int position)
        {
            this.header = header;
            this.pos = position;
            this.length = header.Length;
        }

        public IEnumerable<Cookie> Parse()
        {
            while (pos < length)
            {
                Cookie cookie;
                try
                {
                    cookie = DoParse();
                }
                catch
                {
                    while ((pos < length) && (header[pos] != ','))
                        pos++;
                    pos++;
                    continue;
                }
                yield return cookie;
            }
        }

        Cookie DoParse()
        {
            var name = GetCookieName();
            if (pos >= length)
                return new Cookie(name, string.Empty);

            var value = string.Empty;
            if (header[pos] == '=')
            {
                pos++;
                value = GetCookieValue();
            }

            var cookie = new Cookie(name, value);

            if (pos >= length)
            {
                return cookie;
            }
            else if (header[pos] == ',')
            {
                pos++;
                return cookie;
            }
            else if ((header[pos++] != ';') || (pos >= length))
            {
                return cookie;
            }

            while (pos < length)
            {
                var argName = GetCookieName();
                string argVal = string.Empty;
                if ((pos < length) && (header[pos] == '='))
                {
                    pos++;
                    argVal = GetCookieValue();
                }
                ProcessArg(cookie, argName, argVal);

                if (pos >= length)
                    break;
                if (header[pos] == ',')
                {
                    pos++;
                    break;
                }
                else if (header[pos] != ';')
                {
                    break;
                }

                pos++;
            }

            return cookie;
        }

        void ProcessArg(Cookie cookie, string name, string val)
        {
            if ((name == null) || (name == string.Empty))
                throw new InvalidOperationException();

            name = name.ToUpper();
            switch (name)
            {
                case "COMMENT":
                    if (cookie.Comment == null)
                        cookie.Comment = val;
                    break;
                case "COMMENTURL":
                    if (cookie.CommentUri == null)
                        cookie.CommentUri = new Uri(val);
                    break;
                case "DISCARD":
                    cookie.Discard = true;
                    break;
                case "DOMAIN":
                    if (cookie.Domain == "")
                        cookie.Domain = val;
                    break;
                case "HTTPONLY":
                    cookie.HttpOnly = true;
                    break;
                case "MAX-AGE":
                    if (cookie.Expires == DateTime.MinValue)
                    {
                        try
                        {
                            cookie.Expires = cookie.TimeStamp.AddSeconds(UInt32.Parse(val));
                        }
                        catch { }
                    }
                    break;
                case "EXPIRES":
                    if (cookie.Expires != DateTime.MinValue)
                        break;

                    if ((pos < length) && (header[pos] == ',') && IsWeekDay(val))
                    {
                        pos++;
                        val = val + ", " + GetCookieValue();
                    }

                    cookie.Expires = CookieParser.TryParseCookieExpires(val);
                    break;
                case "PATH":
                    cookie.Path = val;
                    break;
                case "PORT":
                    if (cookie.Port == null)
                        cookie.Port = val;
                    break;
                case "SECURE":
                    cookie.Secure = true;
                    break;
                case "VERSION":
                    try
                    {
                        cookie.Version = (int)UInt32.Parse(val);
                    }
                    catch { }
                    break;
            }
        }

        string GetCookieName()
        {
            int k = pos;
            while (k < length && Char.IsWhiteSpace(header[k]))
                k++;

            int begin = k;
            while (k < length && header[k] != ';' && header[k] != ',' && header[k] != '=')
                k++;

            pos = k;
            return header.Substring(begin, k - begin).Trim();
        }

        string GetCookieValue()
        {
            if (pos >= length)
                return null;

            int k = pos;
            while (k < length && Char.IsWhiteSpace(header[k]))
                k++;

            int begin;
            if (header[k] == '"')
            {
                int j;
                begin = k++;

                while (k < length && header[k] != '"')
                    k++;

                for (j = ++k; j < length && header[j] != ';' && header[j] != ','; j++)
                    ;
                pos = j;
            }
            else
            {
                begin = k;
                while (k < length && header[k] != ';' && header[k] != ',')
                    k++;
                pos = k;
            }

            return header.Substring(begin, k - begin).Trim();
        }

        static bool IsWeekDay(string value)
        {
            foreach (string day in weekDays)
            {
                if (value.ToLower().Equals(day))
                    return true;
            }
            return false;
        }

        static string[] weekDays =
            new string[] { "mon", "tue", "wed", "thu", "fri", "sat", "sun",
				       "monday", "tuesday", "wednesday", "thursday",
				       "friday", "saturday", "sunday" };

        static string[] cookieExpiresFormats =
            new string[] { "r",
					"ddd, dd'-'MMM'-'yyyy HH':'mm':'ss 'GMT'",
					"ddd, dd'-'MMM'-'yy HH':'mm':'ss 'GMT'" };

        static DateTime TryParseCookieExpires(string value)
        {
            if (String.IsNullOrEmpty(value))
                return DateTime.MinValue;

            for (int i = 0; i < cookieExpiresFormats.Length; i++)
            {
                try
                {
                    DateTime cookieExpiresUtc = DateTime.ParseExact(value, cookieExpiresFormats[i],
                        CultureInfo.InvariantCulture);
                   
                    cookieExpiresUtc = DateTime.SpecifyKind(cookieExpiresUtc, DateTimeKind.Utc);
                    return TimeZone.CurrentTimeZone.ToLocalTime(cookieExpiresUtc);
                }
                catch { }
            }
            
            return DateTime.MinValue;
        }
    }
}
