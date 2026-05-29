using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Evidenciador.Core.Abstractions;
using Evidenciador.Core.Models;

namespace Evidenciador.Infra.Docx;

public sealed class OpenXmlDocxEvidenceRenderer : IDocxEvidenceRenderer
{
    public Task RenderAsync(PullRequestEvidence evidence, string outputPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outDir))
            Directory.CreateDirectory(outDir);

        using var doc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document(new Body());

        var body = mainPart.Document.Body!;

        body.Append(ParagraphOf($"Evidenciador - PR evidence"));
        if (!string.IsNullOrWhiteSpace(evidence.PullRequestTitle))
            body.Append(ParagraphOf($"Title: {evidence.PullRequestTitle}"));
        body.Append(ParagraphOf($"PR: {evidence.PullRequestUrl}"));
        body.Append(ParagraphOf($"Files: {evidence.FilesUrl}"));
        body.Append(ParagraphOf($"CollectedAt (UTC): {evidence.CollectedAt:O}"));
        body.Append(new Paragraph(new Run(new Text(" "))));

        foreach (var file in evidence.Files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var title = file.Path;
            if (!string.IsNullOrWhiteSpace(file.ChangeType))
                title += $" ({file.ChangeType})";
            if (file.IsBinary) title += " [binary]";
            if (file.IsTruncated) title += " [truncated]";

            body.Append(TitleParagraph(title));
            body.Append(DiffTable(file.Lines));
            body.Append(new Paragraph(new Run(new Text(" "))));
        }

        mainPart.Document.Save();
        return Task.CompletedTask;
    }

    private static Paragraph TitleParagraph(string text)
    {
        var runProps = new RunProperties(
            new Bold(),
            new FontSize { Val = "24" });

        return new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { After = "120" }),
            new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Paragraph ParagraphOf(string text)
    {
        return new Paragraph(
            new Run(new Text(text) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static Table DiffTable(IReadOnlyList<DiffLineEvidence> lines)
    {
        var table = new Table();

        var borders = new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 6, Color = "D0D7DE" },
            new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "D0D7DE" },
            new LeftBorder { Val = BorderValues.Single, Size = 6, Color = "D0D7DE" },
            new RightBorder { Val = BorderValues.Single, Size = 6, Color = "D0D7DE" },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "D0D7DE" },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "D0D7DE" });

        table.AppendChild(new TableProperties(
            borders,
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" },
            new TableLook { Val = "04A0" }));

        // Column widths in twentieths of a point.
        table.AppendChild(new TableGrid(
            new GridColumn { Width = "900" },
            new GridColumn { Width = "9000" }));

        foreach (var line in lines)
        {
            var row = new TableRow();
            row.Append(LineNumberCell(line));
            row.Append(CodeCell(line));
            table.Append(row);
        }

        return table;
    }

    private static TableCell LineNumberCell(DiffLineEvidence line)
    {
        var num = FormatLineNumber(line);

        return new TableCell(
            CellProps(backgroundHex: "F6F8FA"),
            new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { After = "0" }),
                MonospaceRun(num)));
    }

    private static TableCell CodeCell(DiffLineEvidence line)
    {
        var bg = line.Kind switch
        {
            DiffLineKind.Addition => "DAFBE1",
            DiffLineKind.Deletion => "FFEBE9",
            _ => "F6F8FA",
        };

        var text = line.Text;
        if (string.IsNullOrEmpty(text)) text = " ";

        return new TableCell(
            CellProps(backgroundHex: bg),
            new Paragraph(
                new ParagraphProperties(new SpacingBetweenLines { After = "0" }),
                MonospaceRun(text)));
    }

    private static string FormatLineNumber(DiffLineEvidence line)
    {
        return line.Kind switch
        {
            DiffLineKind.HunkHeader => "",
            DiffLineKind.Placeholder => "",
            DiffLineKind.Addition => line.NewLineNumber?.ToString() ?? "",
            DiffLineKind.Deletion => line.OldLineNumber?.ToString() ?? "",
            _ => line.NewLineNumber?.ToString() ?? line.OldLineNumber?.ToString() ?? "",
        };
    }

    private static TableCellProperties CellProps(string backgroundHex)
    {
        return new TableCellProperties(
            new TableCellWidth { Type = TableWidthUnitValues.Auto },
            new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = backgroundHex },
            new TableCellMargin(new LeftMargin { Width = "80", Type = TableWidthUnitValues.Dxa }, new RightMargin { Width = "80", Type = TableWidthUnitValues.Dxa }));
    }

    private static Run MonospaceRun(string text)
    {
        var props = new RunProperties(
            new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
            new FontSize { Val = "18" });

        return new Run(props, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
    }
}
