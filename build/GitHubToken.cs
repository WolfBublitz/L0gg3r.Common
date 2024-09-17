using YamlDotNet.Serialization;

public class GitHubToken(string token)
{
    [YamlMember(Alias = "GITHUB_TOKEN", ApplyNamingConventions = false)]
    public string Token { get; set; } = token;
}