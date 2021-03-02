using CommandLine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

class Program
{
    public class Options
    {
        [Option('i', "inputRom", Required = true, HelpText = "Path to input FDS rom (requires rom path for input)")]
        public string InputRomPath { get; set; }

        [Option('e', "extract", Required = false, HelpText = "Extract  (requires file info settings path for output)")]
        public string Extract { get; set; }

        [Option('m', "merge", Required = false, HelpText = "Merge")]
        public bool Merge { get; set; }

        [Option('x', "expand", Required = false, HelpText = "Expand (requires directory path to output to)")]
        public string Expand { get; set; }

        [Option('o', "outputRom", Required = false, HelpText = "Path to output FDS rom (requires rom path for output)")]
        public string OutputRomPath { get; set; }
    }

    static void Main(string[] args)
    {
        string InputRomPath = "";
        string OutputRomPath = "";

        string ExtractPath = "";
        string ExpandPath = "";

        bool Merge = false;
        bool Extract = false;
        bool Expand = false;

        Parser.Default.ParseArguments<Options>(args)
            .WithParsed<Options>(o =>
            {
                InputRomPath = o.InputRomPath;
                OutputRomPath = o.OutputRomPath;
                Merge = o.Merge;
                ExtractPath = o.Extract;
                ExpandPath = o.Expand;

                Extract = o.Extract != "" && o.Extract != null;
                Expand = o.Expand != "" && o.Expand != null;

                if (Expand)
                {
                    Merge = Extract = true;
                }
            })
            .WithNotParsed<Options>(o =>
            {
                Console.WriteLine("FDSBuilder: C# Edition: A tool to extract files from an .FDS rom and merge files back into an FDS rom.");
                Console.WriteLine("Author: FCandChill");
                System.Environment.Exit(1);
            });

        Global.ROM = File.ReadAllBytes(InputRomPath);

        //Check is header exists and remove it if it does
        byte[] HeaderCheck = new byte[3];
        Array.Copy(Global.ROM, 0, HeaderCheck, 0, HeaderCheck.Length);
        if (Encoding.ASCII.GetString(HeaderCheck) == "FDS")
        {
            byte[] UnhearedRom = new byte[Global.ROM.Length - Constants.HEADERSIZE];
            Array.Copy(Global.ROM, Constants.HEADERSIZE, UnhearedRom, 0, UnhearedRom.Length);
            Global.ROM = UnhearedRom;
        }

        Global.DiskSideCount = Global.ROM.Length / Constants.DISKSIDESIZE;
        if (Global.ROM.Length % Constants.DISKSIDESIZE != 0)
        {
            throw new ArgumentException("Rom size wrong.");
        }

        List<FileHeader>[] Header = null;
        List<Expand>[] ExpansionSettings = null;
        List<byte[]>[] FileBody = null;

        if (Extract)
        {
            ReadFDSRom(out Header, out FileBody);

            if (!Expand)
            {
                string Json = JsonConvert.SerializeObject(Header, Formatting.Indented);
                File.WriteAllText(Path.Combine(ExtractPath, Constants.FILEINFOFILENAME), Json);

                for (int i = 0; i < FileBody.Length; i++)
                {
                    string DiskSideChar = GetDiskSideChar(i);

                    Directory.CreateDirectory(Path.Combine(ExtractPath, DiskSideChar));

                    int j = 0;
                    foreach (byte[] FileData in FileBody[i])
                    {
                        File.WriteAllBytes(Path.Combine(Path.Combine(ExtractPath, DiskSideChar), $"{j:00}.dk{DiskSideChar}"), FileBody[i][j]);
                        j++;
                    }
                }
            }
        }

        if (Expand)
        {
            string Json = Encoding.ASCII.GetString(File.ReadAllBytes(ExpandPath));
            ExpansionSettings = JsonConvert.DeserializeObject<List<Expand>[]>(Json);

            ExpandFiles(ExpansionSettings, ref Header, ref FileBody);
        }

        if (Merge)
        {
            WriteFDSROM(InputRomPath, Header, FileBody, OutputRomPath, ExtractPath);
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

    private static void WriteFDSROM(string Path, List<FileHeader>[] Header, List<byte[]>[] FileBody, string OutputRomPath, string ExtractPath)
    {
        if (FileBody == null)
        {
            //load filebody data
            FileBody = new List<byte[]>[Global.DiskSideCount];
            for (int i = 0; i < Global.DiskSideCount; i++)
            {
                FileBody[i] = new List<byte[]>();
                string DiskSideChar = GetDiskSideChar(i);

                DirectoryInfo d = new DirectoryInfo(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), DiskSideChar));
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
            string Json = Encoding.ASCII.GetString(File.ReadAllBytes(ExtractPath));
            Header = JsonConvert.DeserializeObject<List<FileHeader>[]>(Json);
        }

        //Write header and file info to rom

        byte[] emptySpace = new byte[Constants.DISKSIDESIZE - Constants.DISKHEADERSIZE];

        for (int i = 0; i < Global.DiskSideCount; i++)
        {
            int pc = Constants.DISKHEADERSIZE + Constants.DISKSIDESIZE * i;

            //Blank out the enrtire disk side, except for header info
            Array.Copy(emptySpace, 0, Global.ROM, pc, emptySpace.Length);

            int j = 0;
            foreach (FileHeader h in Header[i])
            {
                byte[] headerBytes = h.GenerateHeader();
                Array.Copy(headerBytes, 0, Global.ROM, pc, headerBytes.Length);
                pc += headerBytes.Length;

                byte[] fileBodyBytes = FileBody[i][j];
                Array.Copy(fileBodyBytes, 0, Global.ROM, pc, fileBodyBytes.Length);
                pc += fileBodyBytes.Length;
                j++;
            }
        }

        File.WriteAllBytes(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), OutputRomPath), Global.ROM);
    }

    private static void ReadFDSRom(out List<FileHeader>[] Header, out List<byte[]>[] FileBody)
    {
        Header = new List<FileHeader>[Global.DiskSideCount];
        FileBody = new List<byte[]>[Global.DiskSideCount];

        for (int i = 0; i < Global.DiskSideCount; i++)
        {
            Header[i] = new List<FileHeader>();
            FileBody[i] = new List<byte[]>();
            int pc = Constants.DISKHEADERSIZE + Constants.DISKSIDESIZE * i;

            bool NotFileHeader = false;
            while (!NotFileHeader)
            {
                FileHeader h = new FileHeader();
                h.ReadHeader(pc, out NotFileHeader);

                if (!NotFileHeader)
                {
                    Header[i].Add(h);
                    pc += Constants.FILEHEADERSIZE + h.FileSize;

                    //Read filebody
                    byte[] fileBodyData = new byte[h.FileSize];
                    Array.Copy(Global.ROM, h.PCAddressStart + Constants.FILEHEADERSIZE, fileBodyData, 0, fileBodyData.Length);
                    FileBody[i].Add(fileBodyData);
                }
            }
        }
    }

    private static string GetDiskSideChar(int SideNo) => new string(new char[] { (char)(Convert.ToUInt16('a') + SideNo) });
}