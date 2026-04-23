using System;
using System.IO;
using System.Runtime.InteropServices;

namespace CertMonitorService
{
    public static class PopupLauncher
    {
        private static string PopupExePath => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "CertMonitorPopup.exe");

        public static void ShowPopupInSession(int sessionId, object? ignored)
        {
            try
            {
                LaunchProcessInSession(sessionId, PopupExePath, "--scan");
            }
            catch (Exception ex)
            {
                CertMonitorWindowsService.Log($"[PopupLauncher] Erro na sessao {sessionId}: {ex.Message}");
            }
        }

        private static void LaunchProcessInSession(int sessionId, string exePath, string args)
        {
            IntPtr hToken = IntPtr.Zero;
            IntPtr hDupToken = IntPtr.Zero;
            try
            {
                if (!WTSQueryUserToken(sessionId, out hToken))
                    throw new Exception($"WTSQueryUserToken falhou. Erro: {Marshal.GetLastWin32Error()}");

                var sa = new SECURITY_ATTRIBUTES { nLength = Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES)) };
                if (!DuplicateTokenEx(hToken, 0x10000000, ref sa, 2, 1, out hDupToken))
                    throw new Exception($"DuplicateTokenEx falhou. Erro: {Marshal.GetLastWin32Error()}");

                var si = new STARTUPINFO
                {
                    cb = Marshal.SizeOf(typeof(STARTUPINFO)),
                    lpDesktop = "winsta0\\default",
                    dwFlags = 0x00000001,
                    wShowWindow = 1
                };

                string cmdLine = $"\"{exePath}\" {args}";
                bool created = CreateProcessAsUser(hDupToken, null, cmdLine,
                    IntPtr.Zero, IntPtr.Zero, false, 0x00000010,
                    IntPtr.Zero, Path.GetDirectoryName(exePath), ref si, out var pi);

                if (!created)
                    throw new Exception($"CreateProcessAsUser falhou. Erro: {Marshal.GetLastWin32Error()}");

                CloseHandle(pi.hProcess);
                CloseHandle(pi.hThread);
            }
            finally
            {
                if (hToken != IntPtr.Zero) CloseHandle(hToken);
                if (hDupToken != IntPtr.Zero) CloseHandle(hDupToken);
            }
        }

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQueryUserToken(int sessionId, out IntPtr phToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DuplicateTokenEx(IntPtr hExistingToken, uint dwDesiredAccess,
            ref SECURITY_ATTRIBUTES lpTokenAttributes, int ImpersonationLevel, int TokenType, out IntPtr phNewToken);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool CreateProcessAsUser(IntPtr hToken, string? lpApplicationName, string lpCommandLine,
            IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags,
            IntPtr lpEnvironment, string? lpCurrentDirectory, ref STARTUPINFO lpStartupInfo, out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct SECURITY_ATTRIBUTES { public int nLength; public IntPtr lpSecurityDescriptor; public bool bInheritHandle; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct STARTUPINFO
        {
            public int cb;
            public string? lpReserved, lpDesktop, lpTitle;
            public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
            public short wShowWindow, cbReserved2;
            public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    }
}
