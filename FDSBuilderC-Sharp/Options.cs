using CommandLine;

public class Options
{
    [Option('i', "inputDiskImage", Required = true, HelpText = "Path to input FDS disk image (requires rom path for input)")]
    public string InputRomPath { get; set; }
    [Option('o', "outputDiskImage", Required = false, HelpText = "Path to output FDS disk image (requires rom path for input)")]
    public string OutputRomPath { get; set; }

    [Option('e', "extract", Required = false, HelpText = "Extract")]
    public bool Extract { get; set; }

    [Option('m', "merge", Required = false, HelpText = "Merge (requires directory path for the previously extracted FDS file")]
    public string Merge { get; set; }

    [Option('x', "expand", Required = false, HelpText = "Expand (requires directory path to output to)")]
    public string Expand { get; set; }

    [Option('e', "extractDirectory", Required = false, HelpText = "Location where extracted FDS files and JSON setting are.")]
    public string ExtractDirectory { get; set; }


}