using ComplexityRipper;

var rootCommand = CliConfiguration.CreateRootCommand();
return await rootCommand.Parse(args).InvokeAsync();
