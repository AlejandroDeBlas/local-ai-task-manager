using System.Runtime.InteropServices;
using LocalAITaskManager.Core.Abstractions;

namespace LocalAITaskManager.Windows.Processes;

public sealed class WindowsCommandLineParser : ICommandLineParser
{
    [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CommandLineToArgvW(
        [MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine,
        out int pNumArgs
    );

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);

    public IReadOnlyList<string> ParseArguments(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        IntPtr argvPtr = CommandLineToArgvW(commandLine, out int numArgs);
        if (argvPtr == IntPtr.Zero || numArgs <= 0)
        {
            return [];
        }

        try
        {
            var args = new string[numArgs];
            for (int i = 0; i < numArgs; i++)
            {
                IntPtr argPtr = Marshal.ReadIntPtr(argvPtr, i * IntPtr.Size);
                args[i] = Marshal.PtrToStringUni(argPtr) ?? string.Empty;
            }

            return args;
        }
        finally
        {
            LocalFree(argvPtr);
        }
    }
}
