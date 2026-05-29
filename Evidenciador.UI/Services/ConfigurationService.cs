using System.Text.Json;
using System.Text.Json.Serialization;

namespace Evidenciador.UI.Services;

public class AppSettings
{
    [JsonPropertyName("GITHUB_USERNAME")]
    public string GitHubUsername { get; set; } = "";

    [JsonPropertyName("GITHUB_PASSWORD")]
    public string GitHubPassword { get; set; } = "";

    [JsonPropertyName("Redmine")]
    public RedmineSettings Redmine { get; set; } = new();

    [JsonPropertyName("Evidenciador")]
    public EvidenciadorSettings Evidenciador { get; set; } = new();

    [JsonPropertyName("Gogs")]
    public GogsSettings Gogs { get; set; } = new();
}

public class RedmineSettings
{
    [JsonPropertyName("BaseUrl")]
    public string BaseUrl { get; set; } = "";

    [JsonPropertyName("LoginUrl")]
    public string LoginUrl { get; set; } = "/login";

    [JsonPropertyName("IssuesUrl")]
    public string IssuesUrl { get; set; } = "/issues";

    [JsonPropertyName("Username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("Password")]
    public string Password { get; set; } = "";
}

public class EvidenciadorSettings
{
    [JsonPropertyName("Mode")]
    public string Mode { get; set; } = "SinglePr";

    [JsonPropertyName("RedmineQuery")]
    public string RedmineQuery { get; set; } = "assigned_to_id=me";

    [JsonPropertyName("PrUrl")]
    public string PrUrl { get; set; } = "";

    [JsonPropertyName("Out")]
    public string Out { get; set; } = "";

    [JsonPropertyName("OutDir")]
    public string OutDir { get; set; } = ".\\out";

    [JsonPropertyName("Headless")]
    public bool Headless { get; set; } = true;

    [JsonPropertyName("DiagnosticsDir")]
    public string DiagnosticsDir { get; set; } = "";

    [JsonPropertyName("TemplatePath")]
    public string TemplatePath { get; set; } = "TEMPLATE_EVIDENCIA.docx";
}

public class GogsSettings
{
    [JsonPropertyName("BaseUrl")]
    public string BaseUrl { get; set; } = "";

    [JsonPropertyName("Username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("Password")]
    public string Password { get; set; } = "";
}

public class ConfigurationService
{
    private readonly string _configPath;
    private readonly JsonSerializerOptions _jsonOptions;

    public ConfigurationService()
    {
        var baseDir = AppContext.BaseDirectory;
        var solutionDir = Path.GetDirectoryName(baseDir);
        
        while (solutionDir != null && !File.Exists(Path.Combine(solutionDir, "Evidenciador.slnx")))
        {
            solutionDir = Path.GetDirectoryName(solutionDir);
        }

        _configPath = Path.Combine(solutionDir ?? baseDir, "Evidenciador.Cli", "appsettings.json");

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_configPath);
            return JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao carregar configurações: {ex.Message}", "Erro", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return new AppSettings();
        }
    }

    public bool Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(_configPath, json);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erro ao salvar configurações: {ex.Message}", "Erro", 
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    public string GetCliProjectPath()
    {
        var baseDir = AppContext.BaseDirectory;
        
        var possiblePaths = new[]
        {
            Path.Combine(baseDir, "..", "..", "..", "..", "Evidenciador.Cli"),
            Path.Combine(baseDir, "..", "..", "..", "Evidenciador.Cli"),
            Path.Combine(baseDir, "..", "Evidenciador.Cli"),
            Path.Combine(baseDir, "Evidenciador.Cli"),
        };

        foreach (var path in possiblePaths)
        {
            var fullPath = Path.GetFullPath(path);
            if (Directory.Exists(fullPath))
            {
                return fullPath;
            }
        }

        var solutionDir = Path.GetDirectoryName(baseDir);
        
        while (solutionDir != null && !File.Exists(Path.Combine(solutionDir, "Evidenciador.slnx")))
        {
            solutionDir = Path.GetDirectoryName(solutionDir);
        }

        if (solutionDir == null)
        {
            solutionDir = baseDir;
        }

        return Path.Combine(solutionDir, "Evidenciador.Cli");
    }
}