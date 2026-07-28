using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

// Minimal Windows-only reimplementation of the small subset of the
// gkngkc/UnityStandaloneFileBrowser API (namespace SFB) that this mod
// depends on for native Open/Save file dialogs. Not vendored from that
// project's source - written against the documented Win32 common dialog
// API (GetOpenFileNameW / GetSaveFileNameW) to match the calling
// signatures used throughout Overlayer.
namespace SFB
{
    public struct ExtensionFilter
    {
        public string Name;
        public string[] Extensions;

        public ExtensionFilter(string name, params string[] extensions)
        {
            Name = name;
            Extensions = extensions ?? new string[0];
        }
    }

    public static class StandaloneFileBrowser
    {
        public static string[] OpenFilePanel(string title, string directory, ExtensionFilter[] extensions, bool multiselect)
        {
            return Win32FileDialog.OpenFile(title, directory, BuildFilter(extensions), multiselect);
        }

        public static string SaveFilePanel(string title, string directory, string defaultName, string extension)
        {
            ExtensionFilter[] filters = string.IsNullOrEmpty(extension)
                ? null
                : new[] { new ExtensionFilter(extension.ToUpperInvariant() + " File", extension) };
            return Win32FileDialog.SaveFile(title, directory, defaultName, BuildFilter(filters), extension);
        }

        public static string SaveFilePanel(string title, string directory, string defaultName, ExtensionFilter[] extensions)
        {
            string firstExt = extensions != null && extensions.Length > 0 && extensions[0].Extensions.Length > 0
                ? extensions[0].Extensions[0]
                : null;
            return Win32FileDialog.SaveFile(title, directory, defaultName, BuildFilter(extensions), firstExt);
        }

        private static string BuildFilter(ExtensionFilter[] extensions)
        {
            if(extensions == null || extensions.Length == 0)
                return "All Files\0*.*\0\0";

            var sb = new StringBuilder();
            foreach(var f in extensions) {
                sb.Append(f.Name).Append('\0');
                sb.Append(string.Join(";", f.Extensions.Select(e => e == "*" ? "*.*" : "*." + e))).Append('\0');
            }
            sb.Append('\0');
            return sb.ToString();
        }
    }

    internal static class Win32FileDialog
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OPENFILENAME
        {
            public int lStructSize;
            public IntPtr hwndOwner;
            public IntPtr hInstance;
            public string lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int nMaxCustFilter;
            public int nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
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
            public int FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileNameW(ref OPENFILENAME ofn);

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetSaveFileNameW(ref OPENFILENAME ofn);

        private const int OFN_ALLOWMULTISELECT = 0x00000200;
        private const int OFN_EXPLORER = 0x00080000;
        private const int OFN_FILEMUSTEXIST = 0x00001000;
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_OVERWRITEPROMPT = 0x00000002;
        private const int OFN_NOCHANGEDIR = 0x00000008;

        private const int BufferChars = 1 << 16;

        public static string[] OpenFile(string title, string directory, string filter, bool multiselect)
        {
            var ofn = new OPENFILENAME();
            ofn.lStructSize = Marshal.SizeOf(typeof(OPENFILENAME));
            ofn.lpstrFilter = filter;
            ofn.nMaxFile = BufferChars;
            ofn.lpstrFile = Marshal.AllocHGlobal(BufferChars * 2);
            ofn.lpstrInitialDir = NormalizeDir(directory);
            ofn.lpstrTitle = title;
            ofn.Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR
                        | (multiselect ? OFN_ALLOWMULTISELECT : 0);

            try {
                Marshal.WriteInt16(ofn.lpstrFile, 0, 0);
                if(!GetOpenFileNameW(ref ofn))
                    return new string[0];

                var parts = ReadNullSeparatedStrings(ofn.lpstrFile);
                if(parts.Count == 0)
                    return new string[0];
                if(parts.Count == 1)
                    return new[] { parts[0] };

                string dir = parts[0];
                return parts.Skip(1).Select(f => Path.Combine(dir, f)).ToArray();
            }
            finally {
                Marshal.FreeHGlobal(ofn.lpstrFile);
            }
        }

        public static string SaveFile(string title, string directory, string defaultName, string filter, string defaultExt)
        {
            var ofn = new OPENFILENAME();
            ofn.lStructSize = Marshal.SizeOf(typeof(OPENFILENAME));
            ofn.lpstrFilter = filter;
            ofn.nMaxFile = BufferChars;
            ofn.lpstrFile = Marshal.AllocHGlobal(BufferChars * 2);
            ofn.lpstrInitialDir = NormalizeDir(directory);
            ofn.lpstrTitle = title;
            ofn.lpstrDefExt = defaultExt;
            ofn.Flags = OFN_EXPLORER | OFN_OVERWRITEPROMPT | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR;

            try {
                var initial = defaultName ?? string.Empty;
                var initialBytes = Encoding.Unicode.GetBytes(initial + "\0");
                Marshal.Copy(initialBytes, 0, ofn.lpstrFile, initialBytes.Length);

                if(!GetSaveFileNameW(ref ofn))
                    return null;

                return Marshal.PtrToStringUni(ofn.lpstrFile);
            }
            finally {
                Marshal.FreeHGlobal(ofn.lpstrFile);
            }
        }

        private static string NormalizeDir(string directory)
        {
            if(string.IsNullOrEmpty(directory))
                return null;
            try {
                return Path.GetFullPath(directory);
            }
            catch {
                return null;
            }
        }

        private static List<string> ReadNullSeparatedStrings(IntPtr ptr)
        {
            var result = new List<string>();
            int offsetChars = 0;
            while(true) {
                string s = Marshal.PtrToStringUni(IntPtr.Add(ptr, offsetChars * 2));
                if(string.IsNullOrEmpty(s))
                    break;
                result.Add(s);
                offsetChars += s.Length + 1;
            }
            return result;
        }
    }
}
