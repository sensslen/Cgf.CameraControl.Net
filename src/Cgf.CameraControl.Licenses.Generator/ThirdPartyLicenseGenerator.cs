using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Cgf.CameraControl.Licenses.Generator;

/// Turns the nuget-license report and the licence texts beside it into what the licences window
/// shows.
///
/// Both are fixed at build time, so reading them, parsing them and formatting them again on every
/// start would be work done to arrive at a constant. What this emits is the finished strings the
/// window binds to, which also keeps the published binary honest: nothing about the notices depends
/// on a resource being found or a deserializer surviving trimming.
[Generator]
public sealed class ThirdPartyLicenseGenerator : IIncrementalGenerator
{
    private const string FileName = "third-party-licenses.json";
    private const string PackageFolder = "packages";
    private const string SpdxFolder = "spdx";

    /// A numbered condition, at the start of a line: "1." or "2)".
    private static readonly Regex Numbered = new(@"^\d+[.)]\s", RegexOptions.Compiled);

    private static readonly DiagnosticDescriptor Missing = new(
        "CGFLIC001",
        "The third party licence report is missing",
        "No additional file named " + FileName + " was supplied, so the licences window would be empty",
        "Licences",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Unreadable = new(
        "CGFLIC002",
        "The third party licence report cannot be read",
        "{0} is not the JSON array nuget-license writes: {1}",
        "Licences",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Untexted = new(
        "CGFLIC003",
        "A licence the application ships under has no text",
        "{0} is under '{1}' and published no licence text of its own, and no {1}.txt sits in the " +
        SpdxFolder + " folder. Run packaging/licenses/fetch-texts.py.",
        "Licences",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Tuples rather than classes of their own: what travels the pipeline has to compare by value
        // or the generator runs again on every keystroke, and netstandard2.0 has no records without
        // a polyfill package to supply the init accessor they compile to.
        var reports = context.AdditionalTextsProvider
            .Where(text => Path.GetFileName(text.Path) == FileName)
            .Select((text, token) => (Path: text.Path, Content: Read(text, token)))
            .Collect();

        // Two kinds, told apart by the folder they sit in: the licence text a package published for
        // itself, named after the package, and the text of an SPDX identifier, named after it.
        var texts = context.AdditionalTextsProvider
            .Where(text => Path.GetExtension(text.Path) == ".txt" && Folder(text.Path) is PackageFolder or SpdxFolder)
            .Select((text, token) => (
                Folder: Folder(text.Path),
                Name: Path.GetFileNameWithoutExtension(text.Path),
                Content: Read(text, token)))
            .Collect();

        context.RegisterSourceOutput(
            reports.Combine(texts),
            (production, both) => Emit(production, both.Left, both.Right));
    }

    private static string Read(AdditionalText text, System.Threading.CancellationToken token) =>
        text.GetText(token)?.ToString() ?? string.Empty;

    private static string Folder(string path) => Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty;

    private static void Emit(
        SourceProductionContext context,
        ImmutableArray<(string Path, string Content)> reports,
        ImmutableArray<(string Folder, string Name, string Content)> texts)
    {
        if (reports.Length == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(Missing, Location.None));
            return;
        }

        var packages = new List<Package>();
        foreach (var report in reports)
        {
            try
            {
                packages.AddRange(Parse(report.Content));
            }
            catch (JsonException ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(Unreadable, Location.None, report.Path, ex.Message));
                return;
            }
            catch (InvalidReportException ex)
            {
                context.ReportDiagnostic(Diagnostic.Create(Unreadable, Location.None, report.Path, ex.Message));
                return;
            }
        }

        var own = Index(texts, PackageFolder);
        var spdx = Index(texts, SpdxFolder);

        // A licence with no text leaves the window naming a licence it cannot show, which is the one
        // thing a notices screen exists to do.
        foreach (var package in packages.Where(entry => TextFor(entry, own, spdx) is null))
        {
            context.ReportDiagnostic(Diagnostic.Create(Untexted, Location.None, package.Title, package.License));
        }

        context.AddSource("ThirdPartyLicenses.g.cs", SourceText.From(Render(packages, own, spdx), Encoding.UTF8));
    }

