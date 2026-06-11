using Synentra.Commands;

var rootCommand = SynentraCommandLine.Create(args);
return await rootCommand.Parse(args).InvokeAsync();