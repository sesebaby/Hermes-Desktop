using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HermesDesktop.Tests.Views;

[TestClass]
public sealed class DeveloperPageResourceTests
{
    [TestMethod]
    public void CodeBehindResourceLookups_AllExistInSupportedLocales()
    {
        var repositoryRoot = FindRepositoryRoot();
        var appRoot = Path.Combine(repositoryRoot, "Desktop", "HermesDesktop");
        var lookupKeys = EnumerateSourceFiles(appRoot)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), "ResourceLoader\\.GetString\\(\"(?<key>[^\"]+)\"\\)"))
            .Select(match => match.Groups["key"].Value.Replace('/', '.'))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var localeFiles = new[]
        {
            Path.Combine(repositoryRoot, "Desktop", "HermesDesktop", "Strings", "en-us", "Resources.resw"),
            Path.Combine(repositoryRoot, "Desktop", "HermesDesktop", "Strings", "zh-cn", "Resources.resw"),
        };

        foreach (var localeFile in localeFiles)
        {
            var resourceNames = XDocument.Load(localeFile)
                .Descendants("data")
                .Select(element => element.Attribute("name")?.Value)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .ToHashSet(StringComparer.Ordinal);

            var missing = lookupKeys
                .Where(key => !resourceNames.Contains(key))
                .ToArray();

            Assert.AreEqual(
                0,
                missing.Length,
                $"{localeFile} is missing code-behind resources: {string.Join(", ", missing)}");
        }
    }

    private static IEnumerable<string> EnumerateSourceFiles(string appRoot)
        => Directory.EnumerateFiles(appRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(part => string.Equals(part, "bin", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(part, "obj", StringComparison.OrdinalIgnoreCase)));

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "HermesDesktop.sln")))
                return directory.FullName;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from AppContext.BaseDirectory.");
    }
}
