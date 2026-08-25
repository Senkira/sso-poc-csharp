@echo off
setlocal EnableExtensions

set "POC_ROOT=%~dp0"
set "RUNTIME_DIR=%POC_ROOT%.runtime"
set "LOG_DIR=%RUNTIME_DIR%\logs"
set "PROFILE_DIR=%RUNTIME_DIR%\profiles"
set "KEYS_DIR=%RUNTIME_DIR%\keys"
set "PORTABLE_DIR=%POC_ROOT%portable"
set "API_URL=http://127.0.0.1:4174"
set "MVC_URL=http://127.0.0.1:4173"
set "GATEWAY_KEY=local-poc-gateway-key-2026-000000000001"

if exist "%PORTABLE_DIR%\api\SsoGeminiLogin.Api.exe" if exist "%PORTABLE_DIR%\mvc\SsoGeminiLogin.Mvc.exe" if exist "%PORTABLE_DIR%\agent\SsoGeminiLogin.Agent.exe" goto :portable_ready

where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] Portable runtime is not present and .NET 10 SDK was not found.
  echo Use the final portable ZIP or install .NET SDK 10.0.400 for source mode.
  exit /b 1
)

dotnet --list-sdks | findstr /b /c:"10.0.400 " >nul
if errorlevel 1 (
  echo [ERROR] Source mode requires .NET SDK 10.0.400 as pinned by global.json.
  exit /b 1
)

echo [BUILD 1/2] Building .NET 10 Agent and API...
dotnet build "%POC_ROOT%SsoGeminiLogin.Api.sln" -c Release --nologo
if errorlevel 1 exit /b 1

echo [BUILD 2/2] Building .NET 10 MVC...
dotnet build "%POC_ROOT%SsoGeminiLogin.Mvc.sln" -c Release --nologo
if errorlevel 1 exit /b 1

set "RUN_MODE=source"
set "AGENT_EXE=%POC_ROOT%src\SsoGeminiLogin.Agent\bin\Release\net10.0\SsoGeminiLogin.Agent.exe"
set "API_EXE=%POC_ROOT%src\SsoGeminiLogin.Api\bin\Release\net10.0\SsoGeminiLogin.Api.exe"
set "API_ROOT=%POC_ROOT%src\SsoGeminiLogin.Api"
set "MVC_EXE=%POC_ROOT%src\SsoGeminiLogin.Mvc\bin\Release\net10.0\SsoGeminiLogin.Mvc.exe"
set "MVC_ROOT=%POC_ROOT%src\SsoGeminiLogin.Mvc"
goto :start_poc

:portable_ready
set "RUN_MODE=portable zero-install"
set "AGENT_EXE=%PORTABLE_DIR%\agent\SsoGeminiLogin.Agent.exe"
set "API_EXE=%PORTABLE_DIR%\api\SsoGeminiLogin.Api.exe"
set "API_ROOT=%PORTABLE_DIR%\api"
set "MVC_EXE=%PORTABLE_DIR%\mvc\SsoGeminiLogin.Mvc.exe"
set "MVC_ROOT=%PORTABLE_DIR%\mvc"

:start_poc
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"
if not exist "%PROFILE_DIR%" mkdir "%PROFILE_DIR%"
if not exist "%KEYS_DIR%" mkdir "%KEYS_DIR%"

powershell -NoProfile -Command "$root = [IO.Path]::GetFullPath('%POC_ROOT%'); $profileRoot = [IO.Path]::GetFullPath('%PROFILE_DIR%'); $listeners = Get-NetTCPConnection -State Listen -LocalPort 4173,4174 -ErrorAction SilentlyContinue; foreach ($listener in $listeners) { $process = Get-CimInstance Win32_Process -Filter ('ProcessId = ' + $listener.OwningProcess); if ($process.CommandLine -like ('*' + $root + '*')) { Stop-Process -Id $listener.OwningProcess -Force } else { Write-Error ('Required port belongs to another program: ' + $listener.LocalPort); exit 1 } }; Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'msedge.exe' -and $_.CommandLine -like ('*' + $profileRoot + '*') } | ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }; if ($listeners) { Start-Sleep -Milliseconds 700 }; exit 0"
if errorlevel 1 exit /b 1

