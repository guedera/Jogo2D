using System;
using System.Runtime.InteropServices;

namespace TypeDuel
{
    /// <summary>
    /// Abre o seletor de arquivo do sistema para escolher um .txt ou .md (Editor, Windows e macOS).
    /// Devolve null se o jogador cancelar ou se a plataforma nao tiver suporte.
    /// </summary>
    public static class FilePicker
    {
        public static bool IsSupported
        {
            get
            {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
                return true;
#else
                return false;
#endif
            }
        }

        public static string PickTextFile()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanelWithFilters("Choose a .txt or .md file", "", new[] { "Text files", "txt,md", "All files", "*" });
            return string.IsNullOrEmpty(path) ? null : path;
#elif UNITY_STANDALONE_WIN
            return PickWindows();
#elif UNITY_STANDALONE_OSX
            return PickMac();
#else
            return null;
#endif
        }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        sealed class OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public string lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public string lpstrFile;
            public int nMaxFile;
            public string lpstrFileTitle;
            public int nMaxFileTitle;
            public string lpstrInitialDir;
            public string lpstrTitle;
            public int Flags;
            public short nFileOffset;
            public short nFileExtension;
            public string lpstrDefExt;
            public IntPtr lCustData;
            public IntPtr lpfnHook;
            public string lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved;
            public int flagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Auto)]
        static extern bool GetOpenFileName([In, Out] OpenFileName ofn);

        static string PickWindows()
        {
            const int maxPath = 1024;
            const int explorer = 0x00080000, pathMustExist = 0x00000800, fileMustExist = 0x00001000, noChangeDir = 0x00000008;

            var ofn = new OpenFileName();
            ofn.lStructSize = Marshal.SizeOf(ofn);
            ofn.lpstrFilter = "Text files (*.txt;*.md)\0*.txt;*.md\0All files (*.*)\0*.*\0\0";
            ofn.nFilterIndex = 1;
            ofn.lpstrFile = new string('\0', maxPath);
            ofn.nMaxFile = maxPath;
            ofn.lpstrTitle = "Choose a .txt or .md file";
            ofn.Flags = explorer | pathMustExist | fileMustExist | noChangeDir;

            if (!GetOpenFileName(ofn)) return null;

            int end = ofn.lpstrFile.IndexOf('\0');
            string path = end >= 0 ? ofn.lpstrFile.Substring(0, end) : ofn.lpstrFile;
            return path.Length > 0 ? path : null;
        }
#endif

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        static string PickMac()
        {
            try
            {
                var info = new System.Diagnostics.ProcessStartInfo("osascript")
                {
                    Arguments = "-e \"POSIX path of (choose file)\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (var process = System.Diagnostics.Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();
                    return output.Length > 0 ? output : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
#endif
    }
}
