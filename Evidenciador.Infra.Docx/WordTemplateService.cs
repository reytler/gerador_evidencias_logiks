using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Evidenciador.Core.Abstractions;
using Evidenciador.Core.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Evidenciador.Infra.Docx;

public sealed class WordTemplateService : IWordTemplateService
{
    private readonly ILogger<WordTemplateService> _logger;

    public WordTemplateService(ILogger<WordTemplateService> logger)
    {
        _logger = logger;
    }

    public async Task<string> GenerateAsync(RedmineIssueData issueData, string templatePath, string outputDir, CancellationToken ct)
    {
        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"Template não encontrado: {templatePath}");

        Directory.CreateDirectory(outputDir);
        
        var issueDir = Path.Combine(outputDir, $"issue_{issueData.IssueId}");
        Directory.CreateDirectory(issueDir);
        
        var outputPath = Path.Combine(issueDir, $"issue_{issueData.IssueId}.docx");

        await Task.Run(() =>
        {
            File.Copy(templatePath, outputPath, overwrite: true);

            using var doc = WordprocessingDocument.Open(outputPath, isEditable: true);
            var body = doc.MainDocumentPart!.Document.Body!;

            var placeholders = BuildPlaceholders(issueData);
            ReplacePlaceholdersInBody(body, placeholders);

            if (!string.IsNullOrEmpty(issueData.ScreenshotPath) && File.Exists(issueData.ScreenshotPath))
                ReplaceScreenshotPlaceholder(doc, body, issueData.ScreenshotPath, "screenshot");

            if (issueData.Evidencias.Count > 0)
                InsertDiffContent(body, issueData.Evidencias, _logger);

            doc.MainDocumentPart.Document.Save();

            if (issueData.Evidencias.Count > 0)
                VerifyDiffPlaceholderRemoved(doc, outputPath);
        });

