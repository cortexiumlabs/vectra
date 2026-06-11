using Vectra.Commands;

var rootCommand = VectraCommandLine.Create(args);
return await rootCommand.Parse(args).InvokeAsync();