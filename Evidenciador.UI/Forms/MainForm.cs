using System.Diagnostics;
using Evidenciador.UI.Services;
using Evidenciador.UI.Forms;

namespace Evidenciador.UI;

public class MainForm : Form
{
    private readonly ConfigurationService _configService;
    private AppSettings _settings;
    private readonly TabGeralControl tabGeral = new();
    private readonly TabConexoesControl tabConexoes = new();
    private readonly TabAvancadoControl tabAvancado = new();
    private readonly TabControl tabControl = new();
    private readonly Button btnSalvar = new();
    private readonly Button btnExecutar = new();
    private readonly Button btnParar = new();
    private TabPage tpGeral = null!;
    private TabPage tpConexoes = null!;
    private TabPage tpAvancado = null!;
    private readonly Button btnSair = new();
    private Process? _runningProcess;
    private ConsoleForm? _consoleForm;
    private bool _stopRequested;
    private bool _stopInProgress;
    private bool _allowMainFormClose;
    private bool _allowConsoleFormClose;

    public MainForm()
    {
        InitializeComponent();
        _configService = new ConfigurationService();
        _settings = _configService.Load();
        LoadSettingsToUI();
    }

    private void InitializeComponent()
    {
        tpGeral = new TabPage();
        tpConexoes = new TabPage();
        tpAvancado = new TabPage();
        SuspendLayout();
        // 
        // tpGeral
        // 
        tpGeral.Location = new Point(0, 0);
        tpGeral.Name = "tpGeral";
        tpGeral.Size = new Size(200, 100);
        tpGeral.TabIndex = 0;
        tpGeral.Text = "Geral";
        // 
        // tpConexoes
        // 
        tpConexoes.Location = new Point(0, 0);
        tpConexoes.Name = "tpConexoes";
        tpConexoes.Size = new Size(200, 100);
        tpConexoes.TabIndex = 0;
        tpConexoes.Text = "Conexões";
        // 
        // tpAvancado
        // 
        tpAvancado.Location = new Point(0, 0);
        tpAvancado.Name = "tpAvancado";
        tpAvancado.Size = new Size(200, 100);
        tpAvancado.TabIndex = 0;
        tpAvancado.Text = "Avançado";
        // 
        // tabControl
        // 
        tabControl.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        tabControl.Location = new Point(12, 12);
        tabControl.Name = "tabControl";
        tabControl.Size = new Size(560, 297);
        tabControl.TabIndex = 0;
        // 
        // btnSalvar
        // 
        btnSalvar.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnSalvar.Location = new Point(235, 321);
        btnSalvar.Name = "btnSalvar";
        btnSalvar.Size = new Size(75, 28);
        btnSalvar.TabIndex = 1;
        btnSalvar.Text = "Salvar";
        btnSalvar.UseVisualStyleBackColor = true;
        btnSalvar.Click += btnSalvar_Click;
        // 
        // btnExecutar
        // 
        btnExecutar.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnExecutar.Location = new Point(478, 321);
        btnExecutar.Name = "btnExecutar";
        btnExecutar.Size = new Size(75, 28);
        btnExecutar.TabIndex = 2;
        btnExecutar.Text = "Executar";
        btnExecutar.UseVisualStyleBackColor = true;
        btnExecutar.Click += btnExecutar_Click;
        // 
        // btnParar
        // 
        btnParar.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnParar.Enabled = false;
        btnParar.Location = new Point(397, 321);
        btnParar.Name = "btnParar";
        btnParar.Size = new Size(75, 28);
        btnParar.TabIndex = 3;
        btnParar.Text = "Parar";
        btnParar.UseVisualStyleBackColor = true;
        btnParar.Click += btnParar_Click;
        // 
        // btnSair
        // 
        btnSair.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
        btnSair.Location = new Point(316, 321);
        btnSair.Name = "btnSair";
        btnSair.Size = new Size(75, 28);
        btnSair.TabIndex = 4;
        btnSair.Text = "Sair";
        btnSair.UseVisualStyleBackColor = true;
        btnSair.Click += btnSair_Click;

        tabGeral.Dock = DockStyle.Fill;
        tabConexoes.Dock = DockStyle.Fill;
        tabAvancado.Dock = DockStyle.Fill;

        tpGeral.Controls.Add(tabGeral);
        tpConexoes.Controls.Add(tabConexoes);
        tpAvancado.Controls.Add(tabAvancado);

        tabControl.TabPages.Add(tpGeral);
        tabControl.TabPages.Add(tpConexoes);
        tabControl.TabPages.Add(tpAvancado);
        // 
        // MainForm
        // 
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(584, 411);
        Controls.Add(btnParar);
        Controls.Add(btnSair);
        Controls.Add(btnExecutar);
        Controls.Add(btnSalvar);
        Controls.Add(tabControl);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Evidenciador - Configurações";
        FormClosing += MainForm_FormClosing;
        Load += MainForm_Load;
        ResumeLayout(false);
    }

