using System.Reflection;

namespace EarClarinet.App.Services;

public static class AppInfo
{
    public const string Name = "EarClarinet";
    public const string License = "MIT License";
    public const string KoFiUrl = "https://ko-fi.com/gatacampestre";

    public static string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
}