set "ASPNETCORE_ENVIRONMENT=LocalPoc"
set "DataProtection__KeysPath=%KEYS_DIR%"
set "Gateway__SharedKey=%GATEWAY_KEY%"
set "Agent__Mode=Local"
set "Agent__ExecutablePath=%AGENT_EXE%"
set "Agent__CredentialTarget=ESB.GeminiBroker.CodeAssist04"
set "Agent__ProfileRoot=%PROFILE_DIR%"
set "Agent__ExpectedAccountId=7c79d5435d249ca9"
set "Agent__ExpectedAccountEmail=codeassist.04@easybuy.co.th"
set "Agent__MaxWorkers=3"
set "Agent__Headless=true"
set "BrokerApi__GatewayKey=%GATEWAY_KEY%"
set "BrokerApi__BaseUrl=%API_URL%"
set "DevSso__Enabled=true"
set "DevSso__Username=ssotest01"
set "DevSso__Password=123456"
set "DevSso__MappedUser=codeassist.04@easybuy.co.th"

echo [1/5] Starting API in %RUN_MODE% mode...
start "SSO Gemini API" /min cmd /d /s /c ""%API_EXE%" --contentRoot "%API_ROOT%" --urls "%API_URL%" > "%LOG_DIR%\api.log" 2>&1"

echo [2/5] Waiting for API readiness...
powershell -NoProfile -Command "$deadline = (Get-Date).AddMinutes(2); do { try { $r = Invoke-WebRequest -UseBasicParsing -Uri '%API_URL%/health/ready' -TimeoutSec 2; if ($r.StatusCode -eq 200) { exit 0 } } catch {}; Start-Sleep -Milliseconds 500 } while ((Get-Date) -lt $deadline); exit 1"
if errorlevel 1 goto :startup_failed

echo [3/5] Verifying Local Browser Agent and protected Gemini credential...
powershell -NoProfile -Command "$deadline = (Get-Date).AddMinutes(2); do { try { $r = Invoke-WebRequest -UseBasicParsing -Uri '%API_URL%/health/agent' -TimeoutSec 4; if ($r.StatusCode -eq 200) { exit 0 } } catch {}; Start-Sleep -Milliseconds 500 } while ((Get-Date) -lt $deadline); exit 1"
if errorlevel 1 goto :agent_failed

echo [4/5] Starting MVC in %RUN_MODE% mode...
start "SSO Gemini MVC" /min cmd /d /s /c ""%MVC_EXE%" --contentRoot "%MVC_ROOT%" --urls "%MVC_URL%" > "%LOG_DIR%\mvc.log" 2>&1"

echo [5/5] Waiting for MVC readiness...
powershell -NoProfile -Command "$deadline = (Get-Date).AddMinutes(2); do { try { $r = Invoke-WebRequest -UseBasicParsing -Uri '%MVC_URL%/health/ready' -TimeoutSec 2; if ($r.StatusCode -eq 200) { exit 0 } } catch {}; Start-Sleep -Milliseconds 500 } while ((Get-Date) -lt $deadline); exit 1"
if errorlevel 1 goto :startup_failed

echo.
echo POC is ready: %MVC_URL%/
echo Demo login: ssotest01 / 123456
echo Flow waits at Web SSO and then for the Open Gemini Chat button.
start "" "%MVC_URL%/"
exit /b 0

:agent_failed
echo [ERROR] Local Browser Agent or Windows credential is unavailable.
echo Required Credential Manager target: ESB.GeminiBroker.CodeAssist04
echo See: %LOG_DIR%\api.log
exit /b 1

:startup_failed
echo [ERROR] POC startup failed. See logs:
echo   %LOG_DIR%\api.log
echo   %LOG_DIR%\mvc.log
exit /b 1