    private void LoadSettingsToUI()
    {
        tabGeral.txtOutDir.Text = _settings.Evidenciador.OutDir;
        tabGeral.txtTemplate.Text = _settings.Evidenciador.TemplatePath;
        tabGeral.chkHeadless.Checked = _settings.Evidenciador.Headless;
        tabGeral.txtDiagnosticsDir.Text = _settings.Evidenciador.DiagnosticsDir;
        tabGeral.cmbModo.SelectedItem = _settings.Evidenciador.Mode switch
        {
            "RedmineQuery" => "Redmine Query",
            "RedmineIssue" => "Redmine Issue",
            _ => "Single PR"
        };
        tabGeral.txtRedmineQuery.Text = _settings.Evidenciador.RedmineQuery;

        tabConexoes.txtRedmineUrl.Text = _settings.Redmine.BaseUrl;
        tabConexoes.txtRedmineLogin.Text = _settings.Redmine.Username;
        tabConexoes.txtRedminePassword.Text = _settings.Redmine.Password;
        tabConexoes.txtGitHubLogin.Text = _settings.GitHubUsername;
        tabConexoes.txtGitHubPassword.Text = _settings.GitHubPassword;
        tabConexoes.txtGogsUrl.Text = _settings.Gogs.BaseUrl;
        tabConexoes.txtGogsLogin.Text = _settings.Gogs.Username;
        tabConexoes.txtGogsPassword.Text = _settings.Gogs.Password;

        tabAvancado.txtPrUrl.Text = _settings.Evidenciador.PrUrl;
        tabAvancado.txtIssuesUrl.Text = _settings.Redmine.IssuesUrl;
    }

    private void LoadSettingsFromUI()
    {
        _settings.Evidenciador.OutDir = tabGeral.txtOutDir.Text;
        _settings.Evidenciador.TemplatePath = tabGeral.txtTemplate.Text;
        _settings.Evidenciador.Headless = tabGeral.chkHeadless.Checked;
        _settings.Evidenciador.DiagnosticsDir = tabGeral.txtDiagnosticsDir.Text;
        _settings.Evidenciador.Mode = tabGeral.cmbModo.SelectedItem?.ToString() switch
        {
            "Redmine Query" => "RedmineQuery",
            "Redmine Issue" => "RedmineIssue",
            _ => "SinglePr"
        };
        _settings.Evidenciador.RedmineQuery = tabGeral.txtRedmineQuery.Text;

        _settings.Redmine.BaseUrl = tabConexoes.txtRedmineUrl.Text;
        _settings.Redmine.Username = tabConexoes.txtRedmineLogin.Text;
        _settings.Redmine.Password = tabConexoes.txtRedminePassword.Text;
        _settings.GitHubUsername = tabConexoes.txtGitHubLogin.Text;
        _settings.GitHubPassword = tabConexoes.txtGitHubPassword.Text;
        _settings.Gogs.BaseUrl = tabConexoes.txtGogsUrl.Text;
        _settings.Gogs.Username = tabConexoes.txtGogsLogin.Text;
        _settings.Gogs.Password = tabConexoes.txtGogsPassword.Text;

        _settings.Evidenciador.PrUrl = tabAvancado.txtPrUrl.Text;
        _settings.Redmine.IssuesUrl = tabAvancado.txtIssuesUrl.Text;
    }

