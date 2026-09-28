using NGPVP.Shared;
namespace NGPVP.Agent.Services;
public sealed record ScanResult(string Category, string Title, FindingSeverity Severity, string Details);
