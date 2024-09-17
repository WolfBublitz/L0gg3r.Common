using YamlDotNet.Serialization;

public class DotNetVersion(string version)
{
    [YamlMember(Alias = "dotnet-version", ApplyNamingConventions = false)]
    public string Version { get; set; } = version;
}