        _logger.LogInformation("Documento Word gerado: {Path}", outputPath);
        return outputPath;
    }

    private static Dictionary<string, string> BuildPlaceholders(RedmineIssueData data)
    {
        return new Dictionary<string, string>
        {
            [TemplatePlaceholders.CodigoIssue] = data.IssueId,
            [TemplatePlaceholders.NomeProjeto] = data.NomeProjeto,
            [TemplatePlaceholders.DataAtual] = DateTime.Now.ToString("dd/MM/yyyy"),
            [TemplatePlaceholders.NomeAtividade] = data.NomeAtividade,
            [TemplatePlaceholders.PerfilDesenvolvedor] = data.PerfilDesenvolvedor,
            [TemplatePlaceholders.NomeDesenvolvedor] = data.NomeDesenvolvedor,
            [TemplatePlaceholders.UrlPagina] = data.UrlPagina,
            [TemplatePlaceholders.Descricao] = data.Descricao,
            [TemplatePlaceholders.UrlPrGithub] = string.Join("\n", data.PrUrls.Select(u => u.ToString())),
        };
    }

    private static void ReplacePlaceholdersInBody(W.Body body, Dictionary<string, string> placeholders)
    {
        foreach (var paragraph in body.Descendants<W.Paragraph>())
        {
            var fullText = string.Concat(paragraph.Descendants<W.Text>().Select(t => t.Text));
            if (!placeholders.Keys.Any(k => fullText.Contains(k))) continue;

            var multilineKey = placeholders
                .FirstOrDefault(p => fullText.Contains(p.Key) && p.Value.Contains('\n'));

            var runs = paragraph.Elements<W.Run>().ToList();
            if (!runs.Any()) continue;

            var runProps = runs.First().GetFirstChild<W.RunProperties>()?.CloneNode(true) as W.RunProperties;
            foreach (var run in runs) run.Remove();

            if (multilineKey.Key != null)
            {
                var prefix = fullText;
                foreach (var (key, value) in placeholders)
                    if (key != multilineKey.Key)
                        prefix = prefix.Replace(key, value);

                var parts = prefix.Split(multilineKey.Key);
                var lines = multilineKey.Value.Split('\n');

                void AppendRun(string text)
                {
                    var r = new W.Run();
                    if (runProps is not null) r.AppendChild((W.RunProperties)runProps.CloneNode(true));
                    r.AppendChild(new W.Text(text) { Space = SpaceProcessingModeValues.Preserve });
                    paragraph.AppendChild(r);
                }

                if (!string.IsNullOrEmpty(parts[0])) AppendRun(parts[0]);

                for (int i = 0; i < lines.Length; i++)
                {
                    AppendRun(lines[i]);
                    if (i < lines.Length - 1)
                    {
                        var breakRun = new W.Run();
                        if (runProps is not null) breakRun.AppendChild((W.RunProperties)runProps.CloneNode(true));
                        breakRun.AppendChild(new W.Break());
                        paragraph.AppendChild(breakRun);
                    }
                }

                if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1])) AppendRun(parts[1]);
            }
            else
            {
                foreach (var (key, value) in placeholders)
                    fullText = fullText.Replace(key, value);

                var newRun = new W.Run();
                if (runProps is not null) newRun.AppendChild<W.RunProperties>(runProps);
                newRun.AppendChild(new W.Text(fullText) { Space = SpaceProcessingModeValues.Preserve });
                paragraph.AppendChild(newRun);
            }
        }
    }

    private static void ReplaceScreenshotPlaceholder(WordprocessingDocument doc, W.Body body, string imagePath, string imageName)
    {
        var placeholder = body.Descendants<W.Paragraph>()
            .FirstOrDefault(p => p.InnerText.Contains(TemplatePlaceholders.Screenshot));

        if (placeholder == null) return;

        foreach (var run in placeholder.Elements<W.Run>().ToList())
            run.Remove();

        var imagePart = doc.MainDocumentPart!.AddImagePart(ImagePartType.Png);
        using (var stream = File.OpenRead(imagePath))
            imagePart.FeedData(stream);

        var imageRelId = doc.MainDocumentPart.GetIdOfPart(imagePart);
        placeholder.AppendChild(CreateImageRun(imageRelId, 1U, $"{imageName}.png"));
    }

    private static void InsertDiffContent(W.Body body, List<PullRequestEvidence> evidencias, ILogger logger)
    {
        var diffPlaceholders = body.Descendants<W.Paragraph>()
            .Where(p => p.InnerText.Contains(TemplatePlaceholders.DiffContent))
            .ToList();

        if (diffPlaceholders.Count != 1)
        {
            logger.LogError(
                "Template inválido para evidências: esperado exatamente 1 placeholder {Placeholder}, encontrado {Count}",
                TemplatePlaceholders.DiffContent,
                diffPlaceholders.Count);
            throw new InvalidOperationException(
                $"Template inválido: esperado exatamente 1 placeholder {TemplatePlaceholders.DiffContent}, encontrado {diffPlaceholders.Count}.");
        }

        var diffPlaceholder = diffPlaceholders[0];
        OpenXmlElement anchor = diffPlaceholder;

        foreach (var evidencia in evidencias)
        {
            var titlePara = new W.Paragraph(new W.Run(new W.Text($"PR: {evidencia.PullRequestUrl}")));
            anchor.InsertAfterSelf(titlePara);
            anchor = titlePara;

            foreach (var file in evidencia.Files)
            {
                var fileName = new W.Paragraph(new W.Run(new W.Text($"  Arquivo: {file.Path}")));
                fileName.ParagraphProperties = new W.ParagraphProperties(new W.Indentation { Left = "720" });
                anchor.InsertAfterSelf(fileName);
                anchor = fileName;

                var table = CreateDiffTable(file.Lines);
                anchor.InsertAfterSelf(table);
                anchor = table;
            }

            var spacer = new W.Paragraph(new W.Run(new W.Text(" ")));
            anchor.InsertAfterSelf(spacer);
            anchor = spacer;
        }

        diffPlaceholder.Remove();
        logger.LogInformation("Inseridas {EvidenceCount} evidências de diff no placeholder {Placeholder}", evidencias.Count, TemplatePlaceholders.DiffContent);
    }

    private static void VerifyDiffPlaceholderRemoved(WordprocessingDocument doc, string outputPath)
    {
        using var stream = doc.MainDocumentPart!.GetStream(FileMode.Open, FileAccess.Read);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var documentXml = reader.ReadToEnd();

        if (documentXml.Contains(TemplatePlaceholders.DiffContent, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Falha ao gerar DOCX '{outputPath}': o placeholder {TemplatePlaceholders.DiffContent} permaneceu em word/document.xml após inserir as evidências.");
    }

    private static W.Table CreateDiffTable(IReadOnlyList<DiffLineEvidence> lines)
    {
        var table = new W.Table();

        var borders = new W.TableBorders(
            new W.TopBorder { Val = BorderValues.Single, Size = 6, Color = "D0D7DE" },
            new W.BottomBorder { Val = BorderValues.Single, Size = 6, Color = "D0D7DE" },
            new W.LeftBorder { Val = BorderValues.Single, Size = 6, Color = "D0D7DE" },
            new W.RightBorder { Val = BorderValues.Single, Size = 6, Color = "D0D7DE" },
            new W.InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "D0D7DE" },
            new W.InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "D0D7DE" });

        table.AppendChild(new W.TableProperties(
            borders,
            new W.TableWidth { Type = W.TableWidthUnitValues.Pct, Width = "5000" }));

        table.AppendChild(new W.TableGrid(
            new W.GridColumn { Width = "900" },
            new W.GridColumn { Width = "9000" }));

        foreach (var line in lines)
        {
            var row = new W.TableRow();
            row.Append(LineNumberCell(line));
            row.Append(CodeCell(line));
            table.Append(row);
        }

        return table;
    }

    private static W.TableCell LineNumberCell(DiffLineEvidence line)
    {
        var num = line.Kind switch
        {
            DiffLineKind.HunkHeader => "",
            DiffLineKind.Placeholder => "",
            DiffLineKind.Addition => line.NewLineNumber?.ToString() ?? "",
            DiffLineKind.Deletion => line.OldLineNumber?.ToString() ?? "",
            _ => line.NewLineNumber?.ToString() ?? line.OldLineNumber?.ToString() ?? "",
        };

        return new W.TableCell(
            new W.TableCellProperties(new W.Shading { Val = W.ShadingPatternValues.Clear, Color = "auto", Fill = "F6F8FA" }),
            new W.Paragraph(new W.ParagraphProperties(new W.SpacingBetweenLines { After = "0" }),
                new W.Run(new W.RunProperties(new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" }, new W.FontSize { Val = "18" }), new W.Text(num))));
    }

    private static W.TableCell CodeCell(DiffLineEvidence line)
    {
        var bg = line.Kind switch
        {
            DiffLineKind.Addition => "DAFBE1",
            DiffLineKind.Deletion => "FFEBE9",
            _ => "F6F8FA",
        };

        var text = line.Text;
        if (string.IsNullOrEmpty(text)) text = " ";

        return new W.TableCell(
            new W.TableCellProperties(new W.Shading { Val = W.ShadingPatternValues.Clear, Color = "auto", Fill = bg }),
            new W.Paragraph(new W.ParagraphProperties(new W.SpacingBetweenLines { After = "0" }),
                new W.Run(new W.RunProperties(new W.RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" }, new W.FontSize { Val = "18" }), new W.Text(text) { Space = SpaceProcessingModeValues.Preserve })));
    }

    private static W.Run CreateImageRun(string relationshipId, uint imageId, string imageName)
    {
        const long widthEmu = 5400000L;
        const long heightEmu = 3600000L;

        return new W.Run(
            new W.Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = widthEmu, Cy = heightEmu },
                    new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                    new DW.DocProperties { Id = imageId, Name = imageName },
                    new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = imageName },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relationshipId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0L, Y = 0L },
                                        new A.Extents { Cx = widthEmu, Cy = heightEmu }),
                                    new A.PresetGeometry(new A.AdjustValueList())
                                    { Preset = A.ShapeTypeValues.Rectangle })))
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
                { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U }));
    }
}
