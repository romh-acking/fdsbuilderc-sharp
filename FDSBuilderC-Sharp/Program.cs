using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CommandLine;

namespace FDSBuilderC_Sharp
{
    class Program
    {
        public class Options
        {
            [Option('i', "inputRom", Required = true, HelpText = "Path to input FDS rom (requires rom path for input)")]
            public string inputRomPath { get; set; }

            [Option('e', "extract", Required = false, HelpText = "Extract  (requires file info settings path for output)")]
            public string extract { get; set; }

            [Option('m', "merge", Required = false, HelpText = "Merge")]
            public bool merge { get; set; }

            [Option('x', "expand", Required = false, HelpText = "Expand (requires directory path to output to)")]
            public string expand { get; set; }

            [Option('o', "outputRom", Required = false, HelpText = "Path to output FDS rom (requires rom path for output)")]
            public string outputRomPath { get; set; }
        }

        static void Main(string[] args)
        {
            string inputRomPath = "";
            string outputRomPath = "";

            string extractPath = "";
            string expandPath = "";

            bool merge = false;
            bool extract = false;
            bool expand = false;

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed<Options>(o =>
                {
                    inputRomPath = o.inputRomPath;
                    outputRomPath = o.outputRomPath;
                    merge = o.merge;
                    extractPath = o.extract;
                    expandPath = o.expand;

                    extract = o.extract != "" && o.extract != null;
                    expand = o.expand != "" && o.expand != null;

                    if (expand)
                    {
                        merge = extract = true;
                    }
                })
                .WithNotParsed<Options>(o =>
                {
                    Console.WriteLine("FDSBuilder: C# Edition: A tool to extract files from an .FDS rom and merge files back into an FDS rom.");
                    Console.WriteLine("Author: FCandChill");
                    System.Environment.Exit(1);
                });

            Global.ROM = File.ReadAllBytes(inputRomPath);

            //Check is header exists and remove it if it does
            byte[] headerCheck = new byte[3];
            Array.Copy(Global.ROM, 0, headerCheck, 0, headerCheck.Length);
            if (Encoding.ASCII.GetString(headerCheck) == "FDS")
            {
                byte[] unhearedRom = new byte[Global.ROM.Length - Constants.HEADERSIZE];
                Array.Copy(Global.ROM, Constants.HEADERSIZE, unhearedRom, 0, unhearedRom.Length);
                Global.ROM = unhearedRom;
            }

            Global.diskSideCount = Global.ROM.Length / Constants.DISKSIDESIZE;
            if (Global.ROM.Length % Constants.DISKSIDESIZE != 0)
            {
                throw new ArgumentException("Rom size wrong.");
            }

            List<FileHeader>[] header = null;
            List<Expand>[] expansionSettings = null;
            List<byte[]>[] fileBody = null;

            if (extract)
            {
                ReadFDSRom(inputRomPath, out header, out fileBody);

                if (!expand)
                {
                    string json = JsonConvert.SerializeObject(header, Formatting.Indented);
                    File.WriteAllText(Path.Combine(extractPath, Constants.FILEINFOFILENAME), json);

                    for (int i = 0; i < fileBody.Length; i++)
                    {
                        string diskSideChar = getDiskSideChar(i);

                        Directory.CreateDirectory(Path.Combine(extractPath, diskSideChar));

                        int j = 0;
                        foreach (byte[] fileData in fileBody[i])
                        {
                            File.WriteAllBytes(Path.Combine(Path.Combine(extractPath, diskSideChar), $"{j:00}.dk{diskSideChar}"), fileBody[i][j]);
                            j++;
                        }
                    }
                }
            }

            if (expand)
            {
                string json = Encoding.ASCII.GetString(File.ReadAllBytes(expandPath));
                expansionSettings = JsonConvert.DeserializeObject<List<Expand>[]>(json);

                ExpandFiles(expansionSettings, ref header, ref fileBody);
            }

            if (merge)
            {
                WriteFDSROM(inputRomPath, header, fileBody, outputRomPath, extractPath);
            }
        }

        private static void ExpandFiles(List<Expand>[] expansionSettings, ref List<FileHeader>[] header, ref List<byte[]>[] fileBody)
        {
            for (int i = 0; i < Global.diskSideCount; i++)
            {
                int j = 0;
                foreach (Expand e in expansionSettings[i])
                {
                    byte[] arr = fileBody[i][e.fileNumber];
                    fileBody[i][e.fileNumber] = arr.Concat(Enumerable.Repeat<Byte>(0xff, e.bytesToAdd)).ToArray();
                    header[i][e.fileNumber].fileSize += (ushort)e.bytesToAdd;
                    j++;
                }
            }
        }

