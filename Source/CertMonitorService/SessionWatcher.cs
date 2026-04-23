using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Collections.Generic;

namespace CertMonitorService
{
    /// <summary>
    /// Monitora eventos de sessão RDP/local usando a API WTS do Windows.
    /// Detecta cada login individualmente, independente do horário.
    /// </summary>
    public class SessionWatcher
    {
        public event Action<int, string> SessionConnected;

        private Thread _watchThread;
        private bool _running;

        // Sessions já notificadas neste ciclo (evita alertas duplicados)
        private readonly HashSet<int> _notifiedSessions = new HashSet<int>();
        private readonly object _lock = new object();

        public void Start()
        {
            _running = true;
            _watchThread = new Thread(WatchLoop)
            {
                IsBackground = true,
                Name = "SessionWatcherThread"
            };
            _watchThread.Start();
        }

        public void Stop()
        {
            _running = false;
            _watchThread?.Join(3000);
        }

        private void WatchLoop()
        {
            while (_running)
            {
                try
                {
                    CheckActiveSessions();
                }
                catch (Exception ex)
                {
                    CertMonitorWindowsService.Log($"[SessionWatcher] Erro: {ex.Message}");
                }

                // Verifica a cada 30 segundos — levíssimo
                Thread.Sleep(TimeSpan.FromSeconds(30));
            }
        }

        private void CheckActiveSessions()
        {
            var activeSessions = WtsApi.GetActiveSessions();

            lock (_lock)
            {
                foreach (var session in activeSessions)
                {
                    // Ignora sessão 0 (serviços) e sessões já notificadas
                    if (session.SessionId == 0) continue;
                    if (_notifiedSessions.Contains(session.SessionId)) continue;

                    // Sessão nova e ativa — notifica
                    _notifiedSessions.Add(session.SessionId);
                    CertMonitorWindowsService.Log($"[SessionWatcher] Nova sessão detectada: ID={session.SessionId}, Usuário={session.Username}");
                    SessionConnected?.Invoke(session.SessionId, session.Username);
                }

                // Remove sessões que já encerraram (para re-alertar se o usuário reconectar)
                var activeIds = new HashSet<int>();
                foreach (var s in activeSessions) activeIds.Add(s.SessionId);
                _notifiedSessions.RemoveWhere(id => !activeIds.Contains(id));
            }
        }
    }

    public class SessionInfo
    {
        public int SessionId { get; set; }
        public string Username { get; set; }
    }

    /// <summary>
    /// Wrapper para a API WTS (Windows Terminal Services) via P/Invoke
    /// </summary>
    public static class WtsApi
    {
        [DllImport("wtsapi32.dll")]
        private static extern bool WTSEnumerateSessions(
            IntPtr hServer, int Reserved, int Version,
            ref IntPtr ppSessionInfo, ref int pCount);

        [DllImport("wtsapi32.dll")]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("wtsapi32.dll", CharSet = CharSet.Auto)]
        private static extern bool WTSQuerySessionInformation(
            IntPtr hServer, int sessionId, WTS_INFO_CLASS wtsInfoClass,
            out IntPtr ppBuffer, out int pBytesReturned);

        private static readonly IntPtr WTS_CURRENT_SERVER_HANDLE = IntPtr.Zero;

        private enum WTS_CONNECTSTATE_CLASS { Active, Connected, ConnectQuery, Shadow, Disconnected, Idle, Listen, Reset, Down, Init }
        private enum WTS_INFO_CLASS { WTSInitialProgram, WTSApplicationName, WTSWorkingDirectory, WTSOEMId, WTSSessionId, WTSUserName, WTSWinStationName, WTSDomainName, WTSConnectState, WTSClientBuildNumber, WTSClientName, WTSClientDirectory, WTSClientProductId, WTSClientHardwareId, WTSClientAddress, WTSClientDisplay, WTSClientProtocolType }

        [StructLayout(LayoutKind.Sequential)]
        private struct WTS_SESSION_INFO
        {
            public int SessionId;
            [MarshalAs(UnmanagedType.LPStr)] public string pWinStationName;
            public WTS_CONNECTSTATE_CLASS State;
        }

        public static List<SessionInfo> GetActiveSessions()
        {
            var result = new List<SessionInfo>();
            IntPtr ppSessionInfo = IntPtr.Zero;
            int count = 0;

            if (!WTSEnumerateSessions(WTS_CURRENT_SERVER_HANDLE, 0, 1, ref ppSessionInfo, ref count))
                return result;

            try
            {
                int dataSize = Marshal.SizeOf(typeof(WTS_SESSION_INFO));
                IntPtr current = ppSessionInfo;

                for (int i = 0; i < count; i++)
                {
                    var si = (WTS_SESSION_INFO)Marshal.PtrToStructure(current, typeof(WTS_SESSION_INFO));
                    current = IntPtr.Add(current, dataSize);

                    // Apenas sessões ativas
                    if (si.State != WTS_CONNECTSTATE_CLASS.Active &&
                        si.State != WTS_CONNECTSTATE_CLASS.Connected)
                        continue;

                    string username = GetSessionUsername(si.SessionId);
                    if (string.IsNullOrEmpty(username)) continue;

                    result.Add(new SessionInfo
                    {
                        SessionId = si.SessionId,
                        Username = username
                    });
                }
            }
            finally
            {
                WTSFreeMemory(ppSessionInfo);
            }

            return result;
        }

        private static string GetSessionUsername(int sessionId)
        {
            try
            {
                WTSQuerySessionInformation(WTS_CURRENT_SERVER_HANDLE, sessionId,
                    WTS_INFO_CLASS.WTSUserName, out IntPtr buf, out int _);

                string name = Marshal.PtrToStringAnsi(buf);
                WTSFreeMemory(buf);
                return name?.Trim();
            }
            catch { return ""; }
        }
    }
}
