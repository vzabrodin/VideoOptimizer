using CliFx;
using VideoOptimizer.Commands;

await new CommandLineApplicationBuilder()
    .AddCommand(OptimizeCommand.Descriptor)
    .AddCommand(AnalyzeCommand.Descriptor)
    .Build()
    .RunAsync(args);
