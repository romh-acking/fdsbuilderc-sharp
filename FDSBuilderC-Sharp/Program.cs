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

    // 
    public static string ExtractPath = "";

    //
    public static string ExpandPath = "";
    public static string MergePath = "";


    public static void Main(string[] args)
    {
        bool Merge = false;
        bool Extract = false;
        bool Expand = false;

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed<Options>(o =>
            {
                InputDiskImagePath = o.InputRomPath;
                OutputDiskImagePath = o.OutputRomPath;
                MergePath = o.Merge;
                Extract = o.Extract;
                ExtractPath = o.ExtractDirectory;
                ExpandPath = o.Expand;

                Expand = !string.IsNullOrEmpty(o.Expand);
                Merge = !string.IsNullOrEmpty(o.Merge);
            })
            .WithNotParsed<Options>(o =>
            {
                Console.WriteLine("A tool to extract files from an .FDS rom and merge files back into an FDS rom.");
                Console.WriteLine("Author: FCandChill");
                System.Environment.Exit(1);
            });

        // Parameter legitimacy checks

        if ((Merge || Expand) && string.IsNullOrEmpty(OutputDiskImagePath))
        {
            Console.WriteLine("Merge and expand parameters require an output diskimage filepath");
            System.Environment.Exit(1);
        }

        if (Extract && string.IsNullOrEmpty(OutputDiskImagePath))
        {
            Console.WriteLine("Extract parameter doesn't require an output diskimage filepath");
            System.Environment.Exit(1);
        }

        if (Merge && !(Directory.Exists(Path.GetDirectoryName(OutputDiskImagePath))))
        {
            Console.WriteLine("Output diskimage filepath is not valid.");
            System.Environment.Exit(1);
        }

        if (Merge && !(Directory.Exists(Path.GetDirectoryName(ExtractPath)) && Directory.Exists(Path.Combine(ExtractPath, "a")) && Directory.Exists(Path.Combine(ExtractPath, "b"))))
        {
            Console.WriteLine("Extract directory paths are not valid.");
            System.Environment.Exit(1);
        }

        if (Extract && !Directory.Exists(ExtractPath))
        {
            Console.WriteLine("Output extraction directory is not valid.");
            System.Environment.Exit(1);
        }

        if (Expand && !File.Exists(ExpandPath))
        {
            Console.WriteLine("Expansion directory is not valid.");
            System.Environment.Exit(1);
        }

        if (Expand && Path.GetExtension(ExpandPath) != ".json")
        {
            Console.WriteLine("Expansion directory is not valid.");
            System.Environment.Exit(1);
        }

        if (Extract && !(Directory.Exists(ExtractPath) && Directory.Exists(Path.Combine(ExtractPath, "a")) && Directory.Exists(Path.Combine(ExtractPath, "b"))))
        {
            Console.WriteLine("Expansion directory is not valid.");
            System.Environment.Exit(1);
        }

        if (Expand)
        {
            Merge = Extract = true;
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

        List<FileHeader>[] FileHeaders = null;
        List<byte[]>[] FileBodies = null;
        List<Expand>[] ExpansionSettings = null;

        // Extract files from FDS diskimage
        if (Extract)
        {
            ReadFDSFiles(out FileHeaders, out FileBodies);

            // If we're not expanding, write the files to the filesystem.
            // If we are expanding, don't. When expanding, the diskimage,
            // the output will be an expanded rom.
            if (!Expand)
            {
                string Json = JsonConvert.SerializeObject(FileHeaders, Formatting.Indented);
                File.WriteAllText(Path.Combine(ExtractPath, Constants.FILEINFOFILENAME), Json);

                for (int i = 0; i < FileBodies.Length; i++)
                {
                    string DiskSideChar = GetDiskSideChar(i);

                    Directory.CreateDirectory(Path.Combine(ExtractPath, DiskSideChar));

                    int j = 0;
                    foreach (byte[] FileData in FileBodies[i])
                    {
                        File.WriteAllBytes(Path.Combine(Path.Combine(ExtractPath, DiskSideChar), $"{j:00}.dk{DiskSideChar}"), FileBodies[i][j]);
                        j++;
                    }
                }
            }
        }

        // Append specified amount of zeros to the filebodies
        // The ExpansionSettings json file specifies which files to expand and by how much.
        // When this flag is enabled, the Write and merge flag are also enabled
        if (Expand)
        {
            string Json = Encoding.ASCII.GetString(File.ReadAllBytes(ExpandPath));
            ExpansionSettings = JsonConvert.DeserializeObject<List<Expand>[]>(Json);

            ExpandFiles(ExpansionSettings, ref FileHeaders, ref FileBodies);
        }

        // Merge the files into a working FDS disk image
        if (Merge)
        {
            WriteFDSROM(InputDiskImagePath, FileHeaders, FileBodies);
        }
    }

    private static void ExpandFiles(List<Expand>[] ExpansionSettings, ref List<FileHeader>[] Header, ref List<byte[]>[] FileBody)
    {
        for (int i = 0; i < Global.DiskSideCount; i++)
        {
            int j = 0;
            foreach (Expand e in ExpansionSettings[i])
            {
                byte[] arr = FileBody[i][e.FileNumber];
                FileBody[i][e.FileNumber] = arr.Concat(Enumerable.Repeat<Byte>(0xff, e.BytesToAdd)).ToArray();
                Header[i][e.FileNumber].FileSize += (ushort)e.BytesToAdd;
                j++;
            }
        }
    }

    private static void WriteFDSROM(string RomPath, List<FileHeader>[] Header, List<byte[]>[] FileBody)
    {
        // If MergePath exists, then FileBody is empty.
        // Therefore, we must read the FDS files from the hard drive before generating an FDS rom.
        if (!string.IsNullOrEmpty(MergePath))
        {
            //load filebody data
            FileBody = new List<byte[]>[Global.DiskSideCount];
            for (int i = 0; i < Global.DiskSideCount; i++)
            {
                FileBody[i] = new List<byte[]>();
                string DiskSideChar = GetDiskSideChar(i);

                DirectoryInfo d = new DirectoryInfo(System.IO.Path.Combine(MergePath, DiskSideChar));
                FileInfo[] files = d.GetFiles($"*.dk{ GetDiskSideChar(i)}");
                Array.Sort(files, (x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Name, y.Name));

                int j = 0;
                foreach (FileInfo file in files)
                {
                    byte[] fileByte = File.ReadAllBytes(file.FullName);
                    FileBody[i].Add(fileByte);
                    j++;
                }
            }
        }

        if (Header == null)
        {
            Header = new List<FileHeader>[Global.DiskSideCount];
            //Deserialize header info
            string SettingsFilePath = Path.Combine(ExtractPath, "fileInfo.json");
            string Json = Encoding.ASCII.GetString(File.ReadAllBytes(SettingsFilePath));
            Header = JsonConvert.DeserializeObject<List<FileHeader>[]>(Json);
        }

        //Write header and file info to rom
        byte[] emptySpace = new byte[Constants.DISKSIDESIZE - Constants.DISKHEADERSIZE];

        for (int i = 0; i < Global.DiskSideCount; i++)
        {
            int pc = Constants.DISKHEADERSIZE + Constants.DISKSIDESIZE * i;

            //Blank out the enrtire disk side, except for header info
            Array.Copy(emptySpace, 0, Global.FDSDiskImage, pc, emptySpace.Length);

            int j = 0;
            foreach (FileHeader h in Header[i])
            {
                byte[] headerBytes = h.GenerateHeader();
                Array.Copy(headerBytes, 0, Global.FDSDiskImage, pc, headerBytes.Length);
                pc += headerBytes.Length;

                byte[] fileBodyBytes = FileBody[i][j];

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

    private static void ReadFDSFiles(out List<FileHeader>[] FileHeader, out List<byte[]>[] FileBody)
    {
        // Stores the file header info for each file on the diskimage
        FileHeader = new List<FileHeader>[Global.DiskSideCount];

        // Stores the actual file data
        FileBody = new List<byte[]>[Global.DiskSideCount];

        for (int i = 0; i < Global.DiskSideCount; i++)
        {
            FileHeader[i] = new List<FileHeader>();
            FileBody[i] = new List<byte[]>();
            int pc = Constants.DISKHEADERSIZE + Constants.DISKSIDESIZE * i;

            bool NotFileHeader = false;
            while (!NotFileHeader)
            {
                FileHeader h = new FileHeader();
                h.ReadHeader(pc, out NotFileHeader);

                if (!NotFileHeader)
                {
                    FileHeader[i].Add(h);
                    pc += Constants.FILEHEADERSIZE + h.FileSize;

                    //Read filebody
                    byte[] fileBodyData = new byte[h.FileSize];
                    Array.Copy(Global.FDSDiskImage, h.PCAddressStart + Constants.FILEHEADERSIZE, fileBodyData, 0, fileBodyData.Length);
                    FileBody[i].Add(fileBodyData);
                }
            }
        }
    }

    private static string GetDiskSideChar(int SideNo) => new string(new char[] { (char)(Convert.ToUInt16('a') + SideNo) });
}
