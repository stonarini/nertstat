using System.Xml.Serialization;
using System.Text.Json.Serialization;

public record SteamConfig(string User, string Pass, string GuardFile);

[XmlRoot("Lobbies")]
public record LobbyInfo(string Name, int CurrentPlayers, int MaxPlayers, string Region, [property: JsonIgnore, XmlIgnore] string SteamId)
{
    public LobbyInfo() : this(string.Empty, 0, 0, string.Empty, string.Empty) { }
}

public static class XmlHelper {
    public static string ToXml(object obj) {
        var stringWriter = new StringWriter();
        var serializer = new XmlSerializer(obj.GetType());
        serializer.Serialize(stringWriter, obj);
        return stringWriter.ToString();
    }
}

public static class Global { public static List<string[]> State = new(); }
