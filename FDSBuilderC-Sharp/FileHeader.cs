using System;
using System.Linq;
using System.Text;

enum fileHeaderEnum
{
    firstByte = 0x0,
    fileNumber = 0x1,
    fileID = 0x2,
    fileName = 0x3,
    fileAddress = 0xb,
    fileSize = 0xd,
    fileType = 0xf,
    CRC = 0x10
}

public class FileHeader
{
    private const byte FILEHEADER = 0x3;

    public byte fileNumber { get; private set; }
    public byte fileID { get; private set; }
    public string fileName { get; private set; }
    public ushort fileAddress { get; private set; }
    public ushort fileSize { get; set; }
    public byte fileType { get; private set; }

    public int PCAddressStart;

    public void ReadHeader(int startAddress, out bool notFileHeader)
    {
        byte[] fileHeaderStream = new byte[Constants.FILEHEADERSIZE];
        Array.Copy(Global.ROM, startAddress, fileHeaderStream, 0, fileHeaderStream.Length);
        PCAddressStart = startAddress;

        notFileHeader = fileHeaderStream.All(x => x == 0);

        if (!notFileHeader)
        {
            if (fileHeaderStream[(int)fileHeaderEnum.firstByte] != FILEHEADER)
            {
                throw new Exception("File header wrong.");
            }

            fileNumber = fileHeaderStream[(int)fileHeaderEnum.fileNumber];
            fileID = fileHeaderStream[(int)fileHeaderEnum.fileID];

            byte[] b = new byte[8];
            Array.Copy(fileHeaderStream, (int)fileHeaderEnum.fileName, b, 0, b.Length);
            fileName = Encoding.ASCII.GetString(b);

            b = new byte[2];
            Array.Copy(fileHeaderStream, (int)fileHeaderEnum.fileAddress, b, 0, b.Length);
            fileAddress = BitConverter.ToUInt16(b, 0);

            Array.Copy(fileHeaderStream, (int)fileHeaderEnum.fileSize, b, 0, b.Length);
            fileSize = BitConverter.ToUInt16(b, 0);

            fileType = fileHeaderStream[(int)fileHeaderEnum.fileType];
        }
    }

    public byte[] GenerateHeader()
    {
        byte[] b = new byte[Constants.FILEHEADERSIZE];

        b[(int)fileHeaderEnum.firstByte] = 0x3;
        b[(int)fileHeaderEnum.fileNumber] = fileNumber;
        b[(int)fileHeaderEnum.fileID] = fileID;

        byte[] bytes = Encoding.ASCII.GetBytes(fileName);
        Array.Copy(bytes, 0, b, (int)fileHeaderEnum.fileName,  bytes.Length);

        bytes = BitConverter.GetBytes(fileAddress);
        Array.Copy(bytes, 0, b, (int)fileHeaderEnum.fileAddress, bytes.Length);

        bytes = BitConverter.GetBytes(fileSize);
        Array.Copy(bytes, 0, b, (int)fileHeaderEnum.fileSize, bytes.Length);

        b[(int)fileHeaderEnum.fileType] = fileType;

        b[(int)fileHeaderEnum.CRC] = 0x4;

        return b;
    }
}