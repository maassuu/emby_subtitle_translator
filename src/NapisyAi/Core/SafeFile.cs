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
    
        /// <summary>Removes the file so the next save takes its exact name; returns whether anything was removed.</summary>
        public static bool DeleteIfExists(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
    
        /// <summary>
        /// Renames an existing file to a backup next to it (not a subtitle extension, so players ignore it);
        /// returns the backup path, or null when there was nothing to move.
        /// </summary>
        public static string? MoveAside(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var backup = path + ".napisyai-backup";
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }

            File.Move(path, backup);
            return backup;
        }

        /// <summary>Puts the backup back when no new file took its place; otherwise drops the backup. Returns true when restored.</summary>
        public static bool RestoreIfMissing(string path, string backup)
        {
            if (!File.Exists(backup))
            {
                return false;
            }

            if (File.Exists(path))
            {
                File.Delete(backup);
                return false;
            }

            File.Move(backup, path);
            return true;
        }
    }
}
