using CommandLine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public class Program
{
    // Path to rom used to extract files
    // Also used to extract FDS header info when creating a new FDS rom
    public static string InputDiskImagePath = "";

    // Path to the output FDS disk image
    // Used when using the "merge" parameter
    public static string OutputDiskImagePath = "";

    public static string DiskDirectory = "";

    // Path for the expansion settings
    public static string ExpansionSettingsPath = "";

    public static void Main(string[] args)
    {
        bool Merge = false;
        bool Extract = false;
        bool Expand = false;

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed<Options>(o =>
            {
                InputDiskImagePath = o.InputDiskImagePath;
                OutputDiskImagePath = o.OutputDiskImage;
                DiskDirectory = o.DiskDirectory;
                ExpansionSettingsPath = o.ExpansionSettingsPath;

                Extract = o.Extract;
                Expand = o.Expand;
                Merge = o.Merge;
            })
            .WithNotParsed<Options>(o =>
            {
                Console.WriteLine("A tool to extract files from an .FDS rom and merge files back into an FDS rom.");
                Console.WriteLine("Author: FCandChill");
                System.Environment.Exit(1);
            });

        // Parameter legitimacy checks

        if ((Merge) && string.IsNullOrEmpty(OutputDiskImagePath))
        {
            Console.WriteLine("Merge parameter require an output diskimage filepath");
            System.Environment.Exit(1);
        }

        if (Merge)
        {
            if (!(Directory.Exists(Path.GetDirectoryName(OutputDiskImagePath))))
            {
                Console.WriteLine("Output diskimage filepath is not valid.");
                System.Environment.Exit(1);
            }

            if (string.IsNullOrEmpty(DiskDirectory))
            {
                Console.WriteLine("DiskDirectory cannot be null or empty.");
                System.Environment.Exit(1);
            }

            if (!Directory.Exists(Path.GetDirectoryName(DiskDirectory)))
            {
                Console.WriteLine("Main DiskDirectory path is not valid.");
                System.Environment.Exit(1);
            }

            if (!Directory.Exists(Path.Combine(DiskDirectory, "a")))
            {
                Console.WriteLine("DiskDirectory path 'a' is not valid.");
                System.Environment.Exit(1);
            }

            if (!Directory.Exists(Path.Combine(DiskDirectory, "b")))
            {
                Console.WriteLine("DiskDirectory path 'b' is not valid.");
                System.Environment.Exit(1);
            }

            if (!(Merge || Extract))
            {
                Console.WriteLine("When the expand flag is sent, Merge and/or Extract must be set.");
                System.Environment.Exit(1);
            }
        }

        if (Extract)
        {
            if (!Directory.Exists(DiskDirectory))
            {
                Console.WriteLine("DiskDirectory is not valid.");
                System.Environment.Exit(1);
            }

            if (!string.IsNullOrEmpty(OutputDiskImagePath))
            {
                Console.WriteLine("Extract parameter doesn't require an output diskimage filepath");
                System.Environment.Exit(1);
            }
        }

        if (Expand)
        {
            if (!(File.Exists(ExpansionSettingsPath)))
            {
                Console.WriteLine("Expand filepath is not valid.");
                System.Environment.Exit(1);
            }

            if (Path.GetExtension(ExpansionSettingsPath) != ".json")
            {
                Console.WriteLine("Expand file extension must be json.");
                System.Environment.Exit(1);
            }
        }

        if (!File.Exists(InputDiskImagePath))
        {
            Console.WriteLine("InputDiskImagePath doesn't exist.");
            System.Environment.Exit(1);
        }

        // Load FDS disk image
        Global.FDSDiskImage = File.ReadAllBytes(InputDiskImagePath);

        //Check if header exists and remove it if it does
        byte[] HeaderCheck = new byte[3];
        Array.Copy(Global.FDSDiskImage, 0, HeaderCheck, 0, HeaderCheck.Length);
        if (Encoding.ASCII.GetString(HeaderCheck) == "FDS")
        {
            byte[] UnhearedRom = new byte[Global.FDSDiskImage.Length - Constants.HEADERSIZE];
            Array.Copy(Global.FDSDiskImage, Constants.HEADERSIZE, UnhearedRom, 0, UnhearedRom.Length);
            Global.FDSDiskImage = UnhearedRom;
        }

        // Get disk side count from filesize
        Global.DiskSideCount = Global.FDSDiskImage.Length / Constants.DISKSIDESIZE;
        if (Global.FDSDiskImage.Length % Constants.DISKSIDESIZE != 0)
        {
            throw new ArgumentException("Disk image size wrong.");
        }

        List<FileHeader>[] fileHeaders = null;
        List<byte[]>[] fileBodies = null;
        List<Expand>[] expansionSettings = null;

        // Extract files from FDS diskimage
        if (Extract)
        {
            ExtractFDSFilesFromDiskImage(out fileHeaders, out fileBodies);

            string Json = JsonConvert.SerializeObject(fileHeaders, Formatting.Indented);
            File.WriteAllText(Path.Combine(DiskDirectory, Constants.FILEINFOFILENAME), Json);

            for (int i = 0; i < fileBodies.Length; i++)
            {
                string DiskSideChar = GetDiskSideChar(i);

                Directory.CreateDirectory(Path.Combine(DiskDirectory, DiskSideChar));

                int j = 0;
                foreach (byte[] FileData in fileBodies[i])
                {
                    File.WriteAllBytes(Path.Combine(Path.Combine(DiskDirectory, DiskSideChar), $"{j:00}.dk{DiskSideChar}"), fileBodies[i][j]);
                    j++;
                }
            }
        }
        else if (Merge || Expand)
        {
            ReadFDSFilesFromDiskImage(out fileHeaders, out fileBodies);
        }
        else
        {
            throw new Exception("Neither merge, extract, or expand flags are set.");
        }

        // Append specified amount of zeros to the filebodies
        // The ExpansionSettings json file specifies which files to expand and by how much.
        // When this flag is enabled, the Write and merge flag are also enabled
        if (Expand)
        {
            string Json = Encoding.ASCII.GetString(File.ReadAllBytes(ExpansionSettingsPath));
            expansionSettings = JsonConvert.DeserializeObject<List<Expand>[]>(Json);

            ExpandFiles(expansionSettings, ref fileHeaders, ref fileBodies);
        }

        // Merge the files into a working FDS disk image
        if (Merge)
        {
            WriteFDSROM(InputDiskImagePath, fileHeaders, fileBodies);
        }
        else if (Expand)
        {
            string Json = JsonConvert.SerializeObject(fileHeaders, Formatting.Indented);
            File.WriteAllText(Path.Combine(DiskDirectory, Constants.FILEINFOFILENAME), Json);

            for (int i = 0; i < fileBodies.Length; i++)
            {
                string DiskSideChar = GetDiskSideChar(i);

                Directory.CreateDirectory(Path.Combine(DiskDirectory, DiskSideChar));

                int j = 0;
                foreach (byte[] FileData in fileBodies[i])
                {
                    File.WriteAllBytes(Path.Combine(Path.Combine(DiskDirectory, DiskSideChar), $"{j:00}.dk{DiskSideChar}"), fileBodies[i][j]);
                    j++;
                }
            }
        }
        
        Console.WriteLine("Completed successfully.");
    }

    private static void ExpandFiles(List<Expand>[] expansionSettings, ref List<FileHeader>[] fileHeaders, ref List<byte[]>[] fileBodies)
    {
        if (expansionSettings == null)
        {
            throw new Exception("ExpandFiles: expansionSettings cannot be null");
        }

        if (fileHeaders == null)
        {
            throw new Exception("ExpandFiles: header cannot be null");
        }

        if (fileBodies == null)
        {
            throw new Exception("ExpandFiles: fileBody cannot be null");
        }

        for (int i = 0; i < Global.DiskSideCount; i++)
        {
            int j = 0;
            if (i < expansionSettings.Length)
            {
                foreach (Expand e in expansionSettings[i])
                {
                    byte[] arr = fileBodies[i][e.FileNumber];
                    fileBodies[i][e.FileNumber] = arr.Concat(Enumerable.Repeat<Byte>(0xff, e.BytesToAdd)).ToArray();
                    fileHeaders[i][e.FileNumber].FileSize += (ushort)e.BytesToAdd;
                    j++;
                }
            }
        }
    }

    private static void WriteFDSROM(string RomPath, List<FileHeader>[] fileHeaders, List<byte[]>[] fileBodies)
    {
        //Write header and file info to rom
        byte[] emptySpace = new byte[Constants.DISKSIDESIZE - Constants.DISKHEADERSIZE];

        for (int i = 0; i < Global.DiskSideCount; i++)
        {
            int pc = Constants.DISKHEADERSIZE + Constants.DISKSIDESIZE * i;

            //Blank out the enrtire disk side, except for header info
            Array.Copy(emptySpace, 0, Global.FDSDiskImage, pc, emptySpace.Length);

            int j = 0;
            foreach (FileHeader h in fileHeaders[i])
            {
                byte[] headerBytes = h.GenerateHeader();
                Array.Copy(headerBytes, 0, Global.FDSDiskImage, pc, headerBytes.Length);
                pc += headerBytes.Length;

                byte[] fileBodyBytes = fileBodies[i][j];

                if (Constants.DISKSIDESIZE < fileBodyBytes.Length)
                {
                    throw new Exception($"File size for '{h.FileName}.{h.FileType}' is too big for an FDS disk image side.");
                }

                if (Global.FDSDiskImage.Length < fileBodyBytes.Length + pc)
                {
                    throw new Exception($"File size for '{h.FileName}.{h.FileType}' is too big for an FDS disk image.");
                }

                Array.Copy(fileBodyBytes, 0, Global.FDSDiskImage, pc, fileBodyBytes.Length);
                pc += fileBodyBytes.Length;
                j++;
            }
        }

        File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(RomPath), OutputDiskImagePath), Global.FDSDiskImage);
    }

    private static void ExtractFDSFilesFromDiskImage(out List<FileHeader>[] fileHeaders, out List<byte[]>[] fileBodies)
    {
        // Stores the file header info for each file on the diskimage
        fileHeaders = new List<FileHeader>[Global.DiskSideCount];

        // Stores the actual file data
        fileBodies = new List<byte[]>[Global.DiskSideCount];

        for (int i = 0; i < Global.DiskSideCount; i++)
        {
            fileHeaders[i] = new List<FileHeader>();
            fileBodies[i] = new List<byte[]>();
            int pc = Constants.DISKHEADERSIZE + Constants.DISKSIDESIZE * i;

            bool NotFileHeader = false;
            while (!NotFileHeader)
            {
                FileHeader h = new FileHeader();
                h.ReadHeader(pc, out NotFileHeader);

                if (!NotFileHeader)
                {
                    fileHeaders[i].Add(h);
                    pc += Constants.FILEHEADERSIZE + h.FileSize;

                    //Read filebody
                    byte[] fileBodyData = new byte[h.FileSize];
                    Array.Copy(Global.FDSDiskImage, h.PCAddressStart + Constants.FILEHEADERSIZE, fileBodyData, 0, fileBodyData.Length);
                    fileBodies[i].Add(fileBodyData);
                }
            }
        }
    }

    private static void ReadFDSFilesFromDiskImage(out List<FileHeader>[] fileHeaders, out List<byte[]>[] fileBodies)
    {
        // Load header info
        string fileInfoPath = Path.Combine(DiskDirectory, Constants.FILEINFOFILENAME);
        string json = Encoding.ASCII.GetString(File.ReadAllBytes(fileInfoPath));
        fileHeaders = JsonConvert.DeserializeObject<List<FileHeader>[]>(json);
        
        // Load file body data
        fileBodies = new List<byte[]>[Global.DiskSideCount];
        for (int i = 0; i < Global.DiskSideCount; i++)
        {
            fileBodies[i] = new List<byte[]>();
            string diskSideChar = GetDiskSideChar(i);

            DirectoryInfo d = new DirectoryInfo(System.IO.Path.Combine(DiskDirectory, diskSideChar));
            FileInfo[] files = d.GetFiles($"*.dk{ GetDiskSideChar(i)}");
            Array.Sort(files, (x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Name, y.Name));

            foreach (var file in files)
            {
                fileBodies[i].Add(File.ReadAllBytes(file.FullName));
            }
        }
    }

    private static string GetDiskSideChar(int SideNo) => new string(new char[] { (char)(Convert.ToUInt16('a') + SideNo) });
}
