using CliWrap;
using CliWrap.Buffered;
using Spectre.Console;

var setupChoice = AnsiConsole.Prompt(
    new SelectionPrompt<string>()
        .Title("What do you want to setup?")
        .AddChoices(new[] 
        {
            "Powershell"
        }));

if(setupChoice is "Powershell")
{
    await PowershellSetup.Setup();
}