    /// Ordered here rather than trusted from the file, so the window reads the same however the
    /// report was produced.
    private static IEnumerable<Package> Parse(string content)
    {
        if (JToken.Parse(content) is not JArray entries)
        {
            throw new InvalidReportException("the root is not an array");
        }

        return entries
            .OfType<JObject>()
            .Select(entry => new Package(
                Text(entry, "PackageId") ?? string.Empty,
                $"{Text(entry, "PackageId")} {Text(entry, "PackageVersion")}".Trim(),
                Text(entry, "License"),
                Text(entry, "Copyright"),
                Text(entry, "Authors"),
                Text(entry, "PackageProjectUrl")))
            .OrderBy(package => package.Title, System.StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? Text(JObject entry, string name) =>
        entry[name] is JValue { Type: JTokenType.String } value ? (string?)value.Value : null;

    private static string Render(
        IReadOnlyList<Package> packages,
        IReadOnlyDictionary<string, string> own,
        IReadOnlyDictionary<string, string> spdx)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated/>");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("namespace Cgf.CameraControl.App.Licenses;");
        source.AppendLine();
        source.AppendLine("/// The packages this application ships, as nuget-license reported them at build time.");
        source.AppendLine("public static class ThirdPartyLicenses");
        source.AppendLine("{");
        source.AppendLine("    public static global::System.Collections.Generic.IReadOnlyList<ThirdPartyLicense> All { get; } =");
        source.AppendLine("    [");

        foreach (var package in packages)
        {
            source.Append("        new(");
            source.Append(Literal(package.Title));
            source.Append(", ");
            source.Append(Literal(package.License));
            source.Append(", ");
            source.Append(Literal(package.Copyright));
            source.Append(", ");
            source.Append(Literal(package.Authors));
            source.Append(", ");
            source.Append(Literal(package.ProjectUrl));
            source.Append(", ");

            // The whole text on every package that names the licence, rather than a table the window
            // would have to look through. The compiler keeps one copy of a literal however many
            // times it is written, so forty-eight MIT packages carry one MIT text between them.
            source.Append(Literal(TextFor(package, own, spdx)));
            source.AppendLine("),");
        }

        source.AppendLine("    ];");
        source.AppendLine("}");
        return source.ToString();
    }

    private static Dictionary<string, string> Index(
        ImmutableArray<(string Folder, string Name, string Content)> texts,
        string folder) =>
        texts
            .Where(text => text.Folder == folder)
            .ToDictionary(text => text.Name, text => text.Content, System.StringComparer.OrdinalIgnoreCase);

    /// What the package published for itself if it published anything, and the text of the identifier
    /// it declared otherwise. A package's own text says who holds the copyright; the identifier's
    /// text leaves that as a placeholder, so it is the fallback rather than the first choice.
    private static string? TextFor(
        Package package,
        IReadOnlyDictionary<string, string> own,
        IReadOnlyDictionary<string, string> spdx)
    {
        if (own.TryGetValue(package.Id, out var published))
        {
            return Reflow(published);
        }

        return package.License is { } license && spdx.TryGetValue(license, out var generic) ? Reflow(generic) : null;
    }

    /// The licence files are hard wrapped at roughly eighty columns, and a window that wraps them
    /// again breaks each of those lines a second time, leaving a stub word on every other line. Each
    /// paragraph becomes one line here and the window does the only wrapping.
    ///
    /// A blank line ends a paragraph, and so does a numbered clause: some of these files separate
    /// their conditions that way and some do not, and a list run together into one block is unreadable
    /// either way.
    private static string Reflow(string text)
    {
        var paragraphs = new List<StringBuilder> { new() };
        foreach (var raw in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || Numbered.IsMatch(line))
            {
                paragraphs.Add(new StringBuilder());
            }

            if (line.Length == 0)
            {
                continue;
            }

            var paragraph = paragraphs[paragraphs.Count - 1];
            paragraph.Append(paragraph.Length == 0 ? line : " " + line);
        }

        return string.Join(
            "\n\n",
            paragraphs.Select(paragraph => paragraph.ToString()).Where(paragraph => paragraph.Length > 0));
    }

    private static string Literal(string? value) =>
        value is null ? "null" : SyntaxFactory.Literal(value).ToFullString();

    /// A report that parses as JSON but is not the array nuget-license writes.
    private sealed class InvalidReportException(string message) : System.Exception(message);

    /// Built inside one source output rather than carried through the pipeline, so reference
    /// equality is all it needs.
    private sealed class Package(
        string id,
        string title,
        string? license,
        string? copyright,
        string? authors,
        string? projectUrl)
    {
        public string Id { get; } = id;

        public string Title { get; } = title;

        public string? License { get; } = license;

        public string? Copyright { get; } = copyright;

        public string? Authors { get; } = authors;

        public string? ProjectUrl { get; } = projectUrl;
    }
}
