using System.Windows.Forms;

namespace Evidenciador.UI.Forms;

public class TabGeralControl : UserControl
{
    public TextBox txtRedmineQuery = null!;
    public TextBox txtOutDir = null!;
    public TextBox txtTemplate = null!;
    public CheckBox chkHeadless = null!;
    public TextBox txtDiagnosticsDir = null!;
    public ComboBox cmbModo = null!;

    public TabGeralControl()
    {
        var lblModo = new Label { Text = "Modo de Operação:", Location = new Point(20, 22), Width = 110 };
        cmbModo = new ComboBox { Location = new Point(140, 20), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        cmbModo.Items.AddRange(new[] { "Single PR", "Redmine Query", "Redmine Issue" });
        cmbModo.SelectedIndex = 0;

        var lblQuery = new Label { Text = "Query Redmine:", Location = new Point(20, 62), Width = 110 };
        txtRedmineQuery = new TextBox { Location = new Point(140, 60), Width = 300, Text = "assigned_to_id=me" };

        var lblOutDir = new Label { Text = "Diretório Output:", Location = new Point(20, 102), Width = 110 };
        txtOutDir = new TextBox { Location = new Point(140, 100), Width = 300 };

        var lblTemplate = new Label { Text = "Template:", Location = new Point(20, 142), Width = 110 };
        txtTemplate = new TextBox { Location = new Point(140, 140), Width = 300 };

        var lblHeadless = new Label { Text = "Headless:", Location = new Point(20, 182), Width = 110 };
        chkHeadless = new CheckBox { Location = new Point(140, 180), Width = 100 };

        var lblDiagnostics = new Label { Text = "Diagnostics:", Location = new Point(20, 222), Width = 110 };
        txtDiagnosticsDir = new TextBox { Location = new Point(140, 220), Width = 300 };

        Controls.AddRange(new Control[] { lblModo, cmbModo, lblQuery, txtRedmineQuery, lblOutDir, txtOutDir, lblTemplate, txtTemplate, lblHeadless, chkHeadless, lblDiagnostics, txtDiagnosticsDir });
    }
}

public class TabConexoesControl : UserControl
{
    public TextBox txtRedmineUrl = null!;
    public TextBox txtRedmineLogin = null!;
    public TextBox txtRedminePassword = null!;
    public TextBox txtGitHubLogin = null!;
    public TextBox txtGitHubPassword = null!;
    public TextBox txtGogsUrl = null!;
    public TextBox txtGogsLogin = null!;
    public TextBox txtGogsPassword = null!;

    public TabConexoesControl()
    {
        var lblRedmineUrl = new Label { Text = "Redmine URL:", Location = new Point(20, 22), Width = 110 };
        txtRedmineUrl = new TextBox { Location = new Point(140, 20), Width = 300 };

        var lblRedmineLogin = new Label { Text = "Redmine Login:", Location = new Point(20, 62), Width = 110 };
        txtRedmineLogin = new TextBox { Location = new Point(140, 60), Width = 300 };

        var lblRedminePassword = new Label { Text = "Redmine Senha:", Location = new Point(20, 102), Width = 110 };
        txtRedminePassword = new TextBox { Location = new Point(140, 100), Width = 300, UseSystemPasswordChar = true };

        var lblGitHub = new Label { Text = "=== GitHub ===", Location = new Point(20, 142), Width = 110, Font = new Font(Font, FontStyle.Bold) };
        var lblGitHubLogin = new Label { Text = "GitHub Login:", Location = new Point(20, 167), Width = 110 };
        txtGitHubLogin = new TextBox { Location = new Point(140, 165), Width = 300 };

        var lblGitHubPassword = new Label { Text = "GitHub Senha:", Location = new Point(20, 207), Width = 110 };
        txtGitHubPassword = new TextBox { Location = new Point(140, 205), Width = 300, UseSystemPasswordChar = true };

        var lblGogs = new Label { Text = "=== Gogs ===", Location = new Point(20, 247), Width = 110, Font = new Font(Font, FontStyle.Bold) };
        var lblGogsUrl = new Label { Text = "Gogs URL:", Location = new Point(20, 272), Width = 110 };
        txtGogsUrl = new TextBox { Location = new Point(140, 270), Width = 300 };

        var lblGogsLogin = new Label { Text = "Gogs Login:", Location = new Point(20, 312), Width = 110 };
        txtGogsLogin = new TextBox { Location = new Point(140, 310), Width = 300 };

        var lblGogsPassword = new Label { Text = "Gogs Senha:", Location = new Point(20, 352), Width = 110 };
        txtGogsPassword = new TextBox { Location = new Point(140, 350), Width = 300, UseSystemPasswordChar = true };

        Controls.AddRange(new Control[] { 
            lblRedmineUrl, txtRedmineUrl, 
            lblRedmineLogin, txtRedmineLogin, 
            lblRedminePassword, txtRedminePassword,
            lblGitHub, lblGitHubLogin, txtGitHubLogin, lblGitHubPassword, txtGitHubPassword,
            lblGogs, lblGogsUrl, txtGogsUrl, lblGogsLogin, txtGogsLogin, lblGogsPassword, txtGogsPassword 
        });
    }
}

public class TabAvancadoControl : UserControl
{
    public TextBox txtPrUrl = null!;
    public TextBox txtIssuesUrl = null!;

    public TabAvancadoControl()
    {
        var lblPrUrl = new Label { Text = "URL PR Padrão:", Location = new Point(20, 22), Width = 110 };
        txtPrUrl = new TextBox { Location = new Point(140, 20), Width = 300 };

        var lblIssuesUrl = new Label { Text = "Issues URL:", Location = new Point(20, 62), Width = 110 };
        txtIssuesUrl = new TextBox { Location = new Point(140, 60), Width = 300 };

        Controls.AddRange(new Control[] { lblPrUrl, txtPrUrl, lblIssuesUrl, txtIssuesUrl });
    }
}