using System.IO;
using System.Xml.Linq;

namespace BTDeviceBatteryInfo.Tests;

internal static class RepositoryPaths
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    /// <summary>Folder of the application project, found by walking up to the solution file.</summary>
    public static string AppProject { get; } = FindAppProject();

    public static IReadOnlySet<string> ReadKeys(string relativePath)
    {
        var document = XDocument.Load(Path.Combine(AppProject, relativePath));
        return document.Root!.Elements()
            .Select(element => (string?)element.Attribute(Xaml + "Key"))
            .Where(key => key is not null)
            .Select(key => key!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string FindAppProject()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (directory.GetFiles("*.slnx").Length > 0)
                return Path.Combine(directory.FullName, "BTDeviceBatteryInfo");
        }
        throw new DirectoryNotFoundException("The solution folder was not found above the test output directory.");
    }
}
