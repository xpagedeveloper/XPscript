namespace XPScript.Compiler;

internal sealed class UIWebBootstrapPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = StyleForm(generated);
        generated = StyleListView(generated);
        return generated;
    }

    private static string StyleForm(string generated)
    {
        generated = generated
            .Replace("class=\\\"xpscript-uiform\\\" style=\\\"display:grid;", "class=\\\"xpscript-uiform container-fluid py-3\\\" style=\\\"display:grid;", StringComparison.Ordinal)
            .Replace("<h1 style=\\\"grid-column:1/-1\\\">", "<h1 class=\\\"h3 mb-4\\\" style=\\\"grid-column:1/-1\\\">", StringComparison.Ordinal)
            .Replace("<div class=\\\"xpscript-uiform-field\\\"", "<div class=\\\"xpscript-uiform-field mb-3\\\"", StringComparison.Ordinal)
            .Replace("><label for=\\\"xps_", "><label class=\\\"form-label\\\" for=\\\"xps_", StringComparison.Ordinal)
            .Replace("<textarea id=\\\"xps_", "<textarea class=\\\"form-control\\\" id=\\\"xps_", StringComparison.Ordinal)
            .Replace("<select id=\\\"xps_", "<select class=\\\"form-select\\\" id=\\\"xps_", StringComparison.Ordinal)
            .Replace("<input type=\\\"text\\\"", "<input class=\\\"form-control\\\" type=\\\"text\\\"", StringComparison.Ordinal)
            .Replace("<input type=\\\"number\\\"", "<input class=\\\"form-control\\\" type=\\\"number\\\"", StringComparison.Ordinal)
            .Replace("<input type=\\\"date\\\"", "<input class=\\\"form-control\\\" type=\\\"date\\\"", StringComparison.Ordinal)
            .Replace("<input type=\\\"time\\\"", "<input class=\\\"form-control\\\" type=\\\"time\\\"", StringComparison.Ordinal)
            .Replace("<input type=\\\"datetime-local\\\"", "<input class=\\\"form-control\\\" type=\\\"datetime-local\\\"", StringComparison.Ordinal)
            .Replace("<input type=\\\"month\\\"", "<input class=\\\"form-control\\\" type=\\\"month\\\"", StringComparison.Ordinal)
            .Replace("<input type=\\\"color\\\"", "<input class=\\\"form-control form-control-color\\\" type=\\\"color\\\"", StringComparison.Ordinal)
            .Replace("<input type=\\\"email\\\"", "<input class=\\\"form-control\\\" type=\\\"email\\\"", StringComparison.Ordinal)
            .Replace("<input type=\\\"url\\\"", "<input class=\\\"form-control\\\" type=\\\"url\\\"", StringComparison.Ordinal)
            .Replace("<input type=\\\"password\\\"", "<input class=\\\"form-control\\\" type=\\\"password\\\"", StringComparison.Ordinal)
            .Replace("<input type=\\\"range\\\"", "<input class=\\\"form-range\\\" type=\\\"range\\\"", StringComparison.Ordinal)
            .Replace("<input type=\\\"checkbox\\\"", "<input class=\\\"form-check-input me-2\\\" type=\\\"checkbox\\\"", StringComparison.Ordinal)
            .Replace("<input type=\\\"radio\\\"", "<input class=\\\"form-check-input me-2\\\" type=\\\"radio\\\"", StringComparison.Ordinal)
            .Replace("<button style=\\\"grid-column:1/-1\\\" type=\\\"submit\\\"", "<button class=\\\"btn btn-primary\\\" style=\\\"grid-column:1/-1\\\" type=\\\"submit\\\"", StringComparison.Ordinal);

        return generated;
    }

    private static string StyleListView(string generated)
    {
        generated = generated
            .Replace("<section class=\\\"xps-list-view\\\"", "<section class=\\\"xps-list-view container-fluid py-3\\\"", StringComparison.Ordinal)
            .Replace("<h2>", "<h2 class=\\\"h3 mb-3\\\">", StringComparison.Ordinal)
            .Replace("<label>Filter <input type=\\\"search\\\" class=\\\"xps-list-filter\\\" autocomplete=\\\"off\\\"></label>", "<div class=\\\"mb-3\\\"><label class=\\\"form-label w-100\\\">Filter <input type=\\\"search\\\" class=\\\"xps-list-filter form-control\\\" autocomplete=\\\"off\\\"></label></div>", StringComparison.Ordinal)
            .Replace("<table><thead><tr>", "<div class=\\\"table-responsive\\\"><table class=\\\"table table-striped table-hover align-middle\\\"><thead class=\\\"table-light\\\"><tr>", StringComparison.Ordinal)
            .Replace("<button type=\\\"button\\\" class=\\\"xps-list-sort\\\"", "<button type=\\\"button\\\" class=\\\"xps-list-sort btn btn-link text-decoration-none text-body fw-semibold p-0\\\"", StringComparison.Ordinal)
            .Replace("<tr data-row-index=\\\"", "<tr class=\\\"xps-list-row\\\" data-row-index=\\\"", StringComparison.Ordinal)
            .Replace("</tbody></table>", "</tbody></table></div>", StringComparison.Ordinal);

        return generated;
    }
}