        private static void WriteFDSROM(string path, List<FileHeader>[] header, List<byte[]>[] fileBody, string outputRomPath, string extractPath)
        {
            //ushort[][] fileCRC16 = new ushort[Global.diskSideCount][];

            if (fileBody == null)
            {
                //load filebody data
                fileBody = new List<byte[]>[Global.diskSideCount];
                for (int i = 0; i < Global.diskSideCount; i++)
                {
                    fileBody[i] = new List<byte[]>();
                    string diskSideChar = getDiskSideChar(i);

                    DirectoryInfo d = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(path), diskSideChar));
                    FileInfo[] files = d.GetFiles($"*.dk{ getDiskSideChar(i)}");
                    Array.Sort(files, (x, y) => StringComparer.OrdinalIgnoreCase.Compare(x.Name, y.Name));

                    //fileCRC16[i] = new ushort[files.Length];

                    int j = 0;
                    foreach (FileInfo file in files)
                    {
                        byte[] fileByte = File.ReadAllBytes(file.FullName);
                        fileBody[i].Add(fileByte);
                        //fileCRC16[i][j] = CalculateCRC16(fileByte);
                        j++;
                    }
                }
            }

            if (header == null)
            {
                header = new List<FileHeader>[Global.diskSideCount];
                //Deserialize header info
                string json = Encoding.ASCII.GetString(File.ReadAllBytes(extractPath));
                header = JsonConvert.DeserializeObject<List<FileHeader>[]>(json);

                /*for (int i = 0; i < Global.diskSideCount; i++)
                {
                    int j = 0;
                    foreach(FileHeader h in header[i])
                    {
                        h.CRC = fileCRC16[i][j];
                        j++;
                    }
                }*/
            }

            //Write header and file info to rom

            byte[] emptySpace = new byte[Constants.DISKSIDESIZE - Constants.DISKHEADERSIZE];

            for (int i = 0; i < Global.diskSideCount; i++)
            {
                int pc = Constants.DISKHEADERSIZE + Constants.DISKSIDESIZE * i;

                //Blank out the enrtire disk side, except for header info
                Array.Copy(emptySpace, 0, Global.ROM, pc, emptySpace.Length);

                int j = 0;
                foreach (FileHeader h in header[i])
                {
                    byte[] headerBytes = h.GenerateHeader();
                    Array.Copy(headerBytes, 0, Global.ROM, pc, headerBytes.Length);
                    pc += headerBytes.Length;

                    byte[] fileBodyBytes = fileBody[i][j];
                    Array.Copy(fileBodyBytes, 0, Global.ROM, pc, fileBodyBytes.Length);
                    pc += fileBodyBytes.Length;
                    j++;
                }
            }

            File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(path), outputRomPath), Global.ROM);
        }

        private static void ReadFDSRom(string path, out List<FileHeader>[] header, out List<byte[]>[] fileBody)
        {
            header = new List<FileHeader>[Global.diskSideCount];
            fileBody = new List<byte[]>[Global.diskSideCount];

            for (int i = 0; i < Global.diskSideCount; i++)
            {
                header[i] = new List<FileHeader>();
                fileBody[i] = new List<byte[]>();
                int pc = Constants.DISKHEADERSIZE + Constants.DISKSIDESIZE * i;

                while (true)
                {
                    FileHeader h = new FileHeader();
                    h.ReadHeader(pc, out bool notFileHeader);

                    if (!notFileHeader)
                    {
                        header[i].Add(h);
                        pc += Constants.FILEHEADERSIZE + h.fileSize;

                        //Read filebody
                        byte[] fileBodyData = new byte[h.fileSize];
                        Array.Copy(Global.ROM, h.PCAddressStart + Constants.FILEHEADERSIZE, fileBodyData, 0, fileBodyData.Length);
                        fileBody[i].Add(fileBodyData);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private static string getDiskSideChar(int sideNo) => new string(new char[] { (char)(Convert.ToUInt16('a') + sideNo) });

        //Source: http://forums.nesdev.com/viewtopic.php?t=15895
        public static ushort CalculateCRC16(byte[] data)
        {
            //Do not include any existing checksum, not even the blank checksums 00 00 or FF FF.
            //The formula will automatically count 2 0x00 bytes without the programmer adding them manually.
            //Also, do not include the gap terminator (0x80) in the data.
            //If you wish to do so, change sum to 0x0000.
            int sum = 0x8000;

            for (int byte_index = 0; byte_index < data.Length + 2; byte_index++)
            {
                int b = byte_index < data.Length ? data[byte_index] : 0x00;
                for (int bit_index = 0; bit_index < 8; bit_index++)
                {
                    int bit = ((b >> bit_index) & 1);
                    int carry = sum & 1;
                    sum = (sum >> 1) | (bit << 15);
                    if (carry == 1)
                    {
                        sum ^= 0x8408;
                    }
                }
            }
            return (ushort)sum;
        }
    }
}