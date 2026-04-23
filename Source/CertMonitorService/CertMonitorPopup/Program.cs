using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Forms;

namespace CertMonitorPopup
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var certs = GetExpiringCertificates();
            if (certs.Count == 0) return;
            Application.Run(new AlertForm(certs));
        }

        public static List<CertInfo> GetExpiringCertificates()
        {
            var results = new List<CertInfo>();
            var today = DateTime.Now.Date;
            int warningDays = 20;
            var stores = new[]
            {
                (System.Security.Cryptography.X509Certificates.StoreName.My, StoreLocation.LocalMachine, "Pessoal (Maquina)"),
                (System.Security.Cryptography.X509Certificates.StoreName.My, StoreLocation.CurrentUser,  "Pessoal (Usuario)"),
            };
            foreach (var (storeName, storeLocation, label) in stores)
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
                            if (!subject.Contains("ICP-Brasil", StringComparison.OrdinalIgnoreCase)) continue;
                            var daysRemaining = (cert.NotAfter.Date - today).Days;
                            if (daysRemaining > warningDays) continue;
                            if (results.Any(r => r.Thumbprint == cert.Thumbprint)) continue;
                            var friendly = cert.FriendlyName?.Trim();
                            var cn = ExtractCN(subject);
                            var cnpj = ExtractCNPJ(cn);
                            var nomeEmpresa = ExtractNomeEmpresa(cn);
                            results.Add(new CertInfo
                            {
                                FriendlyName = !string.IsNullOrEmpty(friendly) ? friendly : cn,
                                NomeEmpresa = nomeEmpresa,
                                CNPJ = cnpj,
                                ExpiryDate = cert.NotAfter,
                                DaysRemaining = daysRemaining,
                                StoreName = label,
                                StoreLocation = storeLocation,
                                StoreName2 = storeName,
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

        public static bool RemoveCertificate(CertInfo cert)
        {
            try
            {
                using var store = new X509Store(cert.StoreName2, cert.StoreLocation);
                store.Open(OpenFlags.ReadWrite);
                var found = store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false);
                if (found.Count > 0) { store.Remove(found[0]); store.Close(); return true; }
                store.Close(); return false;
            }
            catch { return false; }
        }

        public static string ExtractCN(string subject)
        {
            foreach (var part in subject.Split(','))
            {
                var t = part.Trim();
                if (t.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
                    return t.Substring(3).Trim();
            }
            return subject.Length > 60 ? subject.Substring(0, 60) + "..." : subject;
        }

        public static string ExtractCNPJ(string cn)
        {
            // Formato: NOME DA EMPRESA:12345678000100
            var idx = cn.LastIndexOf(':');
            if (idx >= 0 && idx < cn.Length - 1)
            {
                var cnpj = cn.Substring(idx + 1).Trim();
                if (cnpj.Length == 14 && cnpj.All(char.IsDigit))
                    return FormatCNPJ(cnpj);
                return cnpj;
            }
            return "";
        }

        public static string ExtractNomeEmpresa(string cn)
        {
            var idx = cn.LastIndexOf(':');
            if (idx > 0) return cn.Substring(0, idx).Trim();
            return cn;
        }

        private static string FormatCNPJ(string cnpj)
        {
            if (cnpj.Length == 14)
                return $"{cnpj.Substring(0,2)}.{cnpj.Substring(2,3)}.{cnpj.Substring(5,3)}/{cnpj.Substring(8,4)}-{cnpj.Substring(12,2)}";
            return cnpj;
        }
    }

    public class CertInfo
    {
        public string FriendlyName { get; set; } = "";
        public string NomeEmpresa { get; set; } = "";
        public string CNPJ { get; set; } = "";
        public DateTime ExpiryDate { get; set; }
        public int DaysRemaining { get; set; }
        public string StoreName { get; set; } = "";
        public StoreLocation StoreLocation { get; set; }
        public System.Security.Cryptography.X509Certificates.StoreName StoreName2 { get; set; }
        public string Thumbprint { get; set; } = "";
        public bool IsExpired => DaysRemaining < 0;
        public bool IsUrgent => DaysRemaining >= 0 && DaysRemaining <= 7;
    }

    public class AlertForm : Form
    {
        private List<CertInfo> _certs;
        private ListView _listView;
        private Button _btnRemover;

        public AlertForm(List<CertInfo> certs)
        {
            _certs = certs;
            Build();
        }

        private void Build()
        {
            Text = "Certificados Proximos do Vencimento";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Size = new Size(720, 460);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(13, 15, 20);
            TopMost = true;

            var lblTitulo = new Label
            {
                Text = "  Certificados Proximos do Vencimento",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(18, 20, 28),
                Dock = DockStyle.Top,
                Height = 45,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblSub = new Label
            {
                Text = "  Clique em um certificado para ver a mensagem de aviso | Selecione um vencido para remover",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(180, 180, 180),
                BackColor = Color.FromArgb(18, 20, 28),
                Dock = DockStyle.Top,
                Height = 25,
                TextAlign = ContentAlignment.MiddleLeft
            };

            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                BackColor = Color.FromArgb(20, 23, 31),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9),
                BorderStyle = BorderStyle.None,
                MultiSelect = false
            };

            _listView.Columns.Add("Certificado", 310);
            _listView.Columns.Add("Loja", 110);
            _listView.Columns.Add("Vencimento", 95);
            _listView.Columns.Add("Situacao", 110);

            PopulateList();

            _listView.SelectedIndexChanged += (s, e) => UpdateRemoveButton();
            _listView.Click += OnListClick;

            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 55,
                BackColor = Color.FromArgb(18, 20, 28)
            };

            _btnRemover = new Button
            {
                Text = "🗑  Remover Vencido",
                Size = new Size(150, 34),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(60, 60, 60),
                Cursor = Cursors.Hand,
                Location = new Point(385, 10),
                Enabled = false
            };
            _btnRemover.FlatAppearance.BorderSize = 0;
            _btnRemover.Click += OnRemoverClick;

            var btnOk = new Button
            {
                Text = "Fechar",
                Size = new Size(110, 34),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(220, 80, 30),
                Cursor = Cursors.Hand,
                Location = new Point(550, 10)
            };
            btnOk.FlatAppearance.BorderSize = 0;
            btnOk.Click += (s, e) => Close();
            btnOk.MouseEnter += (s, e) => btnOk.BackColor = Color.FromArgb(240, 100, 40);
            btnOk.MouseLeave += (s, e) => btnOk.BackColor = Color.FromArgb(220, 80, 30);

            var lblData = new Label
            {
                Text = $"Verificado em {DateTime.Now:dd/MM/yyyy HH:mm}",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(120, 120, 120),
                Location = new Point(10, 18),
                AutoSize = true,
                BackColor = Color.Transparent
            };

            pnlFooter.Controls.Add(_btnRemover);
            pnlFooter.Controls.Add(btnOk);
            pnlFooter.Controls.Add(lblData);

            Controls.Add(_listView);
            Controls.Add(lblSub);
            Controls.Add(lblTitulo);
            Controls.Add(pnlFooter);
        }

        private void PopulateList()
        {
            _listView.Items.Clear();
            foreach (var cert in _certs)
            {
                string situacao = cert.IsExpired
                    ? $"Vencido ha {Math.Abs(cert.DaysRemaining)}d"
                    : cert.DaysRemaining == 0 ? "Vence HOJE"
                    : $"Vence em {cert.DaysRemaining}d";

                var item = new ListViewItem(cert.FriendlyName);
                item.SubItems.Add(cert.StoreName);
                item.SubItems.Add(cert.ExpiryDate.ToString("dd/MM/yyyy"));
                item.SubItems.Add(situacao);
                item.Tag = cert;

                item.ForeColor = cert.IsExpired ? Color.FromArgb(255, 100, 100)
                    : cert.IsUrgent ? Color.FromArgb(255, 195, 120)
                    : Color.FromArgb(250, 220, 60);

                _listView.Items.Add(item);
            }
        }

        private void OnListClick(object sender, EventArgs e)
        {
            if (_listView.SelectedItems.Count == 0) return;
            var cert = _listView.SelectedItems[0].Tag as CertInfo;
            if (cert == null) return;

            // Monta mensagem formatada para WhatsApp
            string status;
            if (cert.IsExpired)
                status = $"ja venceu ha {Math.Abs(cert.DaysRemaining)} dia(s) (em {cert.ExpiryDate:dd/MM/yyyy})";
            else if (cert.DaysRemaining == 0)
                status = "vence HOJE";
            else
                status = $"vence em {cert.DaysRemaining} dia(s), no dia {cert.ExpiryDate:dd/MM/yyyy}";

            string cnpj = string.IsNullOrEmpty(cert.CNPJ) ? "" : $" (CNPJ: {cert.CNPJ})";
            string empresa = string.IsNullOrEmpty(cert.NomeEmpresa) ? cert.FriendlyName : cert.NomeEmpresa;

            string mensagem =
                $"Ola!\n\n" +
                $"Passando para informar que o certificado digital da empresa\n" +
                $"{empresa}{cnpj}\n\n" +
                $"{status}.\n\n" +
                $"Para evitar interrupções nos serviços fiscais e operacionais,\n" +
                $"recomendamos programar a renovação o quanto antes.\n\n" +
                $"Qualquer dúvida estamos a disposição!\n\n" +
                $"Atenciosamente.";

            var msgForm = new MensagemForm(empresa, mensagem);
            msgForm.ShowDialog(this);
        }

        private void UpdateRemoveButton()
        {
            if (_listView.SelectedItems.Count > 0)
            {
                var cert = _listView.SelectedItems[0].Tag as CertInfo;
                bool podeRemover = cert != null && cert.IsExpired;
                _btnRemover.Enabled = podeRemover;
                _btnRemover.BackColor = podeRemover ? Color.FromArgb(180, 30, 30) : Color.FromArgb(60, 60, 60);
            }
            else
            {
                _btnRemover.Enabled = false;
                _btnRemover.BackColor = Color.FromArgb(60, 60, 60);
            }
        }

        private void OnRemoverClick(object sender, EventArgs e)
        {
            if (_listView.SelectedItems.Count == 0) return;
            var cert = _listView.SelectedItems[0].Tag as CertInfo;
            if (cert == null || !cert.IsExpired) return;

            var resultado = MessageBox.Show(
                $"Voce esta prestes a REMOVER permanentemente o certificado:\n\n" +
                $"  {cert.FriendlyName}\n" +
                $"  Vencido em: {cert.ExpiryDate:dd/MM/yyyy}\n" +
                $"  Loja: {cert.StoreName}\n\n" +
                $"ATENCAO: Esta acao e IRREVERSIVEL!\n" +
                $"O certificado sera removido definitivamente do Windows Certificate Store.\n\n" +
                $"Deseja continuar?",
                "Confirmar Remocao",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (resultado != DialogResult.Yes) return;

            bool removido = Program.RemoveCertificate(cert);
            if (removido)
            {
                MessageBox.Show($"Certificado removido com sucesso!\n\n{cert.FriendlyName}",
                    "Removido", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _certs.Remove(cert);
                PopulateList();
                if (_certs.Count == 0) Close();
            }
            else
            {
                MessageBox.Show("Nao foi possivel remover o certificado.\nVerifique se voce tem permissao de Administrador.",
                    "Erro ao Remover", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    // Janela da mensagem para printar e enviar no WhatsApp
    public class MensagemForm : Form
    {
        public MensagemForm(string empresa, string mensagem)
        {
            Text = $"Mensagem de Aviso — {empresa}";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Size = new Size(520, 420);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(18, 20, 28);
            TopMost = true;

            var lblTitulo = new Label
            {
                Text = "  Mensagem de Aviso de Vencimento",
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(25, 28, 38),
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft
            };

            var lblDica = new Label
            {
                Text = "  Tire um print desta janela e envie pelo WhatsApp",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(100, 200, 100),
                BackColor = Color.FromArgb(25, 28, 38),
                Dock = DockStyle.Top,
                Height = 24,
                TextAlign = ContentAlignment.MiddleLeft
            };

            // Caixa da mensagem — fundo branco para print limpo
            var pnlMsg = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.White,
                Padding = new Padding(20)
            };

            var txtMsg = new RichTextBox
            {
                Text = mensagem,
                Font = new Font("Arial Unicode MS", 11),
                ForeColor = Color.FromArgb(30, 30, 30),
                BackColor = Color.White,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = RichTextBoxScrollBars.None
            };

            pnlMsg.Controls.Add(txtMsg);

            var pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                BackColor = Color.FromArgb(25, 28, 38)
            };

            var btnFechar = new Button
            {
                Text = "Fechar",
                Size = new Size(110, 32),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.FromArgb(220, 80, 30),
                Cursor = Cursors.Hand,
                Location = new Point(385, 9)
            };
            btnFechar.FlatAppearance.BorderSize = 0;
            btnFechar.Click += (s, e) => Close();

            pnlFooter.Controls.Add(btnFechar);

            Controls.Add(pnlMsg);
            Controls.Add(lblDica);
            Controls.Add(lblTitulo);
            Controls.Add(pnlFooter);
        }
    }
}
