namespace Castellan.Worker.Models;

public record LogEvent(
    DateTimeOffset Time,
    string Host,
    string Channel,
    int EventId,
    string Level,
    string User,
    string Message,
    string RawJson = "",
    string UniqueId = ""
);

