using System.Text.Json.Nodes;
using CliWrap;
using CliWrap.Buffered;
using ICSharpCode.SharpZipLib.Zip;

public static class PowershellSetup
{
    public static async Task Setup()
    {
        await InstallPowershell();
        await InstallOhMyPosh();
        await InstallWindowsTerminal();
        await InstallTerminalIcons();

        await InstallCascadiaNerdFonts();
        await ApplyCascadiaNerdToWindowsTerminal();

        await CopyPowershellProfile();
    }

    private static async Task ApplyCascadiaNerdToWindowsTerminal()
    {
        Console.WriteLine("Applying Cascade font to windows terminal");

        var userProfileDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var settingsPath = $"{userProfileDirectory}\\AppData\\Local\\Packages\\Microsoft.WindowsTerminal_8wekyb3d8bbwe\\LocalState\\settings.json";

        var settingsJson = await File.ReadAllTextAsync(settingsPath);
        var newJsonNode = JsonNode.Parse(settingsJson, documentOptions: new System.Text.Json.JsonDocumentOptions
        {
            CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        });
        var oldJson = newJsonNode.ToString();

        var profiles = newJsonNode["profiles"]["list"] as JsonArray;
        
        foreach (var profile in profiles)
        {
            if(profile["source"]?.GetValue<string>() == "Windows.Terminal.PowershellCore")
            {
                profile["font"] = JsonNode.Parse("{\"face\": \"CaskaydiaCove NF\"}");
            }
        }

        var newSettingsJson = newJsonNode.ToString();

        if(newSettingsJson.Equals(oldJson))
        {
            return;
        }

        File.Copy(settingsPath, Path.Combine(Path.GetDirectoryName(settingsPath), $"settings_backup_{Guid.NewGuid()}.json"));
        await File.WriteAllTextAsync(settingsPath, newSettingsJson);
    }

    private static async Task InstallTerminalIcons()
    {
        Console.WriteLine("Installing Terminal icons...");

        await Cli.Wrap("pwsh")
            .WithArguments(new[] {"-noprofile", "-c", "Install-Module -Name Terminal-Icons -Repository PSGallery -Force -AcceptLicense"})
            .WithStandardErrorPipe(PipeTarget.ToDelegate(Console.WriteLine))
            .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
            .ExecuteAsync();
    }

    private static async Task InstallCascadiaNerdFonts()
    {
        Console.WriteLine("Downloading fonts...");

        var fontDownloadUrl = "https://github.com/ryanoasis/nerd-fonts/releases/download/v2.1.0/CascadiaCode.zip?WT.mc_id=-blog-scottha";
        var fontArchiveName = "fonts.zip";

        var tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDirectory);
        Console.WriteLine($"Unzipping fonts to {tempDirectory}...");

