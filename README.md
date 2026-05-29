# Evidenciador

Aplicacao .NET 8 para gerar documentos de evidencia em `.docx` a partir de Pull Requests e issues.

O projeto combina:

- `Evidenciador.UI`: interface WinForms, indicada como a forma principal de uso para iniciantes.
- `Evidenciador.Cli`: modo de linha de comando, usado pela UI e util para automacao e diagnostico.
- automacao web com Playwright/Chromium para login, navegacao e coleta de dados.
- geracao de documentos Word com `DocumentFormat.OpenXml`.

Hoje o fluxo operacional validado no codigo cobre principalmente:

- PR unico no GitHub.
- issues do Redmine com PRs associados no GitHub.
- suporte existente a Gogs no fluxo de PRs encontrados via Redmine, com ressalvas de validacao operacional adicional.

Suporte a Azure DevOps esta pendente.

## Visao Geral

O Evidenciador coleta diffs de PRs, organiza essas evidencias e gera arquivos Word.

Existem 3 modos principais no codigo:

- `SinglePr`: gera 1 `.docx` a partir de 1 PR.
- `RedmineQuery`: busca varias issues no Redmine por query, encontra PRs relacionados e gera 1 `.docx` por issue.
- `RedmineIssue`: processa 1 issue especifica do Redmine e gera 1 `.docx`.

Em termos praticos:

- iniciantes devem começar pela UI WinForms.
- quem precisa automatizar execucoes, depurar argumentos ou integrar com scripts pode usar a CLI diretamente.

## Plataformas

Plataformas hoje mais aderentes ao repositorio:

- Windows: principal alvo atual. A UI e WinForms e o README usa exemplos em PowerShell.

Plataformas possiveis, mas pendentes de validacao operacional completa neste repositorio:

- Linux e macOS: a CLI e .NET 8 + Playwright, mas a UI nao roda fora de Windows e o fluxo completo nao esta documentado nem validado aqui.
- Azure DevOps: pendente. Nao ha implementacao documentada nem fluxo confirmado no codigo atual.

Hospedagens de PR atualmente presentes no codigo:

- GitHub: suporte principal.
- Gogs: suporte existente no coletor Playwright e na selecao de coletor do fluxo Redmine, mas ainda merece validacao operacional mais ampla e documentacao mais detalhada.

## Estrutura Do Repositorio

| Caminho | Papel |
|---|---|
| `Evidenciador.Core` | modelos, contratos e utilitarios de dominio |
| `Evidenciador.Infra.Playwright` | automacao Playwright para GitHub, Gogs e Redmine |
| `Evidenciador.Infra.Docx` | geracao de `.docx` com OpenXML |
| `Evidenciador.Cli` | entrypoint CLI, configuracao e orquestracao |
| `Evidenciador.UI` | interface WinForms para configuracao e execucao |
| `AGENTS.md` | resumo tecnico do projeto para agentes e manutencao |

## Arquitetura Em Linguagem Simples

O fluxo geral funciona assim:

1. A UI ou a CLI carrega configuracoes.
2. O modo de execucao e definido (`SinglePr`, `RedmineQuery` ou `RedmineIssue`).
3. O Playwright abre Chromium para autenticar e navegar nas paginas necessarias.
4. O sistema coleta o diff do PR ou a lista de issues e PRs relacionados.
5. O dominio transforma o diff em uma estrutura padronizada de arquivos e linhas alteradas.
6. A camada de DOCX gera o documento final.

Separacao principal:

- `Core` nao conhece Playwright nem OpenXML.
- `Infra.Playwright` coleta dados.
- `Infra.Docx` renderiza os documentos.
- `Cli` organiza o fluxo.
- `UI` edita configuracoes e dispara a CLI.

## Pre-Requisitos

- Windows 10 ou 11 para usar a UI.
- .NET 8 SDK.
- PowerShell para comandos de setup.
- Chromium do Playwright instalado.
- acesso de rede aos sistemas que voce vai usar: GitHub, Redmine e, se aplicavel, Gogs.

## Setup Inicial

### 1. Restaurar e compilar

```powershell
dotnet build
```

### 2. Instalar o Chromium do Playwright

Depois do build, instale o navegador usado pela automacao:

```powershell
pwsh .\Evidenciador.Cli\bin\Debug\net8.0\playwright.ps1 install chromium
```

