namespace Evidenciador.Core.Models;

public static class TemplatePlaceholders
{
    public const string CodigoIssue = "{{CODIGO_ISSUE}}";
    public const string NomeProjeto = "{{NOME_PROJETO}}";
    public const string DataAtual = "{{DATA_ATUAL}}";
    public const string NomeAtividade = "{{NOME_ATIVIDADE}}";
    public const string PerfilDesenvolvedor = "{{PERFIL_DESENVOLVEDOR}}";
    public const string NomeDesenvolvedor = "{{NOME_DESENVOLVEDOR}}";
    public const string UrlPagina = "{{URL_PAGINA_ATIVIDADE}}";
    public const string Screenshot = "{{PRINT_TELA_ISSUE}}";
    public const string Descricao = "{{TEXTO_DESCRICAO_CAMPO_ISSUE}}";
    public const string PrintPr = "{{PRINT_PR_COM_CODIGO_EXIBIDO}}";
    public const string UrlPrGithub = "{{URL_PR_GITHUB}}";
    public const string DiffContent = "{{CONTEUDO_DIFF}}";
}