using Evidenciador.UI.Forms;

namespace Evidenciador.UI;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}