Se voce recompilar em outra configuracao ou limpar `bin`, confirme novamente se o script continua presente.

### 3. Preparar configuracao de desenvolvimento com seguranca

Recomendacao para desenvolvimento:

- use `appsettings.Development.json` para defaults nao sensiveis.
- use `dotnet user-secrets` para credenciais locais.
- evite editar `Evidenciador.Cli/appsettings.json`.

Motivo:

- `Evidenciador.Cli/Program.cs` carrega `appsettings.json`, depois `appsettings.{Environment}.json`, depois user-secrets em `Development`, e por fim variaveis de ambiente.
- `AGENTS.md` tambem deixa explicito que `appsettings.json` nao deve ser alterado para guardar segredos.

Ative o ambiente de desenvolvimento:

```powershell
$env:DOTNET_ENVIRONMENT = "Development"
```

Exemplo generico de `Evidenciador.Cli/appsettings.Development.json`:

```json
{
  "Redmine": {
    "BaseUrl": "https://redmine.exemplo.local/",
    "LoginUrl": "/login",
    "IssuesUrl": "/issues"
  },
  "Gogs": {
    "BaseUrl": "https://gogs.exemplo.local/",
    "Username": ""
  },
  "Evidenciador": {
    "Mode": "SinglePr",
    "PrUrl": "",
    "Out": "",
    "OutDir": ".\\out",
    "Headless": true,
    "DiagnosticsDir": ".\\diag",
    "TemplatePath": "TEMPLATE_EVIDENCIA.docx",
    "RedmineQuery": "assigned_to_id=me"
  }
}
```

Defina segredos locais com `user-secrets`:

```powershell
dotnet user-secrets set "GITHUB_USERNAME" "seu-usuario" --project .\Evidenciador.Cli\Evidenciador.Cli.csproj
dotnet user-secrets set "GITHUB_PASSWORD" "sua-senha-ou-token" --project .\Evidenciador.Cli\Evidenciador.Cli.csproj
dotnet user-secrets set "REDMINE_USERNAME" "seu-usuario" --project .\Evidenciador.Cli\Evidenciador.Cli.csproj
dotnet user-secrets set "REDMINE_PASSWORD" "sua-senha" --project .\Evidenciador.Cli\Evidenciador.Cli.csproj
```

Alternativamente, use variaveis de ambiente:

```powershell
$env:GITHUB_USERNAME = "seu-usuario"
$env:GITHUB_PASSWORD = "sua-senha-ou-token"
$env:REDMINE_USERNAME = "seu-usuario"
$env:REDMINE_PASSWORD = "sua-senha"
```

## Boas Praticas De Credenciais

- Nunca commit credenciais reais.
- Nao use `Evidenciador.Cli/appsettings.json` como armazenamento pessoal de segredos.
- Prefira `user-secrets` para desenvolvimento local.
- Use variaveis de ambiente quando a execucao vier de script, CI interna ou agendador.

Ressalva importante sobre a UI atual:

- a implementacao atual da UI le e salva diretamente `Evidenciador.Cli/appsettings.json`.
- isso significa que usar a UI para preencher logins e senhas pode gravar segredos nesse arquivo.
- por isso, para desenvolvimento seguro, prefira deixar credenciais fora da UI quando possivel ou revise cuidadosamente o arquivo antes de qualquer commit.

## Uso Principal Pela UI

A UI WinForms e o melhor ponto de entrada para quem esta usando o projeto pela primeira vez.

Ela permite:

- editar configuracoes gerais, conexoes e campos avancados.
- salvar configuracoes.
- executar a CLI sem montar argumentos manualmente.
- visualizar o log de execucao em uma janela de console.
- interromper a execucao em andamento.
- abrir o diretorio de saida automaticamente ao final quando a execucao termina com sucesso.

## Como abrir a UI

No Visual Studio:

- abra a solucao `Evidenciador.slnx`.
- defina `Evidenciador.UI` como projeto de inicializacao.
- execute.

Pela CLI do .NET:

```powershell
dotnet run --project .\Evidenciador.UI
```

## Como usar a UI

Fluxo sugerido para iniciantes:

1. Compile o projeto e instale o Chromium do Playwright.
2. Abra a UI.
3. Preencha primeiro os campos de saida e, se for usar Redmine, o template `.docx`.
4. Configure as conexoes necessarias para o seu fluxo.
5. Escolha o modo em `Geral`.
6. Clique em `Salvar`.
7. Clique em `Executar`.
8. Acompanhe o log na janela de console.

