
public static class RegionMapper {
    public static string GetRegion(string? rawData) {
        if (string.IsNullOrEmpty(rawData)) return "Unknown";

        var bestCode = rawData.Split(',')
            .Select(segment => segment.Split('='))
            .Where(parts => parts.Length == 2)
            .Select(parts => new {
                Code = new string(parts[0].TakeWhile(char.IsLetter).ToArray()),
                Ping = int.TryParse(new string(parts[1].TakeWhile(char.IsDigit).ToArray()), out int p) ? p : 999
            })
            .OrderBy(x => x.Ping)
            .FirstOrDefault()?.Code ?? "?";

        return bestCode.ToString();
    }
}
