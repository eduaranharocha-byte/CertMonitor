using System;
using System.ServiceProcess;
using System.Timers;
using System.IO;

namespace CertMonitorService
{
    public class CertMonitorWindowsService : ServiceBase
    {
        private SessionWatcher _sessionWatcher;
        private System.Timers.Timer _dailyCheckTimer;
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "CertMonitor", "certmonitor.log");

        public CertMonitorWindowsService()
        {
            ServiceName = "CertMonitorService";
            CanStop = true;
            CanPauseAndContinue = false;
            AutoLog = true;
        }

        protected override void OnStart(string[] args)
        {
            Log("Servico CertMonitor iniciado.");
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath) ?? "C:\\ProgramData\\CertMonitor");
            _sessionWatcher = new SessionWatcher();
            _sessionWatcher.SessionConnected += OnUserSessionConnected;
            _sessionWatcher.Start();
            _dailyCheckTimer = new System.Timers.Timer(TimeSpan.FromHours(12).TotalMilliseconds);
            _dailyCheckTimer.Elapsed += (s, e) => Log("[Status] Servico ativo.");
            _dailyCheckTimer.Start();
            Log("Aguardando conexoes de usuarios...");
        }

        protected override void OnStop()
        {
            Log("Servico CertMonitor parado.");
            _sessionWatcher?.Stop();
            _dailyCheckTimer?.Stop();
            _dailyCheckTimer?.Dispose();
        }

        private void OnUserSessionConnected(int sessionId, string username)
        {
            Log($"Usuario conectado: {username} (Sessao {sessionId})");
            try
            {
                // Lança o popup na sessão do usuário — ele mesmo busca os certificados
                PopupLauncher.ShowPopupInSession(sessionId, null);
                Log($"Popup lancado para sessao {sessionId}.");
            }
            catch (Exception ex)
            {
                Log($"Erro ao lancar popup para sessao {sessionId}: {ex.Message}");
            }
        }

        public static void Log(string message)
        {
            try
            {
                var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                File.AppendAllText(LogPath, line + Environment.NewLine);
                Console.WriteLine(line);
            }
            catch { }
        }

        public void StartInConsole() => OnStart(Array.Empty<string>());
        public void StopInConsole() => OnStop();
    }
}
