using System;
using System.IO;
using System.Text;

namespace MuServer
{
    class FileListGenerator
    {
        #region Fields

        private readonly string _vdir;
        private readonly DirectoryInfo _dir;

        #endregion

        #region Constructors

        public FileListGenerator(string vdir, string dirPath)
        {
            vdir = vdir.Trim();
            if (vdir[vdir.Length - 1] != '/') vdir += '/';
            _vdir = vdir;
            _dir = new DirectoryInfo(dirPath);
        }

        #endregion

        #region Methods

        public string GetFileListHtml()
        {
            try
            {
                var sbResult = new StringBuilder("<html><meta charset=\"utf-8\">");
                var subdirs = _dir.GetDirectories();
                var files = _dir.GetFiles();

                sbResult.Append("<div>");
                sbResult.AppendFormat("<a href=\"{0}\">..</a>", _vdir.OneLevelUp());
                sbResult.Append("</div>"); 

                foreach (var subdir in subdirs)
                {
                    sbResult.Append("<div>");
                    // TODO convert to html url string
                    var url = _vdir + subdir.Name.Utf8ToUrl();
                    sbResult.AppendFormat("<a href=\"{0}/\">{1}</a>", url, subdir.Name);
                    sbResult.Append("</div>");
                }

                foreach (var file in files)
                {
                    sbResult.Append("<div>");
                    // TODO convert to html url string
                    var url = _vdir + file.Name.Utf8ToUrl();
                    sbResult.AppendFormat("<a href=\"{0}\">{1}</a>", url, file.Name);
                    sbResult.Append("</div>");
                }

                sbResult.Append("</html>");
                return sbResult.ToString();
            }
            catch (Exception)
            {
                Console.WriteLine("Some error has happened when generating file list");
                throw;
            }
        }

        #endregion
    }
}
