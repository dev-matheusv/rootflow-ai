using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RootFlow.Application.Abstractions.Documents;

namespace RootFlow.Infrastructure.Documents;

public sealed class QuestPdfTrainingCertificateRenderer : ITrainingCertificateRenderer
{
    public byte[] Render(TrainingCertificateRenderRequest request)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(0);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontColor(Colors.Grey.Darken4));

                page.Content().Element(content =>
                {
                    // Decorative double border
                    content.Padding(18).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(4)
                        .Border(2).BorderColor(Colors.Grey.Darken2).Padding(36)
                        .Column(col =>
                        {
                            col.Spacing(10);

                            col.Item().Text("RootFlow").FontSize(11)
                                .LetterSpacing(0.4f).Bold().FontColor(Colors.Grey.Darken1);

                            col.Item().PaddingTop(12).AlignCenter()
                                .Text("CERTIFICADO DE CONCLUSÃO")
                                .FontSize(28).Bold().LetterSpacing(0.18f);

                            col.Item().PaddingTop(8).AlignCenter()
                                .Text("Certificamos que")
                                .FontSize(13).FontColor(Colors.Grey.Darken1);

                            col.Item().PaddingTop(6).AlignCenter()
                                .Text(request.EmployeeName)
                                .FontSize(34).Bold();

                            col.Item().PaddingTop(10).AlignCenter()
                                .Text("concluiu com aproveitamento o programa de treinamento")
                                .FontSize(13).FontColor(Colors.Grey.Darken1);

                            col.Item().PaddingTop(4).AlignCenter()
                                .Text(request.ProgramName)
                                .FontSize(22).Bold().FontColor(Colors.Grey.Darken3);

                            if (!string.IsNullOrWhiteSpace(request.ProgramDescription))
                            {
                                col.Item().PaddingTop(4).AlignCenter()
                                    .Text(request.ProgramDescription)
                                    .FontSize(11).FontColor(Colors.Grey.Darken1).Italic();
                            }

                            col.Item().PaddingTop(28).AlignCenter()
                                .Text($"oferecido por {request.WorkspaceName}.")
                                .FontSize(12).FontColor(Colors.Grey.Darken1);

                            col.Item().PaddingTop(40).Row(footer =>
                            {
                                footer.RelativeItem().Column(left =>
                                {
                                    left.Item().Text("Emitido em")
                                        .FontSize(9).FontColor(Colors.Grey.Darken1)
                                        .LetterSpacing(0.18f);
                                    left.Item().Text(request.IssuedAtUtc.ToString("dd 'de' MMMM 'de' yyyy",
                                            System.Globalization.CultureInfo.GetCultureInfo("pt-BR")))
                                        .FontSize(12).Bold();
                                });

                                footer.RelativeItem().Column(right =>
                                {
                                    right.Item().AlignRight().Text("Código de verificação")
                                        .FontSize(9).FontColor(Colors.Grey.Darken1)
                                        .LetterSpacing(0.18f);
                                    right.Item().AlignRight().Text(request.CertificateCode)
                                        .FontSize(12).Bold().LetterSpacing(0.12f);
                                    right.Item().PaddingTop(2).AlignRight()
                                        .Text(request.VerificationUrl)
                                        .FontSize(9).FontColor(Colors.Grey.Darken1);
                                });
                            });
                        });
                });
            });
        }).GeneratePdf();
    }
}
