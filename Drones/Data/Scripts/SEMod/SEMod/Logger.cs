using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;

namespace SEMod
{
    using Sandbox.ModAPI;
    using System;
    using System.IO;

    public static class Logger
    {
        private readonly static string DebugFileName = "debug.log";
        private static TextWriter DebugWriter;

        private readonly static string ErrorLogFileName = "error.log";
        private static TextWriter ErrorLogWriter;
        public static bool DebugEnabled = false;

        private static bool isInitialized = false;

        public static string ErrorFileName { get { return ErrorLogFileName; } }


        public static void Init()
        {
            if (!isInitialized)
            {
                if (DebugEnabled)
                {
                    
                    DebugWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage(DebugFileName, typeof(Logger));
                    DebugWriter.WriteLine();
                    DebugWriter.Flush();
                    isInitialized = true;
                }
            }
        }

        public static void Debug(string text, params object[] args)
        {
            if (DebugWriter == null || !isInitialized || !DebugEnabled)
                return;

            string msg = text;
            if (args.Length != 0)
                msg = string.Format(text, args);

            DebugWriter.WriteLine(string.Format("[{0:yyyy-MM-dd HH:mm:ss:fff}] Debug - {1}", DateTime.Now, msg));
            DebugWriter.Flush();
        }

        public static void LogException(Exception ex, string additionalInformation = null)
        {
            if (!isInitialized)
                return;

            // we create the writer when it is needed to prevent the creation of empty files
            if (ErrorLogWriter == null)
                ErrorLogWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage(ErrorLogFileName, typeof(Logger));

            ErrorLogWriter.WriteLine(string.Format("[{0:yyyy-MM-dd HH:mm:ss:fff}] Error - {1}", DateTime.Now, ex.ToString()));

            if (!string.IsNullOrEmpty(additionalInformation))
            {
                ErrorLogWriter.WriteLine(string.Format("Additional information on {0}:", ex.Message));
                ErrorLogWriter.WriteLine(additionalInformation);
            }

            ErrorLogWriter.Flush();
        }

        public static void Terminate()
        {
            if (DebugWriter != null)
            {
                DebugWriter.Flush();
                DebugWriter.Close();
                DebugWriter = null;
            }

            if (ErrorLogWriter != null)
            {
                ErrorLogWriter.Flush();
                ErrorLogWriter.Close();
                ErrorLogWriter = null;
            }

            isInitialized = false;
        }

        public static void WriteGameLog(string text, params object[] args)
        {
            string message = text;
            if (args != null && args.Length != 0)
                message = string.Format(text, args);

            if (MyAPIGateway.Utilities.IsDedicated)
                VRage.Utils.MyLog.Default.WriteLineAndConsole(message);
            else
                VRage.Utils.MyLog.Default.WriteLine(message);

            VRage.Utils.MyLog.Default.Flush();
        }
    }
}