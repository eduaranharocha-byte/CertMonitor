# ============================================================
#  CertMonitor — Script de Instalação/Desinstalação
#  Execute como Administrador no Windows Server
# ============================================================

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("install","uninstall","status","test")]
    [string]$Action = "install"
)

$ServiceName  = "CertMonitorService"
$DisplayName  = "Monitor de Certificados SSL"
$Description  = "Monitora certificados instalados e alerta usuários ao fazer RDP quando há vencimentos em até 20 dias."
$InstallDir   = "C:\Ferramentas\CertMonitor"
$ServiceExe   = Join-Path $InstallDir "CertMonitorService.exe"
$PopupExe     = Join-Path $InstallDir "CertMonitorPopup.exe"
$LogDir       = "$env:ProgramData\CertMonitor"

function Require-Admin {
    if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
        [Security.Principal.WindowsBuiltInRole]::Administrator)) {
        Write-Error "Execute este script como Administrador!"
        exit 1
    }
}

function Install-Service {
    Require-Admin

    # Verifica se os executáveis estão presentes
    if (-not (Test-Path $ServiceExe)) {
        Write-Error "Arquivo não encontrado: $ServiceExe"
        Write-Host "Compile o projeto e copie CertMonitorService.exe e CertMonitorPopup.exe para: $InstallDir"
        exit 1
    }
    if (-not (Test-Path $PopupExe)) {
        Write-Error "Arquivo não encontrado: $PopupExe"
        exit 1
    }

    # Cria pasta de log
    New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

    # Remove serviço antigo se existir
    $existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if ($existing) {
        Write-Host "Removendo serviço existente..."
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        sc.exe delete $ServiceName | Out-Null
        Start-Sleep -Seconds 2
    }

    # Instala o serviço
    Write-Host "Instalando serviço '$ServiceName'..."
    New-Service `
        -Name $ServiceName `
        -DisplayName $DisplayName `
        -Description $Description `
        -BinaryPathName "`"$ServiceExe`"" `
        -StartupType Automatic `
        | Out-Null

    # Configura recuperação automática em falhas
    sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

    # Inicia o serviço
    Start-Service -Name $ServiceName
    $svc = Get-Service -Name $ServiceName
    Write-Host ""
    Write-Host "✅ Serviço instalado com sucesso!" -ForegroundColor Green
    Write-Host "   Status : $($svc.Status)" -ForegroundColor Cyan
    Write-Host "   Log    : $LogDir\certmonitor.log"
    Write-Host ""
    Write-Host "O serviço agora monitorará certificados e alertará cada usuário ao fazer RDP."
}

function Uninstall-Service {
    Require-Admin

    $existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $existing) {
        Write-Host "Serviço '$ServiceName' não está instalado."
        return
    }

    Write-Host "Parando e removendo serviço..."
    Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1

    Write-Host "✅ Serviço removido com sucesso!" -ForegroundColor Green
}

function Show-Status {
    $svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    if (-not $svc) {
        Write-Host "❌ Serviço '$ServiceName' NÃO está instalado." -ForegroundColor Red
        return
    }

    Write-Host ""
    Write-Host "📋 Status do CertMonitor" -ForegroundColor Cyan
    Write-Host "   Nome    : $($svc.DisplayName)"
    Write-Host "   Status  : $($svc.Status)"
    Write-Host "   Startup : $($svc.StartType)"

    $logFile = "$LogDir\certmonitor.log"
    if (Test-Path $logFile) {
        Write-Host ""
        Write-Host "📄 Últimas entradas do log:" -ForegroundColor Yellow
        Get-Content $logFile -Tail 15
    }
}

function Test-Popup {
    Write-Host "Testando popup na sessão atual..."
    if (Test-Path $PopupExe) {
        # Busca certificados e simula o popup
        & $ServiceExe --console
    } else {
        Write-Error "CertMonitorPopup.exe não encontrado em $InstallDir"
    }
}

# ── Execução ──────────────────────────────────────────────────────────────────

Write-Host ""
Write-Host "=====================================" -ForegroundColor DarkCyan
Write-Host "  CertMonitor — Gerenciador de Serviço" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor DarkCyan
Write-Host ""

switch ($Action) {
    "install"   { Install-Service }
    "uninstall" { Uninstall-Service }
    "status"    { Show-Status }
    "test"      { Test-Popup }
}
