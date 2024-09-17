using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualBasic;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.Execution;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Utilities.Collections;
using Serilog;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;

class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Solution("L0gg3r.Common.sln")]
    [YamlMember]
    private readonly Solution solution;

    public static int Main() => Execute<Build>();


    Target CreateGithubTestWorkflow => _ => _
        .Executes(() =>
        {
            FileInfo file = new(solution.Path);

            Dictionary<string, object> jobs = [];

            foreach (string solutionFilePath in Directory.GetFiles(file.Directory.FullName, "*.sln", SearchOption.AllDirectories))
            {
                Solution solution = SolutionModelTasks.ParseSolution(solutionFilePath);

                Log.Information($"Processing solution: {solution.Name}");

                Project[] testProjects = solution.AllProjects
                    .Where(project => project.GetProperty("IsTestProject") == "true")
                    .ToArray();

                if (!testProjects.Any())
                {
                    Log.Information("No test projects found");
                    continue;
                }

                foreach (Project testProject in testProjects)
                {
                    Log.Information($"Found test project: {testProject.Name}");

                    string projectPath = Path.GetRelativePath(file.Directory.FullName, testProject.Path);

                    GitHubJob job = new()
                    {
                        Name = testProject.Name,
                        RunsOn = "ubuntu-latest",
                        Steps =
                        [
                            new
                            {
                                name = "Checkout",
                                uses = "actions/checkout@v4",
                                with = new
                                {
                                    submodules = "recursive",
                                    token = "${{ secrets.SUBMODULE_TOKEN }}"
                                }
                            },
                            new 
                            {
                                name = "Update Submodules",
                                run = "git submodule update --init --recursive"
                            },
                            new {
                                name = "Setup .NET",
                                uses = "actions/setup-dotnet@v2",
                                with = new DotNetVersion("8.0.x")
                            },
                            new
                            {
                                name = "Add GitHub NuGet Source",
                                run = "dotnet nuget add source \"https://nuget.pkg.github.com/WolfBublitz/index.json\" --username WolfBublitz --password ${GITHUB_TOKEN} --store-password-in-clear-text --name github",
                                env = new GitHubToken("${{ secrets.GITHUB_TOKEN }}"),
                            },
                            new
                            {
                                name = "Test",
                                run = $"dotnet test {projectPath} -c Release"
                            }
                        ]
                    };
                
                    jobs.Add(testProject.Name, job);
                }
            }

            var workflow = new
            {
                Name = $"{solution.Name} tests",
                On = "push",
                Jobs = jobs,
            };

            ISerializer serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .DisableAliases()
                .Build();

            string yaml = serializer.Serialize(workflow);

            string targetDirPath = Path.Combine(file.Directory.FullName, ".github", "workflows");

            if (!Directory.Exists(targetDirPath))
            {
                Directory.CreateDirectory(targetDirPath);
            }

            File.WriteAllText(Path.Combine(targetDirPath, "Test-Jobs.yml"), yaml);
        });

}