Campos relevantes visiveis na UI hoje:

- `Modo`: `Single PR`, `Redmine Query` ou `Redmine Issue`.
- `OutDir`: diretorio de saida.
- `TemplatePath`: template Word usado nos fluxos Redmine.
- `Headless`: executa o navegador invisivel quando marcado.
- `DiagnosticsDir`: pasta opcional para artefatos de diagnostico.
- conexoes para Redmine, GitHub e Gogs.
- `PrUrl` e `IssuesUrl` na aba avancada.

Observacao pratica:

- a UI executa `dotnet run --project "Evidenciador.Cli.csproj"` sem passar argumentos extras; portanto ela depende do que estiver salvo na configuracao.

## Uso Alternativo Pela CLI

A CLI e util quando voce quer:

- automatizar execucoes.
- testar rapidamente um PR unico.
- reproduzir erros da UI de forma mais direta.
- controlar argumentos sem depender da tela.

## Ajuda

```powershell
dotnet run --project .\Evidenciador.Cli -- --help
```

## Modo `SinglePr`

Exemplo generico:

```powershell
dotnet run --project .\Evidenciador.Cli -- `
  --pr-url "https://github.com/ORGANIZACAO/REPOSITORIO/pull/123/files?w=1" `
  --out-dir ".\out" `
  --headless true `
  --diagnostics-dir ".\diag"
```

No PowerShell voce tambem pode escrever tudo em uma linha:

```powershell
dotnet run --project .\Evidenciador.Cli -- --pr-url "https://github.com/ORGANIZACAO/REPOSITORIO/pull/123/files?w=1" --out-dir ".\out" --headless true --diagnostics-dir ".\diag"
```

Comportamento real relevante:

- exige `GITHUB_USERNAME` e `GITHUB_PASSWORD`.
- usa `--pr-url` ou `Evidenciador:PrUrl`.
- usa `--out-dir` ou `--out` ou os equivalentes em configuracao.
- se `--out-dir` e `--out` forem informados ao mesmo tempo, `--out-dir` vence.
- `Headless` padrao e `true`.
- `DiagnosticsDir` e opcional.

## Modo `RedmineQuery`

Exemplo generico:

```powershell
dotnet run --project .\Evidenciador.Cli -- --redmine-url "https://redmine.exemplo.local/" --redmine-query "assigned_to_id=me" --out-dir ".\out" --template-path ".\Evidenciador.Cli\TEMPLATE_EVIDENCIA.docx"
```

Comportamento real relevante:

- exige credenciais do Redmine.
- usa `Redmine:BaseUrl` ou `--redmine-url`.
- usa `Evidenciador:RedmineQuery` ou `--redmine-query`.
- exige `TemplatePath` existente.
- gera um documento por issue encontrada.

## Modo `RedmineIssue`

Exemplo generico:

```powershell
dotnet run --project .\Evidenciador.Cli -- --redmine-url "https://redmine.exemplo.local/" --issue-id "12345" --out-dir ".\out" --template-path ".\Evidenciador.Cli\TEMPLATE_EVIDENCIA.docx"
```

## Argumentos Principais Da CLI

Argumentos confirmados em `CliArgumentParser.cs`:

- `--pr-url <url>`
- `--out <arquivo-ou-diretorio>`
- `--out-dir <diretorio>`
- `--headless [true|false]`
- `--diagnostics-dir <diretorio>`
- `--redmine-url <url>`
- `--redmine-login <usuario>`
- `--redmine-password <senha>`
- `--redmine-query <query>`
- `--issue-id <id>`
- `--template-path <arquivo.docx>`

## Fluxo Fim A Fim

### `SinglePr`

1. A aplicacao resolve credenciais e configuracao.
2. O Playwright abre o PR no GitHub.
3. Se necessario, faz login interativo.
4. O sistema tenta baixar o `.diff` do PR.
5. O diff e parseado em arquivos e linhas alteradas.
6. Se o parse do `.diff` nao render nada, existe fallback para scraping da aba de arquivos do PR.
7. Um `.docx` simples e gerado com os arquivos e diff.

### `RedmineQuery` e `RedmineIssue`

