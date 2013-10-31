using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MuServer
{
    public static class StringHelper
    {
        public static string GetExtension(this string source)
        {
            var ext = Path.GetExtension(source);
            return ext ?? "";
        }

        public static bool IsSubString(this string substring, string superstring, bool ignoreCase=false)
        {
            if (ignoreCase)
            {
                substring = substring.ToLower();
                superstring = superstring.ToLower();
            }
            return (substring.Length <= superstring.Length && superstring.Substring(0, substring.Length) == substring);
        }
        
        public static string Subtract(this string minuend, string subtrend)
        {
            return minuend.Substring(subtrend.Length);
        }

        public static string SubstringBetween(this string source, int start, int end)
        {
            return source.Substring(start, end - start);
        }

        public static string UrlToNormal(this string urlStr)
        {
            var byteList = new List<byte>();

            for (var i = 0; i < urlStr.Length; i++)
            {
                var ch = urlStr[i];
                if (ch == '%' && i < urlStr.Length-2)
                {
                    var ch2 = urlStr[i + 1];
                    if (ch2 == '%')
                    {
                        byteList.Add((byte)ch);
                        i++;
                        continue;
                    }
                    var sHex = urlStr.Substring(i+1, 2);
                    
                    try
                    {
                        var b = (byte) Convert.ToInt32(sHex, 16);
                        byteList.Add(b);
                        i += 2;
                    }
                    catch (Exception)
                    {
                        byteList.Add((byte)ch);
                    }
                }
                else
                {
                    byteList.Add((byte)ch);
                }
            }

            var bytes = byteList.ToArray();
            return bytes.ByteArrayToString(Encoding.UTF8);
        }

        public static string NormalToUrl(this string normalStr)
        {
            var sbUrl = new StringBuilder();
            foreach (var ch in normalStr)
            {
                if (char.IsLetterOrDigit(ch) || ch == ' ' || ch == '/' || ch == '_' || ch == '.')
                {
                    sbUrl.Append(ch);
                }
                else
                {
                    var sHex = Convert.ToString((byte)ch, 16);
                    sbUrl.Append('%');
                    sbUrl.Append(sHex);
                }
            }
            return sbUrl.ToString();
        }

        public static string HtmlToNormal(this string htmlStr)
        {
            throw new NotImplementedException();
        }

        public static string NormalToHtml(this string normalStr)
        {
            throw new NotImplementedException();
        }

        public static string OneLevelUp(this string path)
        {
// ReSharper disable ReturnValueOfPureMethodIsNotUsed
            path = path.TrimEnd('/');
// ReSharper restore ReturnValueOfPureMethodIsNotUsed
            var last = path.LastIndexOf('/');
            return last < 0 ? path : path.Substring(0, last+1);
        }

        public static string ByteArrayToString(this byte[] bytes, Encoding encoding)
        {
            return encoding.GetString(bytes);
        }

        public static byte[] ToByteArray(this string source, Encoding encoding)
        {
            return encoding.GetBytes(source);
        }

        public static byte[] ToUTF8ByteArray(this string source)
        {
            var encoding = new UTF8Encoding();
            return source.ToByteArray(encoding);
        }

        public static byte[] ToAscIIByteArray(this string source)
        {
            var encoding = new ASCIIEncoding();
            return source.ToByteArray(encoding);
        }
    }
}
