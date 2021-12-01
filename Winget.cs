using System.Text;
using CliWrap;
using CliWrap.Buffered;

public static class Winget
{
    public static async Task<bool> IsInstalled(string packageId, Action<string> stdOut, Action<string> stdErr)
    {
        var outputBuilder = new StringBuilder();

        var resultExists = await Cli.Wrap("winget")
            .WithArguments(new[] {"list", "--id", packageId})
            .WithStandardOutputPipe(PipeTarget.Merge(new[] {PipeTarget.ToDelegate(stdOut), PipeTarget.ToStringBuilder(outputBuilder)}))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(stdErr))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        var output = outputBuilder.ToString();

        return !output.Contains("No installed package found matching input criteria.");
    }

    public static async Task Install(string packageId, Action<string> stdOut, Action<string> stdErr)
    {
        await Cli.Wrap("winget")
            .WithArguments(new[] {"install", packageId, "--silent", "--accept-package-agreements", "--accept-source-agreements"})
            .WithStandardErrorPipe(PipeTarget.ToDelegate(stdErr))
            .WithStandardOutputPipe(PipeTarget.ToDelegate(stdOut))
            .ExecuteAsync();
    }
}