    private void btnSalvar_Click(object? sender, EventArgs e)
    {
        LoadSettingsFromUI();
        if (_configService.Save(_settings))
        {
            MessageBox.Show("Configurações salvas com sucesso!", "Sucesso",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private async void btnExecutar_Click(object? sender, EventArgs e)
    {
        if (IsExecutionRunning())
        {
            return;
        }

        LoadSettingsFromUI();

        if (!_configService.Save(_settings))
        {
            return;
        }

        _stopRequested = false;
        _stopInProgress = false;
        _allowConsoleFormClose = false;
        _allowMainFormClose = false;
        UpdateExecutionButtons(isRunning: true);

        _consoleForm = new ConsoleForm();
        _consoleForm.FormClosing += ConsoleForm_FormClosing;
        _consoleForm.Show(this);

        try
        {
            var cliPath = _configService.GetCliProjectPath();
            AppendConsoleOutput("CLI Path: " + cliPath + Environment.NewLine);

            if (!Directory.Exists(cliPath))
            {
                AppendConsoleOutput("[ERRO] Diretório do CLI não encontrado: " + cliPath + Environment.NewLine);
                return;
            }

            var outDir = _settings.Evidenciador.OutDir;
            if (!Path.IsPathRooted(outDir))
            {
                outDir = Path.GetFullPath(Path.Combine(cliPath, "..", outDir));
            }
            AppendConsoleOutput("Output Dir: " + outDir + Environment.NewLine);

            var cliProjectPath = Path.Combine(cliPath, "Evidenciador.Cli.csproj");
            AppendConsoleOutput("CLI Project: " + cliProjectPath + Environment.NewLine);

            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{cliProjectPath}\"",
                WorkingDirectory = cliPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = processInfo };
            _runningProcess = process;
            process.OutputDataReceived += (s, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    AppendConsoleOutput(args.Data + Environment.NewLine);
                }
            };
            process.ErrorDataReceived += (s, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    AppendConsoleOutput("[ERRO] " + args.Data + Environment.NewLine);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (_stopRequested)
            {
                AppendConsoleOutput(Environment.NewLine + "=== Execução interrompida pelo usuário ===" + Environment.NewLine);
                return;
            }

            AppendConsoleOutput(Environment.NewLine + "=== Execução Finalizada ===" + Environment.NewLine);

            if (process.ExitCode == 0)
            {
                AppendConsoleOutput("Sucesso! Abrindo diretório de saída..." + Environment.NewLine);

                if (Directory.Exists(outDir))
                {
                    Process.Start("explorer.exe", outDir);
                }
                else
                {
                    MessageBox.Show("Diretório de saída não existe: " + outDir, "Aviso",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else
            {
                AppendConsoleOutput($"Erro na execução. Código de saída: {process.ExitCode}" + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            if (!_stopRequested)
            {
                AppendConsoleOutput("[ERRO] " + ex.Message + Environment.NewLine);
            }
        }
        finally
        {
            _runningProcess?.Dispose();
            _runningProcess = null;
            _stopInProgress = false;
            UpdateExecutionButtons(isRunning: false);

            if (_consoleForm is { IsDisposed: false })
            {
                _consoleForm.FormClosing -= ConsoleForm_FormClosing;
            }

            _consoleForm = null;
        }
    }

    private void btnParar_Click(object? sender, EventArgs e)
    {
        StopExecution();
    }

    private void btnSair_Click(object? sender, EventArgs e)
    {
        Close();
    }

    private void MainForm_Load(object? sender, EventArgs e)
    {

    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowMainFormClose || !IsExecutionRunning())
        {
            return;
        }

        var confirmed = ConfirmStopExecution();
        if (!confirmed)
        {
            e.Cancel = true;
            return;
        }

        _allowMainFormClose = true;
        _allowConsoleFormClose = true;
        StopExecution();

        if (_consoleForm is { IsDisposed: false })
        {
            _consoleForm.Close();
        }
    }

    private void ConsoleForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowConsoleFormClose || !IsExecutionRunning())
        {
            return;
        }

        var confirmed = ConfirmStopExecution();
        if (!confirmed)
        {
            e.Cancel = true;
            return;
        }

        _allowConsoleFormClose = true;
        StopExecution();
    }

    private bool ConfirmStopExecution()
    {
        return MessageBox.Show(
            "Há uma execução em andamento. Deseja interromper e fechar?",
            "Confirmar fechamento",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;
    }

    private void StopExecution()
    {
        if (!IsExecutionRunning() || _stopInProgress)
        {
            return;
        }

        _stopRequested = true;
        _stopInProgress = true;
        UpdateExecutionButtons(isRunning: true);
        AppendConsoleOutput(Environment.NewLine + "=== Interrompendo execução... ===" + Environment.NewLine);

        try
        {
            _runningProcess?.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (Exception ex)
        {
            AppendConsoleOutput("[ERRO] Falha ao interromper execução: " + ex.Message + Environment.NewLine);
        }
    }

    private bool IsExecutionRunning()
    {
        if (_runningProcess is null)
        {
            return false;
        }

        try
        {
            return !_runningProcess.HasExited;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private void UpdateExecutionButtons(bool isRunning)
    {
        if (IsDisposed)
        {
            return;
        }

        btnExecutar.Enabled = !isRunning;
        btnExecutar.Text = isRunning ? "Executando..." : "Executar";
        btnParar.Enabled = isRunning && !_stopInProgress;
    }

    private void AppendConsoleOutput(string text)
    {
        if (_consoleForm is { IsDisposed: false })
        {
            _consoleForm.AppendOutput(text);
        }
    }
}
