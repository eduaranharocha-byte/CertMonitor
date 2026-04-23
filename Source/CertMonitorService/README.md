# 🔐 CertMonitor — Serviço Windows para Múltiplos Usuários

Monitor de certificados para Windows Server com múltiplos acessos RDP simultâneos.
Instalação **única** no servidor — alerta cada usuário individualmente no momento do seu login.

---

## 🏗️ Arquitetura

```
┌─────────────────────────────────────────────────────┐
│              WINDOWS SERVER                         │
│                                                     │
│  ┌─────────────────────────────────┐                │
│  │  CertMonitorService (SYSTEM)    │  ← sempre ativo│
│  │  • Lê Certificate Store         │                │
│  │  • Detecta logins RDP           │                │
│  │  • Verifica a cada 6h           │                │
│  └────────────┬────────────────────┘                │
│               │ detecta login                       │
│    ┌──────────▼──────────────────┐                  │
│    │  Sessão RDP — João (08:00)  │ → popup aparece  │
│    │  Sessão RDP — Maria (11:30) │ → popup aparece  │
│    │  Sessão RDP — Pedro (15:00) │ → popup aparece  │
│    └─────────────────────────────┘                  │
└─────────────────────────────────────────────────────┘
```

---

## 📦 Estrutura de Arquivos

```
C:\Ferramentas\CertMonitor\
├── CertMonitorService.exe   ← Serviço principal (roda como SYSTEM)
└── CertMonitorPopup.exe     ← Janela de alerta (lançada por sessão)
```

---

## 🔨 Compilação

Você precisará do [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0).

### 1. Compilar o Serviço
```powershell
cd CertMonitorService
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
# Saída: bin\Release\net6.0-windows\win-x64\publish\CertMonitorService.exe
```

### 2. Compilar o Popup
```powershell
cd CertMonitorService\CertMonitorPopup
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
# Saída: bin\Release\net6.0-windows\win-x64\publish\CertMonitorPopup.exe
```

---

## 🚀 Instalação no Servidor (uma única vez)

### Passo 1 — Criar pasta e copiar arquivos
```powershell
New-Item -ItemType Directory -Force "C:\Ferramentas\CertMonitor"
Copy-Item CertMonitorService.exe "C:\Ferramentas\CertMonitor\"
Copy-Item CertMonitorPopup.exe   "C:\Ferramentas\CertMonitor\"
```

### Passo 2 — Instalar o serviço (como Administrador)
```powershell
# Abra o PowerShell como Administrador e execute:
.\Install-CertMonitor.ps1 -Action install
```

✅ Pronto! O serviço inicia automaticamente e sobrevive a reinicializações (inclusive o reinício de domingo).

---

## 🖥️ Comandos Úteis

```powershell
# Ver status e log
.\Install-CertMonitor.ps1 -Action status

# Testar em modo console (sem instalar)
.\CertMonitorService.exe --console

# Desinstalar
.\Install-CertMonitor.ps1 -Action uninstall

# Via sc.exe / serviços do Windows
sc query CertMonitorService
net start CertMonitorService
net stop CertMonitorService
```

---

## 📋 Comportamento Esperado

| Evento | O que acontece |
|--------|---------------|
| Servidor reinicia (domingo) | Serviço sobe automaticamente com o Windows |
| Usuário faz RDP (qualquer horário) | Popup aparece na sessão dele em ~30 segundos |
| Certificado OK (>20 dias) | Nenhum popup, nenhuma interrupção |
| Certificado vence em 8–20 dias | 🟡 Popup amarelo de aviso |
| Certificado vence em 0–7 dias | 🟠 Popup laranja urgente |
| Certificado já vencido | 🔴 Popup vermelho crítico |
| Usuário desconecta e reconecta | Popup aparece novamente no novo login |

---

## 🎨 Cores dos Alertas

| Cor | Faixa | Texto |
|-----|-------|-------|
| 🔴 Vermelho | Já vencido | "VENCIDO há Xd" |
| 🟠 Laranja | 0 a 7 dias | "⚡ X dia(s)" |
| 🟡 Amarelo | 8 a 20 dias | "⏳ X dia(s)" |

---

## 📁 Lojas Verificadas

- Pessoal (Máquina Local)
- Pessoal (Usuário Atual)
- Autoridades Raiz Confiáveis
- Pessoas Confiáveis
- Web Hosting
- Autoridades Intermediárias

---

## 📄 Log

O serviço grava em:
```
C:\ProgramData\CertMonitor\certmonitor.log
```

Exemplo de log:
```
[2025-01-15 08:02:11] Serviço CertMonitor iniciado.
[2025-01-15 08:02:11] Aguardando conexões de usuários...
[2025-01-15 08:03:45] Usuário conectado: joao (Sessão 2)
[2025-01-15 08:03:45] Encontrados 2 certificado(s) a vencer. Exibindo alerta para sessão 2...
[2025-01-15 08:03:46] Popup lançado com sucesso na sessão 2.
[2025-01-15 11:31:02] Usuário conectado: maria (Sessão 3)
[2025-01-15 11:31:02] Popup lançado com sucesso na sessão 3.
```

---

## ⚙️ Ajustes

Para alterar o prazo de alerta (padrão 20 dias), edite em `CertificateService.cs`:
```csharp
private static readonly int WARNING_DAYS = 20;
```

O serviço verifica novas sessões a cada **30 segundos** — intervalo muito leve (~0% CPU).
