# Evidenciador

CLI para coletar diffs de um Pull Request no GitHub (via Playwright/Chromium) e gerar um DOCX com um layout simples no estilo GitHub.

## Requisitos

- .NET 8 SDK
- Playwright browsers (Chromium)

## Credenciais (nao commitar)

O app le `GITHUB_USERNAME` e `GITHUB_PASSWORD` via configuracao.

Opcao 1: variaveis de ambiente (PowerShell)

```powershell
$env:GITHUB_USERNAME = "seu-usuario"
$env:GITHUB_PASSWORD = "sua-senha"
```

Opcao 2: user-secrets (apenas dev)

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
dotnet user-secrets set "GITHUB_USERNAME" "seu-usuario" --project .\Evidenciador.Cli\Evidenciador.Cli.csproj
dotnet user-secrets set "GITHUB_PASSWORD" "sua-senha" --project .\Evidenciador.Cli\Evidenciador.Cli.csproj
```

## Instalar Playwright (Chromium)

Depois de buildar, instale o Chromium:

```powershell
dotnet build
pwsh .\Evidenciador.Cli\bin\Debug\net8.0\playwright.ps1 install chromium
```

## Executar

```powershell
dotnet run --project .\Evidenciador.Cli -- \
  --pr-url "https://github.com/OWNER/REPO/pull/123/files?w=1" \
  --out-dir ".\out" \
  --headless true \
  --diagnostics-dir ".\diag"
```

- `--headless` default: `true`
- `--diagnostics-dir` (opcional): grava screenshot e html em pontos-chave (login/logged-in/pr)

Quando voce usa `--out-dir`, o app cria um `.docx` dentro do diretorio usando um nome gerado automaticamente que inclui o titulo do PR (quando disponivel).

## Opcao B (defaults via appsettings)

Voce pode definir defaults nao sensiveis em `Evidenciador.Cli\appsettings.json` (ou `appsettings.Development.json`):

```json
{
  "Evidenciador": {
    "PrUrl": "https://github.com/OWNER/REPO/pull/123",
    "OutDir": ".\\out",
    "Headless": false,
    "DiagnosticsDir": ".\\diag"
  }
}
```

As credenciais continuam vindo de `GITHUB_USERNAME` / `GITHUB_PASSWORD` (env vars ou user-secrets).

Observacao (JSON no Windows): para caminhos com barra invertida, use `\\` (ex.: `.\\out`). Alternativamente, use `.` para o diretorio atual.
