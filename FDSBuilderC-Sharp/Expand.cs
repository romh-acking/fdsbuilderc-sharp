using Newtonsoft.Json;

public struct Expand
{
    [JsonProperty(Required = Required.Always)]
    public int FileNumber { get; set; }
    [JsonConverter(typeof(HexStringToUshortJsonConverter)), JsonProperty(Required = Required.Always)]
    public int BytesToAdd { get; set; }
}