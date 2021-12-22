using FDSBuilderC_Sharp;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Text;

public enum FileHeaderEnum
{
    FirstByte = 0x0,
    FileNumber = 0x1,
    FileID = 0x2,
    FileName = 0x3,
    FileAddress = 0xb,
    FileSize = 0xd,
    FileType = 0xf,
    CRC = 0x10
}

public class FileHeader
{
    private const byte FILEHEADER = 0x3;

    [JsonProperty(Required = Required.Always)]
    public byte FileNumber { get; set; }
    [JsonProperty(Required = Required.Always)]
    public byte FileID { get; set; }
    [JsonProperty(Required = Required.Always)]
    public string FileName { get; set; }
    [JsonConverter(typeof(HexStringToUshortJsonConverter)), JsonProperty(Required = Required.Always)]
    public ushort FileAddress { get; set; }
    [JsonConverter(typeof(HexStringToUshortJsonConverter)), JsonProperty(Required = Required.Always)]
    public ushort FileSize { get; set; }
    [JsonConverter(typeof(HexStringToByteJsonConverter)), JsonProperty(Required = Required.Always)]
    public byte FileType { get; set; }
    [JsonConverter(typeof(HexStringToLongJsonConverter)), JsonProperty(Required = Required.Always)]
    public long PCAddressStart { get; set; }

    public void ReadHeader(long StartAddress, out bool NotFileHeader)
    {
        byte[] FileHeaderStream = new byte[Constants.FILEHEADERSIZE];
        Array.Copy(Global.FDSDiskImage, StartAddress, FileHeaderStream, 0, FileHeaderStream.Length);
        PCAddressStart = StartAddress;

        NotFileHeader = FileHeaderStream.All(x => x == 0);

        if (!NotFileHeader)
        {
            if (FileHeaderStream[(int)FileHeaderEnum.FirstByte] != FILEHEADER)
            {
                throw new Exception("File header wrong.");
            }

            FileNumber = FileHeaderStream[(int)FileHeaderEnum.FileNumber];
            FileID = FileHeaderStream[(int)FileHeaderEnum.FileID];

            byte[] b = new byte[8];
            Array.Copy(FileHeaderStream, (int)FileHeaderEnum.FileName, b, 0, b.Length);
            FileName = Encoding.ASCII.GetString(b);

            b = new byte[2];
            Array.Copy(FileHeaderStream, (int)FileHeaderEnum.FileAddress, b, 0, b.Length);
            FileAddress = BitConverter.ToUInt16(b, 0);

            Array.Copy(FileHeaderStream, (int)FileHeaderEnum.FileSize, b, 0, b.Length);
            FileSize = BitConverter.ToUInt16(b, 0);

            FileType = FileHeaderStream[(int)FileHeaderEnum.FileType];
        }
    }

    public byte[] GenerateHeader()
    {
        byte[] b = new byte[Constants.FILEHEADERSIZE];

        b[(int)FileHeaderEnum.FirstByte] = 0x3;
        b[(int)FileHeaderEnum.FileNumber] = FileNumber;
        b[(int)FileHeaderEnum.FileID] = FileID;

        byte[] Bytes = Encoding.ASCII.GetBytes(FileName);
        Array.Copy(Bytes, 0, b, (int)FileHeaderEnum.FileName,  Bytes.Length);

        Bytes = BitConverter.GetBytes(FileAddress);
        Array.Copy(Bytes, 0, b, (int)FileHeaderEnum.FileAddress, Bytes.Length);

        Bytes = BitConverter.GetBytes(FileSize);
        Array.Copy(Bytes, 0, b, (int)FileHeaderEnum.FileSize, Bytes.Length);

        b[(int)FileHeaderEnum.FileType] = FileType;

        b[(int)FileHeaderEnum.CRC] = 0x4;

        return b;
    }
}

public sealed class HexStringToLongJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => typeof(long).Equals(objectType);
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => writer.WriteValue(MyMath.DecToHex((long)value, Prefix.X));
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) => (long)MyMath.HexToDec((string)reader.Value);
}

public sealed class HexStringToUshortJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => typeof(ushort).Equals(objectType);
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => writer.WriteValue(MyMath.DecToHex((ushort)value, Prefix.X));
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) => (ushort)MyMath.HexToDec((string)reader.Value);
}
public sealed class HexStringToByteJsonConverter : JsonConverter
{
    public override bool CanConvert(Type objectType) => typeof(byte).Equals(objectType);
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => writer.WriteValue(MyMath.DecToHex((byte)value, Prefix.X));
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer) => (byte)MyMath.HexToDec((string)reader.Value);
}