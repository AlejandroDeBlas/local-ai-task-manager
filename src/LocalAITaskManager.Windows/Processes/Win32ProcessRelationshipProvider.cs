using System.ComponentModel;
using System.Runtime.InteropServices;
using LocalAITaskManager.Core.Abstractions;

namespace LocalAITaskManager.Windows.Processes;

public sealed class Win32ProcessRelationshipProvider : IProcessRelationshipProvider
{
    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr INVALID_HANDLE_VALUE = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public UIntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr hObject);

    public IProcessRelationshipSnapshot GetSnapshot()
    {
        var parentMap = new Dictionary<int, int>();

        IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == INVALID_HANDLE_VALUE)
        {
            return new ProcessRelationshipSnapshot(parentMap);
        }

        try
        {
            var entry = new PROCESSENTRY32W();
            entry.dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>();

            if (Process32FirstW(snapshot, ref entry))
            {
                do
                {
                    int pid = (int)entry.th32ProcessID;
                    int parentPid = (int)entry.th32ParentProcessID;

                    if (pid > 0)
                    {
                        parentMap[pid] = parentPid;
                    }
                }
                while (Process32NextW(snapshot, ref entry));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return new ProcessRelationshipSnapshot(parentMap);
    }
}
