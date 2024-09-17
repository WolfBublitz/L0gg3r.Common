using YamlDotNet.Serialization;

public class GitHubJob
{
    [YamlMember(Alias = "name", ApplyNamingConventions = false)]
    public string Name { get; set; }

    [YamlMember(Alias = "runs-on", ApplyNamingConventions = false)]
    public string RunsOn { get; set; }

    [YamlMember(Alias = "steps", ApplyNamingConventions = false)]
    public object[] Steps { get; set; }
}
