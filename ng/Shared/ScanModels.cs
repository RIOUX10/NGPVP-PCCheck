using System.Text.Json.Serialization;

namespace NGPVP.Shared;
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FindingSeverity { Clean, Info, Known, Suspicious, Detected }
public sealed record ScanResultDto(string Category, string Title, FindingSeverity Severity, string Details);
public sealed record SessionCreateResponse(string Pin, string AgentToken, int ExpiresInMinutes);
public sealed record SessionStatus(string Pin, bool Connected, bool Started, bool Completed, List<ScanResultDto> Results);
