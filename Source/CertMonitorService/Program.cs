using System;
using System.ServiceProcess;

namespace CertMonitorService
{
    static class Program
    {
        static void Main(string[] args)
        {
            // Permite rodar em modo console para testes: CertMonitor.exe --console
            if (args.Length > 0 && args[0] == "--console")
            {
                Console.WriteLine("[CertMonitor] Rodando em modo console para teste...");
                var svc = new CertMonitorWindowsService();
                svc.StartInConsole();
                Console.WriteLine("Pressione ENTER para encerrar.");
                Console.ReadLine();
                svc.StopInConsole();
                return;
            }

            ServiceBase.Run(new CertMonitorWindowsService());
        }
    }
}
