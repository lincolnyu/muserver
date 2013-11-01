using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MuServerCodecs;

namespace MuServer
{
    internal class WebServer
    {
        #region Fields

        private readonly TcpListener _listener;
        private readonly AutoResetEvent _doneEvent = new AutoResetEvent(false);
        private int _numRunningThreads;

        #endregion

        #region Constructors

        public WebServer(int port = 5050)
        {
            try
            {
                //start listing on the given port
                _listener = new TcpListener(new IPAddress(new byte[] {192, 168, 1, 88}), port);
                _listener.Start();
                Console.WriteLine("Web Server Running... Press ^C to Stop...");

                //start the thread which calls the method 'StartListen'
                Fork();

                while (true)
                {
                    if (!_doneEvent.WaitOne()) continue;
                    if (_numRunningThreads <= 0)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An Exception Occurred while Listening: " + e);
            }
        }

        #endregion

        #region Methods

        private void Fork()
        {
            ThreadPool.QueueUserWorkItem(TryStartListen);
            Interlocked.Increment(ref _numRunningThreads);
        }

        private void Merge()
        {
            Interlocked.Decrement(ref _numRunningThreads);
            _doneEvent.Set();
        }

        public void TryStartListen(object obj)
        {
            try
            {
                const string webServerRoot = @"F:\Music";
                Socket socket;
                do
                {
                    // accepts a new connection
                    socket = _listener.AcceptSocket();
                    Console.WriteLine("Socket Type " + socket.SocketType);
                } while (!socket.Connected);

                Fork();

                StartListen(socket, webServerRoot);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Occurred: {0} ", e);
            }
            finally
            {
                Merge();
            }
        }

        private void StartListen(Socket socket, string webServerRoot)
        {
            Console.WriteLine("\nClient Connected!!\n==================\nClient IP {0}\n", socket.RemoteEndPoint);

            // makes a byte array and receives data from the client
            var bufRecv = new byte[1024];
            socket.Receive(bufRecv, bufRecv.Length, 0);

            // converts byte to string
            var bufferedStr = Encoding.UTF8.GetString(bufRecv);

            // we only deal with GET type for the moment
            if (bufferedStr.Substring(0, 3) != "GET")
            {
                Console.WriteLine("Only Get Method is supported..");
                socket.Close();

                return;
            }

            // Looks for HTTP request
            var iStartPos = bufferedStr.IndexOf("HTTP", 1, StringComparison.Ordinal);

            // Gets the HTTP text and version
            var httpVersion = bufferedStr.Substring(iStartPos, 8);

            // Extracts the requested type and requested file/directory
            var request = bufferedStr.Substring(0, iStartPos - 1);

            // Replaces backslashes with forward slashes if any
// ReSharper disable ReturnValueOfPureMethodIsNotUsed
            request.Replace(@"\", "/");
// ReSharper restore ReturnValueOfPureMethodIsNotUsed

            // If file name is not supplied add forward slash to indicate 
            // that it is a directory and then we will look for the 
            // default file name..
            if ((request.IndexOf(".", StringComparison.Ordinal) < 1) && (!request.EndsWith("/")))
            {
                request = request + "/";
            }

            // Extracts the requested file name
            iStartPos = request.LastIndexOf("/", StringComparison.Ordinal) + 1;
            var requestedFile = request.Substring(iStartPos);

            // Extracts The directory Name (anything between the first and the last slashes inclusive)
            var dirName = request.SubstringBetween(request.IndexOf("/", StringComparison.Ordinal),
                                                    request.LastIndexOf("/", StringComparison.Ordinal) + 1);

            /* Identifies the Physical Directory */

            var isDDir = false;

            var localDir = GetPathRelative(dirName);
            if (localDir != "")
            {
                isDDir = true;
            }
            else if (dirName == "/")
            {
                localDir = webServerRoot;
            }
            else
            {
                localDir = GetLocalPath(dirName);
            }

            Console.WriteLine("Directory Requested: " + localDir);

            // If the physical directory does not exists then
            // dispaly the error message
            if (localDir.Length == 0)
            {
                const string errorMessage = "<H2>Error!! Requested Directory does not exists</H2><Br>";
                // sErrorMessage = sErrorMessage + "Please check data\\Vdirs.Dat";

                // Formats The Message
                SendHeader(httpVersion, "", errorMessage.Length, " 404 Not Found", socket);

                // Sends to the browser
                SendToBrowser(errorMessage, socket);

                socket.Close();

                return;
            }

            /* Identifies the file name */

            requestedFile = requestedFile.UrlToNormal();

            // If The file name is not supplied then look in the default file list
            if (requestedFile.Length == 0)
            {
                // Get the default filename
                requestedFile = GetTheDefaultFileName(localDir);

                if (requestedFile == "" && isDDir)
                {
                    var flg = new FileListGenerator(dirName, localDir);
                    var html = flg.GetFileListHtml();
                    var htmlBytes = html.ToUTF8ByteArray();
                    using (var memstream = new MemoryStream(htmlBytes))
                    {
                        using (var reader = new BinaryReader(memstream))
                        {
                            Respond(socket, httpVersion, "text/html", reader, htmlBytes.Length);
                        }                           
                    }
                    socket.Close();

                    return;
                }
                if (requestedFile == "")
                {
                    const string errorMessage = "<H2>Error!! No Default File Name Specified</H2>";
                    SendHeader(httpVersion, "", errorMessage.Length, " 404 Not Found", socket);
                    SendToBrowser(errorMessage, socket);

                    socket.Close();

                    return;
                }
            }

            /* gets the MIME type */
            var mimeType = GetMimeType(requestedFile);

            //Build the physical path
            var physicalFilePath = Path.Combine(localDir, requestedFile);
            Console.WriteLine("File Requested: " + physicalFilePath);

            if (File.Exists(physicalFilePath) == false)
            {
                const string errorMessage = "<H2>404 Error! File Does Not Exists...</H2>";
                SendHeader(httpVersion, "", errorMessage.Length, " 404 Not Found", socket);
                SendToBrowser(errorMessage, socket);

                Console.WriteLine("File doesn't exist");
            }
            else
            {
                if (physicalFilePath.GetExtension().ToUpper() == ".APE")
                {
                    using (var apeDecoder = new ApeDecoder(physicalFilePath))
                    {
                        var blockSize = apeDecoder.GetInfo(ApeDecoder.Field.BlockAlign);
                        var numBlocks = apeDecoder.GetInfo(ApeDecoder.Field.TotalBlocks);
                        var headerBytes = apeDecoder.GetHeaderBytes();
                        var totalBytes = numBlocks*blockSize + headerBytes.Length;

                        SendHeader(httpVersion, "audio/wav", totalBytes, " 200 OK", socket);
                        
                        SendToBrowser(headerBytes, socket);
                        
                        var bufferSize = blockSize*1024;
                        var dataBuffer = new byte[bufferSize];
                        while (true) 
                        {
                            var read = apeDecoder.GetData(dataBuffer);
                            if (read <= 0 || !SendToBrowser(dataBuffer, 0, read, socket))
                            {
                                break;
                            }
                        }
                        Console.WriteLine("Transcoding done with WAV stream decoded from APE sent");
                    }
                }
                else if (physicalFilePath.GetExtension().ToUpper() == ".FLAC")
                {
                    using (var flacDecoder = new FlacDecoder(physicalFilePath))
                    {
                        const int bufferSize = 4 * 1024;
                        var dataBuffer = new byte[bufferSize];
                        var headerSent = false;

                        while (!flacDecoder.EndOfStream())
                        {
                            var read = flacDecoder.GetData(dataBuffer);
                            if (!headerSent)
                            {
                                var totalBytes = flacDecoder.TotalBytes;
                                SendHeader(httpVersion, "audio/wav", (int)totalBytes, " 200 OK", socket);
                                headerSent = true;
                            }
                            if (read <= 0 || !SendToBrowser(dataBuffer, 0, read, socket))
                            {
                                break;
                            }
                        }
                        Console.WriteLine("Transcoding done with WAV stream decoded from FLAC sent");
                    }
                }
                else
                {
                    const long lengthLimit = 4*1024*1024;  // 4M
                    using (var fs = new FileStream(physicalFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var reader = new BinaryReader(fs))
                        {
                            if (fs.Length < lengthLimit)
                            {
                                Respond(socket, httpVersion, mimeType, reader, (int)fs.Length);
                            }
                            else if (fs.Length <= 0x7fffffff)
                            {
                                RespondByChunks(socket, httpVersion, mimeType, reader, (int)fs.Length);
                            }
                            else
                            {
                                Console.WriteLine("File length greater than {0} bytes not supported", 0x80000000);
                            }
                        }
                    }
                }
            }

            socket.Close();
        }

        private void RespondByChunks(Socket socket, string httpVersion, string mimeType, BinaryReader reader, int length = -1)
        {
            SendHeader(httpVersion, mimeType, length, " 200 OK", socket);
            
            const int bufferLength = 4*1024*1024;
            var bytes = new byte[bufferLength];
            while ((reader.Read(bytes, 0, bytes.Length)) != 0)
            {
                // Read from the file and write the data to the network
                if (!SendToBrowser(bytes, socket))
                {
                    return;
                }
            }
        }

        private void Respond(Socket socket, string httpVersion, string mimeType,  BinaryReader reader, int length)
        {
            var totalBytes = 0;

            var response = "";

            var bytes = new byte[length];
            int read;
            while ((read = reader.Read(bytes, 0, bytes.Length)) != 0)
            {
                // Read from the file and write the data to the network
                response = response + Encoding.UTF8.GetString(bytes, 0, read);

                totalBytes = totalBytes + read;
            }
            reader.Close();

            SendHeader(httpVersion, mimeType, totalBytes, " 200 OK", socket);

            SendToBrowser(bytes, socket);
        }

        private static string GetPathRelative(string dirName)
        {
            var directDir = "";
            var realDir = "";

            try
            {
                var sr = new StreamReader("data\\DDirs.Dat");
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    //Remove extra Spaces
                    // ReSharper disable ReturnValueOfPureMethodIsNotUsed
                    line.Trim();
                    // ReSharper restore ReturnValueOfPureMethodIsNotUsed

                    if (line.Length <= 0) continue;

                    //find the separator
                    var iStartPos = line.IndexOf(";", StringComparison.Ordinal);

                    // Convert to lowercase
                    line = line.ToLower();

                    directDir = line.Substring(0, iStartPos);
                    var mappedDir = line.Substring(iStartPos + 1);

                    if (directDir.IsSubString(dirName, true))
                    {
                        var rel = dirName.Subtract(directDir);
// ReSharper disable ReturnValueOfPureMethodIsNotUsed
                        rel.TrimStart('/');
                        rel = rel.Replace('/', '\\');
                        rel = rel.UrlToNormal();
// ReSharper restore ReturnValueOfPureMethodIsNotUsed
                        realDir = Path.Combine(mappedDir, rel);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An Exception Occurred: " + e);
            }

            Console.WriteLine("Direct Dir  : " + directDir);
            Console.WriteLine("Directory   : " + dirName);
            Console.WriteLine("Physical Dir: " + realDir);

            return realDir;
        }

        /// <summary>
        ///  Returns the Physical Path
        /// </summary>
        /// <param name="dirName">Virtual Directory </param>
        /// <returns>Physical local Path</returns>
        private static string GetLocalPath(string dirName)
        {
            var virtualDir = "";
            var realDir = "";

            //Remove extra spaces
// ReSharper disable ReturnValueOfPureMethodIsNotUsed
            dirName.Trim();
// ReSharper restore ReturnValueOfPureMethodIsNotUsed

            // Convert to lowercase
            dirName = dirName.ToLower();

            //Remove the slash
            //dirName = dirName.Substring(1, dirName.Length - 2);

            try
            {
                //Open the Vdirs.dat to find out the list virtual directories
                var sr = new StreamReader("data\\VDirs.Dat");

                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    //Remove extra Spaces
// ReSharper disable ReturnValueOfPureMethodIsNotUsed
                    line.Trim();
// ReSharper restore ReturnValueOfPureMethodIsNotUsed

                    if (line.Length <= 0) continue;

                    //find the separator
                    var iStartPos = line.IndexOf(";", StringComparison.Ordinal);

                    // Convert to lowercase
                    line = line.ToLower();

                    virtualDir = line.Substring(0, iStartPos);
                    realDir = line.Substring(iStartPos + 1);

                    if (virtualDir == dirName)
                    {
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An Exception Occurred: " + e);
            }


            Console.WriteLine("Virtual Dir : " + virtualDir);
            Console.WriteLine("Directory   : " + dirName);
            Console.WriteLine("Physical Dir: " + realDir);

            return virtualDir == dirName ? realDir : "";
        }


        /// <summary>
        /// Returns The Default File Name
        /// Input : WebServerRoot Folder
        /// Output: Default File Name
        /// </summary>
        /// <param name="localDir"></param>
        /// <returns></returns>
        public string GetTheDefaultFileName(string localDir)
        {
            try
            {
                //Open the default.dat to find out the list
                // of default file
                var sr = new StreamReader("data\\Default.Dat");

                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    //Look for the default file in the web server root folder
                    var filePath = Path.Combine(localDir, line);
                    if (File.Exists(filePath))
                    {
                        return line;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An Exception Occurred: " + e);
            }
            return "";
        }

        /// <summary>
        /// This function takes FileName as Input and returns the mime type..
        /// </summary>
        /// <param name="sRequestedFile">To indentify the Mime Type</param>
        /// <returns>Mime Type</returns>
        public string GetMimeType(string sRequestedFile)
        {
            var mimeType = "";
            var mimeExt = "";

            // Convert to lowercase
            sRequestedFile = sRequestedFile.ToLower();

            var iStartPos = sRequestedFile.IndexOf(".", StringComparison.Ordinal);

            var fileExt = sRequestedFile.Substring(iStartPos);

            try
            {
                //Open the Vdirs.dat to find out the list virtual directories
                var sr = new StreamReader("data\\Mime.Dat");

                string line;
                while ((line = sr.ReadLine()) != null)
                {
// ReSharper disable ReturnValueOfPureMethodIsNotUsed
                    line.Trim();
// ReSharper restore ReturnValueOfPureMethodIsNotUsed

                    if (line.Length <= 0) continue;

                    //find the separator
                    iStartPos = line.IndexOf(";", StringComparison.Ordinal);

                    // Convert to lower case
                    line = line.ToLower();

                    mimeExt = line.Substring(0, iStartPos);
                    mimeType = line.Substring(iStartPos + 1);

                    if (mimeExt == fileExt)
                        break;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("An Exception Occurred: " + e);
            }

            return mimeExt == fileExt ? mimeType : "";
        }

        /// <summary>
        /// This function send the Header Information to the client (Browser)
        /// </summary>
        /// <param name="httpVersion">HTTP Version</param>
        /// <param name="mimeHeader">Mime Type</param>
        /// <param name="totalBytes">Total Bytes to be sent in the body</param>
        /// <param name="statusBytes"></param>
        /// <param name="socket">Socket reference</param>
        /// <returns></returns>
        public void SendHeader(string httpVersion, string mimeHeader, int totalBytes, string statusBytes, Socket socket)
        {
            var sBuffer = "";

            // if Mime type is not provided set default to text/html
            if (mimeHeader.Length == 0)
            {
                mimeHeader = "text/html"; // Default Mime Type is text/html
            }

            sBuffer = sBuffer + httpVersion + statusBytes + "\r\n";
            sBuffer = sBuffer + "Server: cx1193719-b\r\n";
            sBuffer = sBuffer + "Content-Type: " + mimeHeader + "\r\n";
            sBuffer = sBuffer + "Accept-Ranges: bytes\r\n";
            if (totalBytes >= 0)
            {
                sBuffer = sBuffer + "Content-Length: " + totalBytes + "\r\n\r\n";
            }

            Console.WriteLine("=== header ===");
            Console.WriteLine(sBuffer);
            Console.WriteLine("==============");

            var bSendData = Encoding.UTF8.GetBytes(sBuffer);

            SendToBrowser(bSendData, socket);

            Console.WriteLine("Total Bytes: " + totalBytes.ToString(CultureInfo.InvariantCulture));

        }

        /// <summary>
        ///  Overloaded Function, takes string, convert to bytes and calls 
        ///  overloaded sendToBrowserFunction.
        /// </summary>
        /// <param name="sData">The data to be sent to the browser(client)</param>
        /// <param name="socket">Socket reference</param>
        public bool SendToBrowser(String sData, Socket socket)
        {
            return SendToBrowser(Encoding.UTF8.GetBytes(sData), socket);
        }

        public bool SendToBrowser(byte[] buffer, Socket socket)
        {
            return SendToBrowser(buffer, 0, buffer.Length, socket);
        }

        /// <summary>
        ///  Sends data to the browser (client)
        /// </summary>
        /// <param name="buffer">Byte Array</param>
        /// <param name="offset">The position in the data buffer at which to begin sending data</param>
        /// <param name="length">The number of bytes to send</param>
        /// <param name="socket">Socket reference</param>
        public bool SendToBrowser(byte[] buffer, int offset, int length, Socket socket)
        {
            try
            {
                if (socket.Connected)
                {
                    if ((socket.Send(buffer, offset, length, 0)) == -1)
                    {
                        Console.WriteLine("Socket Error cannot Send Packet");
                    }
                    else
                    {
                        Console.Write("S");
                        //Console.WriteLine("No. of bytes sent {0}", numBytes);
                    }
                    return true;
                }
                Console.WriteLine("Connection Dropped....");
                return false;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error Occurred: {0} ", e);
            }
            return false;
        }

        #endregion
    }
}
