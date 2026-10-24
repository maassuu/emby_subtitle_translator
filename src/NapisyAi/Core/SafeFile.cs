using System.IO;
using System.Text;

namespace NapisyAi.Core
{
    public static class SafeFile
    {
        /// <summary>Writes UTF-8 via a temp file; returns false and leaves everything as is when the file already exists.</summary>
        public static bool WriteNew(string path, string content)
        {
            if (File.Exists(path))
            {
                return false;
            }

            var temp = path + ".tmp";
            try
            {
                File.WriteAllText(temp, content, new UTF8Encoding(false));
                File.Move(temp, path);
                return true;
            }
            catch (IOException) when (File.Exists(path))
            {
                return false;
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }
            }
        }
    }
}