        try
        {
            await DownloadToDirectory(fontDownloadUrl, tempDirectory, fontArchiveName);

            var extractionTargetFolder = Path.Combine(tempDirectory, "extracted");
            await UnzipFile(tempDirectory, extractionTargetFolder, fontArchiveName);

            await InstallFonts(extractionTargetFolder);
        }
        finally
        {
            Console.WriteLine($"Deleting temp directory {tempDirectory}...");
            Directory.Delete(tempDirectory, recursive: true);   
        }
    }

    private static async Task InstallFonts(string folderWithFonts)
    {
        foreach(var fontFile in new DirectoryInfo(folderWithFonts).EnumerateFiles())
        {
            if (fontFile.Extension != ".ttf")
            {
                continue;
            }

            var pwshScript = $@"
                $fileName = ""{fontFile.FullName}""
                $objShell = New-Object -ComObject Shell.Application
                $objFolder = $objShell.namespace(0x14)

                $fontPath = ""$env:windir\Fonts\""
                $fontName = $($objFolder.getDetailsOf($fileName, 21))

                if (!(Test-Path $fontPath\$fontName))
                {{
                    $objFolder.CopyHere($fileName,0x14)
                }}

                ";

            await Cli.Wrap("pwsh")
                .WithArguments(new[] {"-noprofile", "-c", pwshScript})
                .WithStandardErrorPipe(PipeTarget.ToDelegate(Console.WriteLine))
                .WithStandardOutputPipe(PipeTarget.ToDelegate(Console.WriteLine))
                .ExecuteAsync();
            
        }
    }

    private static async Task UnzipFile(string directory, string extractionTargetFolder, string fileName)
    {
        await using var zipFile = File.Open(Path.Combine(directory, fileName), FileMode.Open);
        await using var zip = new ZipInputStream(zipFile);

        ZipEntry theEntry;
        while ((theEntry = zip.GetNextEntry()) != null)
        {
            var innerDirectoryName = Path.Combine(extractionTargetFolder, Path.GetDirectoryName(theEntry.Name));
            var innerFileName = Path.GetFileName(theEntry.Name);

            // create directory
            if (innerDirectoryName.Length > 0)
            {
                Directory.CreateDirectory(innerDirectoryName);
            }

            if (innerFileName != string.Empty)
            {
                await using var streamWriter = File.Create(Path.Combine(innerDirectoryName, theEntry.Name));
                await zip.CopyToAsync(streamWriter);
            }
        }
    }

    private static HttpClient _client = new();
    private static async Task DownloadToDirectory(string downloadUrl, string downloadTargetPath, string fileName)
    {
        await using var stream = await _client.GetStreamAsync(downloadUrl);
        
        await using var file = File.Create(Path.Combine(downloadTargetPath, fileName));

        await stream.CopyToAsync(file);
    }

    private static async Task InstallWindowsTerminal()
    {
        const string packageId = "terminal";

        Console.WriteLine("Checking if Windows Terminal is installed...");

        var isInstalled = await Winget.IsInstalled(packageId, Console.WriteLine, Console.WriteLine);

        if (isInstalled)
        {
            Console.WriteLine("Windows Terminal is already installed, skipping...");
            return;
        }

        Console.WriteLine("Installing Windows Terminal...");

        await Winget.Install(packageId, Console.WriteLine, Console.WriteLine);
    }

    private static async Task InstallPowershell()
    {
        const string packageId = "Microsoft.PowerShell";

        Console.WriteLine("Checking if Powershell is installed...");

        var isInstalled = await Winget.IsInstalled(packageId, Console.WriteLine, Console.WriteLine);

        if (isInstalled)
        {
            Console.WriteLine("Powershell is already installed, skipping...");
            return;
        }

        Console.WriteLine("Installing powershell...");

        await Winget.Install(packageId, Console.WriteLine, Console.WriteLine);
    }

    private static async Task CopyPowershellProfile()
    {
        Console.WriteLine($"Getting profile directory...");
        var result = await Cli.Wrap("pwsh")
            .WithArguments(new[] {"-noprofile", "-c", "echo $PROFILE"})
            .WithStandardErrorPipe(PipeTarget.ToDelegate((s) => Console.WriteLine(s)))
            .WithStandardOutputPipe(PipeTarget.ToDelegate((s) => Console.WriteLine(s)))
            .ExecuteBufferedAsync();

        var profilePath = result.StandardOutput.Trim();        
        Console.WriteLine($"$pwsh -c 'echo $Profile': {profilePath}");

        var targetDir = Path.GetDirectoryName(profilePath);

        var sourceDir = "./Powershell";
        
        Console.WriteLine("Copying files to pwsh profile...");

        foreach(var file in new DirectoryInfo(sourceDir).EnumerateFiles())
        {
            var fileNameWithExtension = file.Name;

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var copyTarget = Path.Combine(targetDir, fileNameWithExtension);
            file.CopyTo(copyTarget, true);

            Console.WriteLine($"Copying {file.FullName} to {copyTarget}");
        }
    }

    private static async Task InstallOhMyPosh()
    {
        var packageId = "JanDeDobbeleer.OhMyPosh";

        Console.WriteLine("Checking if oh my posh is installed...");

        var isInstalled = await Winget.IsInstalled(packageId, Console.WriteLine, Console.WriteLine);

        if (isInstalled)
        {
            Console.WriteLine("oh my posh is already installed, skipping...");
            return;
        }

        Console.WriteLine("Installing oh my posh...");

        await Winget.Install(packageId, Console.WriteLine, Console.WriteLine);
    }
}