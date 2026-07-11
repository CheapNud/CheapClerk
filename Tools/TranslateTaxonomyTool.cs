using System.ComponentModel;
using CheapClerk.Configuration;
using CheapClerk.Services;
using ModelContextProtocol.Server;

namespace CheapClerk.Tools;

[McpServerToolType]
public sealed class TranslateTaxonomyTool
{
    [McpServerTool(Name = "translate_taxonomy"), Description("Fill in missing tag and document-type translations for every supported culture. Run after adding tags or when labels show untranslated.")]
    public static async Task<string> TranslateTaxonomy(
        TaxonomyTranslationService taxonomyTranslation,
        CancellationToken cancellationToken = default)
    {
        var reportLines = new List<string>();
        foreach (var culture in ClassificationOptions.SupportedCultures)
        {
            var sweep = await taxonomyTranslation.EnsureTranslationsAsync(culture, cancellationToken);
            reportLines.Add($"{culture}: {sweep.AlreadyTranslated} up to date, {sweep.NewlyTranslated} translated, {sweep.Failed} failed");
        }
        return string.Join("\n", reportLines);
    }
}
