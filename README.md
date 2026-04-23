# 🔐 CertMonitor — Monitor de Certificados Digitais ICP-Brasil

> Ferramenta para Windows Server que monitora automaticamente certificados digitais (e-CNPJ, e-CPF) instalados na máquina e alerta cada usuário individualmente no momento do login via RDP, quando há certificados próximos do vencimento.

---

## 📋 Índice

- [O que é e por que existe](#-o-que-é-e-por-que-existe)
- [Como funciona](#-como-funciona)
- [Estrutura do projeto](#-estrutura-do-projeto)
- [Pré-requisitos](#-pré-requisitos)
- [Compilação](#-compilação)
- [Instalação no servidor](#-instalação-no-servidor)
- [Como usar](#-como-usar)
- [Funcionalidades](#-funcionalidades)
- [Cores dos alertas](#-cores-dos-alertas)
- [Comandos úteis](#-comandos-úteis)
- [Personalização](#-personalização)
- [Segurança](#-segurança)

---

## 💡 O que é e por que existe

Empresas de contabilidade, escritórios fiscais e prestadores de serviços digitais gerenciam dezenas de certificados digitais ICP-Brasil (e-CNPJ e e-CPF) de seus clientes. Esses certificados têm prazo de validade e, quando vencem, causam interrupções em serviços fiscais como emissão de NF-e, acesso ao e-CAC, transmissão de obrigações acessórias, entre outros.

O **CertMonitor** resolve esse problema monitorando automaticamente todos os certificados instalados no Windows Certificate Store e alertando os usuários **antes** que o problema aconteça.

---

## ⚙️ Como funciona

O sistema é composto por dois executáveis que trabalham juntos:

```
┌─────────────────────────────────────────────────────┐
│              WINDOWS SERVER                         │
│                                                     │
│  ┌──────────────────────────────────────┐           │
│  │  CertMonitorService.exe              │           │
│  │  • Roda como Serviço Windows         │           │
│  │  • Conta SYSTEM — sempre ativo       │           │
│  │  • Detecta logins RDP                │           │
│  │  • Funciona mesmo sem usuário logado │           │
│  └──────────────┬───────────────────────┘           │
│                 │ detecta novo login                │
│      ┌──────────▼──────────────────────┐            │
│      │  CertMonitorPopup.exe           │            │
│      │  • Abre na sessão do usuário    │            │
│      │  • Lê certificados do perfil   │            │
│      │  • Exibe popup de alerta        │            │
│      └─────────────────────────────────┘            │
└─────────────────────────────────────────────────────┘
```

### Fluxo completo

```
Servidor reinicia (domingo automático)
        ↓
Serviço sobe automaticamente (conta SYSTEM)
        ↓
João faz RDP às 08h → popup aparece só para João
Maria faz RDP às 11h → popup aparece só para Maria
Pedro faz RDP às 15h → popup aparece só para Pedro
```

Cada usuário é alertado **no momento do seu próprio login**, independente do horário.

---

## 📁 Estrutura do projeto

```
CertMonitor/
└── Source/
    ├── .gitignore
    └── CertMonitorService/
        ├── Program.cs                    # Ponto de entrada do serviço
        ├── CertMonitorWindowsService.cs  # Lógica principal do serviço Windows
        ├── SessionWatcher.cs             # Monitora logins RDP via API WTS
        ├── PopupLauncher.cs              # Lança o popup na sessão do usuário
        ├── CertificateService.cs         # Lê o Windows Certificate Store
        ├── CertMonitorService.csproj     # Configuração do projeto do serviço
        ├── Install-CertMonitor.ps1       # Script de instalação/desinstalação
        └── CertMonitorPopup/
            ├── Program.cs                # Interface visual do popup
            └── CertMonitorPopup.csproj   # Configuração do projeto do popup
```

### O que cada arquivo faz

| Arquivo | Função |
|---|---|
| `Program.cs` (serviço) | Inicializa o serviço Windows. Suporta modo `--console` para testes |
| `CertMonitorWindowsService.cs` | Gerencia o ciclo de vida do serviço, registra logs e coordena os componentes |
| `SessionWatcher.cs` | Usa a API WTS do Windows para detectar novas sessões RDP a cada 30 segundos |
| `PopupLauncher.cs` | Usa `CreateProcessAsUser` para abrir o popup na sessão correta do usuário |
| `CertificateService.cs` | Abre o Windows Certificate Store e filtra certificados ICP-Brasil próximos do vencimento |
| `Program.cs` (popup) | Interface gráfica com lista de certificados, mensagem de aviso e opção de remoção |
| `Install-CertMonitor.ps1` | Instala, desinstala e verifica o status do serviço via PowerShell |

---

## 🖥️ Pré-requisitos

### Para compilar
- Windows 10/11 ou Windows Server 2016+
- [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)

### Para executar
- Windows 10/11 ou Windows Server 2016+
- **.NET não é necessário** — os executáveis são self-contained
- Permissão de **Administrador** para instalar o serviço

---

## 🔨 Compilação

### 1. Compilar o Serviço

```powershell
cd Source\CertMonitorService
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Saída: `bin\Release\net6.0-windows\win-x64\publish\CertMonitorService.exe`

### 2. Compilar o Popup

```powershell
cd Source\CertMonitorService\CertMonitorPopup
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

Saída: `bin\Release\net6.0-windows\win-x64\publish\CertMonitorPopup.exe`

---

## 🚀 Instalação no servidor

### Passo 1 — Criar pasta e copiar executáveis

```powershell
New-Item -ItemType Directory -Force "C:\Ferramentas\CertMonitor"
Copy-Item "CertMonitorService.exe" "C:\Ferramentas\CertMonitor\"
Copy-Item "CertMonitorPopup.exe"   "C:\Ferramentas\CertMonitor\"
Copy-Item "Install-CertMonitor.ps1" "C:\Ferramentas\CertMonitor\"
```

### Passo 2 — Instalar o serviço (como Administrador)

```powershell
cd "C:\Ferramentas\CertMonitor"
powershell -ExecutionPolicy Bypass -File ".\Install-CertMonitor.ps1" -Action install
```

### Passo 3 — Verificar instalação

```powershell
powershell -ExecutionPolicy Bypass -File ".\Install-CertMonitor.ps1" -Action status
```

✅ Pronto! O serviço inicia automaticamente e sobrevive a reinicializações.

---

## 🖱️ Como usar

### Uso automático (recomendado)
Após instalado, o serviço funciona sozinho. A cada login RDP o popup aparece automaticamente se houver certificados próximos do vencimento.

### Verificação manual
Para verificar a qualquer momento sem esperar o login:

```powershell
& "C:\Ferramentas\CertMonitor\CertMonitorPopup.exe" --scan
```

Ou simplesmente **dois cliques** no `CertMonitorPopup.exe`.

---

## ✨ Funcionalidades

### 1. Alerta automático no login RDP
Cada usuário recebe o popup no momento do seu próprio login, independente do horário.

### 2. Filtro ICP-Brasil
Exibe apenas certificados digitais brasileiros (e-CNPJ, e-CPF) — ignora certificados internos do sistema operacional como Microsoft Root, VeriSign, etc.

### 3. Mensagem de aviso para WhatsApp
Ao clicar em qualquer certificado da lista, abre uma janela com uma mensagem formatada pronta para ser printada e enviada ao cliente via WhatsApp:

```
Olá!

Passando para informar que o certificado digital da empresa
NOME DA EMPRESA (CNPJ: XX.XXX.XXX/XXXX-XX)

vence em X dia(s), no dia DD/MM/AAAA.

Para evitar interrupções nos serviços fiscais e operacionais,
recomendamos programar a renovação o quanto antes.

Qualquer dúvida estamos à disposição!

Atenciosamente.
```

### 4. Remoção de certificados vencidos
Certificados já vencidos podem ser removidos diretamente pelo painel com proteção contra remoção acidental — uma confirmação com aviso de irreversibilidade é exibida antes de qualquer exclusão.

### 5. Log automático
Todas as atividades são registradas em:
```
C:\ProgramData\CertMonitor\certmonitor.log
```

---

## 🎨 Cores dos alertas

| Cor | Situação |
|---|---|
| 🔴 Vermelho | Certificado **já vencido** |
| 🟠 Laranja | Vence em **0 a 7 dias** (urgente) |
| 🟡 Amarelo | Vence em **8 a 20 dias** (aviso) |

---

## 🛠️ Comandos úteis

```powershell
# Instalar o serviço
.\Install-CertMonitor.ps1 -Action install

# Ver status e log
.\Install-CertMonitor.ps1 -Action status

# Desinstalar
.\Install-CertMonitor.ps1 -Action uninstall

# Iniciar/parar/reiniciar
Start-Service -Name CertMonitorService
Stop-Service -Name CertMonitorService -Force
Restart-Service -Name CertMonitorService -Force

# Verificar certificados manualmente (PowerShell)
Get-ChildItem Cert:\CurrentUser\My | Where-Object { 
    $_.Subject -like "*ICP-Brasil*" 
} | Select-Object FriendlyName, NotAfter
```

---

## ⚙️ Personalização

### Alterar prazo de alerta (padrão: 20 dias)
Em `CertificateService.cs`, linha:
```csharp
private static readonly int WARNING_DAYS = 20;
```

### Alterar intervalo de verificação de sessões (padrão: 30 segundos)
Em `SessionWatcher.cs`:
```csharp
Thread.Sleep(TimeSpan.FromSeconds(30));
```

### Alterar texto da mensagem de aviso
Em `CertMonitorPopup\Program.cs`, método `OnListClick`.

---

## 🔒 Segurança

- O projeto **não armazena** nenhum dado de certificado em disco
- Nenhum CNPJ, senha ou chave privada é gravado em arquivo
- Os certificados são lidos em tempo real do Windows Certificate Store
- O log registra apenas eventos operacionais (login de usuário, quantidade de certificados encontrados)
- A remoção de certificados exige confirmação explícita e é irreversível

---

## 📄 Licença

Projeto desenvolvido para uso interno. Livre para adaptação e redistribuição.
