using System.Text;

namespace NapisyAi.Core
{
    public static class ProcessArgs
    {
        /// <summary>
        /// Quotes one argument for ProcessStartInfo.Arguments: backslashes are doubled only before a quote
        /// or at the end, so UNC paths like \\nas\share survive.
        /// </summary>
        public static string Quote(string arg)
        {
            var sb = new StringBuilder("\"");
            var backslashes = 0;
            foreach (var ch in arg)
            {
                if (ch == '\\')
                {
                    backslashes++;
                    continue;
                }

                if (ch == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1).Append('"');
                }
                else
                {
                    sb.Append('\\', backslashes).Append(ch);
                }

                backslashes = 0;
            }

            return sb.Append('\\', backslashes * 2).Append('"').ToString();
        }
    }
}
