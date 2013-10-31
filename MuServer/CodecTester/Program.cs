using System;
using System.IO;
using MuServerCodecs;

namespace CodecTester
{
    class Program
    {
        static void TestApeDecoding()
        {
            var apeDecoder = new ApeDecoder(@"C:\Users\quanb_000\Documents\Projects\quanbenSoft\MuServer\MuServer\CodecTester\samples\test.ape");
            var blockAlign = apeDecoder.GetInfo(ApeDecoder.Field.BlockAlign);
            Console.WriteLine("BA = {0}\n", blockAlign);
            var header = apeDecoder.GetHeaderInfo();
            Console.WriteLine("HEADER = {0}\n", header.AvgBytesPerSec);

            using (var fs = new FileStream("a.wav", FileMode.Create))
            {
                var headerBytes = apeDecoder.GetHeaderBytes();
                fs.Write(headerBytes, 0, headerBytes.Length);
                while (true)
                {
                    var buffer = new byte[4096];
                    var nRead = apeDecoder.GetData(buffer);
                    if (nRead == 0) break;
                    fs.Write(buffer, 0, nRead);
                }
            }
        }

        static void TestFlacDecoding()
        {
            var flacDecoder = new FlacDecoder(@"C:\Users\quanb_000\Documents\Projects\quanbenSoft\MuServer\MuServer\CodecTester\samples\test.flac");

            using (var fs = new FileStream("b.wav", FileMode.Create))
            {
                const int bufferSize = 4096;
                var buffer = new byte[bufferSize];
                var totalBytesGot = false;
                while (!flacDecoder.EndOfStream())
                {
                    var read = flacDecoder.GetData(buffer);
                    if (!totalBytesGot)
                    {
                        var totalBytes = flacDecoder.TotalBytes;
                        Console.WriteLine("total bytes = {0}", totalBytes);
                        totalBytesGot = true;
                    }
                    fs.Write(buffer, 0, read);
                }
            }
        }

        static void Main()
        {
            //TestApeDecoding();
            TestFlacDecoding();
        }
    }
}
