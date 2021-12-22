using CommandLine;

public class Options
{
    [Option('i', "inputDiskImage", Required = true, HelpText = "Path to input FDS disk image")]
    public string InputDiskImagePath { get; set; }
    [Option('o', "outputDiskImage", Required = false, HelpText = "Path to output FDS disk image (requires rom path for input)")]
    public string OutputDiskImage { get; set; }

    [Option('e', "extract", Required = false, HelpText = "Extract")]
    public bool Extract { get; set; }

    [Option('m', "merge", Required = false, HelpText = "Merge")]
    public bool Merge { get; set; }

    [Option('x', "expand", Required = false, HelpText = "Expand")]
    public bool Expand { get; set; }

    [Option('x', "expansionSettingsPath", Required = false)]
    public string ExpansionSettingsPath { get; set; }

    [Option('e', "diskDirectory", Required = false, HelpText = "Location where to read and write FDS files and where the JSON setting are.")]
    public string DiskDirectory { get; set; }


}