1. A aplicacao autentica no Redmine.
2. Coleta uma issue especifica ou uma lista de issues.
3. Tira screenshot da issue.
4. Extrai PRs da pagina e da descricao da issue.
5. Tenta autenticar no GitHub.
6. Para cada PR encontrado, escolhe o coletor adequado.
7. Coleta as evidencias dos PRs.
8. Preenche o template Word e gera 1 documento por issue.

Sobre Gogs nesse fluxo:

- existe `GogsPlaywrightDiffCollector` e uma fabrica que seleciona Gogs quando a URL do PR corresponde a Gogs.
- isso indica suporte implementado no fluxo orquestrado por Redmine.
- por outro lado, o modo `SinglePr` hoje injeta diretamente o coletor GitHub em `EvidenceApp`, entao o uso de Gogs nesse modo nao esta confirmado como fluxo operacional pronto.

## Saida Gerada

### `SinglePr`

- gera 1 arquivo `.docx`.
- quando o destino e um diretorio, o nome e gerado automaticamente.
- formato observado no codigo: `{owner}_{repo}_pr{number}_{timestamp}_{slug}.docx`.
- em caso de colisao, o sistema cria sufixos como `-2`, `-3`.

### `RedmineQuery` e `RedmineIssue`

- gera 1 pasta por issue em `OutDir`.
- dentro dela, gera `issue_{id}.docx`.
- o documento pode incluir placeholders preenchidos, screenshot da issue e tabelas de diff para os PRs coletados.

### Diagnosticos

Se `DiagnosticsDir` estiver configurado, o projeto pode gravar artefatos uteis para investigacao, como:

- screenshots de login e navegacao.
- HTML ou `.diff` capturado em pontos chave do fluxo GitHub.

## Template Word

Nos modos Redmine, o documento final depende de um template `.docx`.

Arquivo padrao no repositorio:

- `Evidenciador.Cli/TEMPLATE_EVIDENCIA.docx`

O codigo espera placeholders especificos e falha se o placeholder principal de diff nao estiver presente exatamente uma vez. Em outras palavras:

- nao altere o template sem validar o fluxo completo.
- se for customizar, teste pelo menos uma execucao real de `RedmineIssue`.

## Limitacoes E Observacoes

- A UI atual salva configuracoes em `Evidenciador.Cli/appsettings.json`, inclusive campos sensiveis se o usuario os preencher.
- O modo `SinglePr` hoje esta alinhado ao coletor GitHub; suporte operacional equivalente para Gogs nesse modo nao esta confirmado.
- O suporte a Gogs existe no codigo, principalmente no fluxo Redmine, mas ainda deve ser tratado como implementado com ressalvas de validacao operacional.
- Azure DevOps esta pendente.
- O projeto nao possui suite de testes automatizados, linter ou CI configurados no repositorio neste momento.
- Como a automacao depende de UI web de terceiros, mudancas de layout em GitHub, Redmine ou Gogs podem quebrar seletor e navegacao.

## Contribuicao

Para contribuir de forma pragmatica:

1. Entenda primeiro qual modo voce esta mexendo: `SinglePr`, `RedmineQuery` ou `RedmineIssue`.
2. Preserve a separacao de responsabilidades entre `Core`, `Infra.*`, `Cli` e `UI`.
3. Evite acoplar regras de negocio diretamente em classes de Playwright ou em formularios WinForms.
4. Se alterar seletores ou fluxo de login, valide com uma execucao real.
5. Se alterar geracao de DOCX, valide abrindo o arquivo final no Word.
6. Nao commite credenciais, URLs internas sensiveis ou diagnosticos com dados privados.
7. Se mudar configuracao padrao, prefira exemplos genericos e defaults seguros.

Checklist util antes de abrir mudancas:

- `dotnet build`
- instalar ou confirmar Playwright Chromium
- testar o modo impactado
- revisar se nenhum segredo foi parar em `appsettings.json`
- revisar se a documentacao continua coerente com o comportamento real

## Comandos Rapidos

Build:

```powershell
dotnet build
```

Executar UI:

```powershell
dotnet run --project .\Evidenciador.UI
```

Executar CLI com defaults:

```powershell
dotnet run --project .\Evidenciador.Cli
```

Instalar Chromium do Playwright:

```powershell
pwsh .\Evidenciador.Cli\bin\Debug\net8.0\playwright.ps1 install chromium
```
