using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Linq;

namespace CertMonitorService
{
    public class CertificateInfo
    {
        public string FriendlyName { get; set; } = "";
        public string Subject { get; set; } = "";
        public DateTime ExpiryDate { get; set; }
        public int DaysRemaining { get; set; }
        public string StoreName { get; set; } = "";
        public string Thumbprint { get; set; } = "";
        public bool IsExpired => DaysRemaining < 0;
        public bool IsUrgent => DaysRemaining >= 0 && DaysRemaining <= 7;
        public bool IsWarning => DaysRemaining > 7 && DaysRemaining <= 20;
    }

    public static class CertificateService
    {
        private static readonly int WARNING_DAYS = 20;

        private static readonly (System.Security.Cryptography.X509Certificates.StoreName name, StoreLocation location, string label)[] Stores =
        {
            (System.Security.Cryptography.X509Certificates.StoreName.My, StoreLocation.LocalMachine, "Pessoal (Maquina)"),
            (System.Security.Cryptography.X509Certificates.StoreName.My, StoreLocation.CurrentUser,  "Pessoal (Usuario)"),
        };

        public static List<CertificateInfo> GetExpiringCertificates()
        {
            var results = new List<CertificateInfo>();
            var today = DateTime.Now.Date;

            foreach (var (storeName, storeLocation, label) in Stores)
            {
                try
                {
                    using var store = new X509Store(storeName, storeLocation);
                    store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                    foreach (X509Certificate2 cert in store.Certificates)
                    {
                        try
                        {
                            var subject = cert.Subject ?? "";

                            if (!subject.Contains("ICP-Brasil", StringComparison.OrdinalIgnoreCase))
                                continue;

                            var expiry = cert.NotAfter.Date;
                            var daysRemaining = (expiry - today).Days;

                            if (daysRemaining > WARNING_DAYS) continue;
                            if (results.Any(r => r.Thumbprint == cert.Thumbprint)) continue;

                            var friendly = cert.FriendlyName?.Trim();

                            results.Add(new CertificateInfo
                            {
                                FriendlyName = !string.IsNullOrEmpty(friendly) ? friendly : ExtractCN(subject),
                                Subject = subject,
                                ExpiryDate = cert.NotAfter,
                                DaysRemaining = daysRemaining,
                                StoreName = label,
                                Thumbprint = cert.Thumbprint ?? ""
                            });
                        }
                        catch { }
                        finally { cert?.Dispose(); }
                    }
                    store.Close();
                }
                catch { }
            }

            return results.OrderBy(c => c.DaysRemaining).ToList();
        }

        private static string ExtractCN(string subject)
        {
            if (string.IsNullOrEmpty(subject)) return "Certificado sem nome";
            foreach (var part in subject.Split(','))
            {
                var t = part.Trim();
                if (t.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                    return t.Substring(3).Trim();
            }
            return subject.Length > 60 ? subject.Substring(0, 60) + "..." : subject;
        }
    }
}
