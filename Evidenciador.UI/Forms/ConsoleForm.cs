using System.Text;

namespace Evidenciador.UI.Forms;

public class ConsoleForm : Form
{
    private readonly TextBox _txtOutput;
    private readonly StringBuilder _sb = new();

    public ConsoleForm()
    {
        Text = "Evidenciador - Execução";
        Size = new Size(700, 500);
        StartPosition = FormStartPosition.CenterScreen;

        _txtOutput = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10)
        };

        Controls.Add(_txtOutput);
    }

    public void AppendOutput(string text)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => AppendOutput(text));
            return;
        }

        _sb.Append(text);
        _txtOutput.Text = _sb.ToString();
        _txtOutput.SelectionStart = _txtOutput.Text.Length;
        _txtOutput.ScrollToCaret();
    }
}