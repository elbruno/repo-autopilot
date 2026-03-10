using System.Text.Json;
using System.Text.Json.Serialization;
using LocalRepoAuto.Core.Models;
using Microsoft.Extensions.Logging;

namespace LocalRepoAuto.Core.Logging;

/// <summary>
/// Implementation of audit logger.
/// Writes immutable append-only JSONL (JSON Lines) audit files.
/// Each line is a complete JSON object (one entry).
/// </summary>
public class AuditLogger : IAuditLogger
{
    private readonly string _logsDirectory;
    private readonly ILogger<AuditLogger> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AuditLogger(
        string? logsDirectory = null,
        ILogger<AuditLogger>? logger = null)
    {
        _logsDirectory = logsDirectory ?? Path.Combine(".localrepoauto", "logs");
        _logger = logger ?? new NullLogger();

        // Ensure logs directory exists
        Directory.CreateDirectory(_logsDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    /// <summary>
    /// Log parsed intent at workflow start.
    /// </summary>
    public async Task LogIntentAsync(Intent intent)
    {
        var entry = new AuditLogEntry
        {
            WorkflowId = "initial", // Will be set by orchestrator
            Actor = "IntentRouter",
            Action = "ParseIntent",
            Status = "Completed",
            Description = intent.Summary,
            Parameters = new() { { "userInput", intent.RawInput } },
            Results = new()
            {
                { "intentType", intent.Type.ToString() },
                { "confidence", intent.ConfidenceLevel }
            }
        };

        // Copy parameters to results
        foreach (var param in intent.Parameters)
        {
            entry.Results[$"param_{param.Key}"] = param.Value;
        }

        await LogActionAsync(entry);
    }

    /// <summary>
    /// Log an agent decision or analysis.
    /// </summary>
    public async Task LogDecisionAsync(AuditLogEntry decision)
    {
        if (string.IsNullOrWhiteSpace(decision.Actor))
        {
            decision.Actor = "Unknown";
        }

        decision.Status ??= "Completed";
        decision.Timestamp = DateTime.UtcNow;

        await AppendEntryAsync(decision);
    }

    /// <summary>
    /// Log a Git action or operation.
    /// </summary>
    public async Task LogActionAsync(AuditLogEntry action)
    {
        if (string.IsNullOrWhiteSpace(action.Actor))
        {
            action.Actor = "Unknown";
        }

        action.Status ??= "Completed";
        action.Timestamp = DateTime.UtcNow;

        await AppendEntryAsync(action);
    }

    /// <summary>
    /// Log workflow outcome.
    /// </summary>
    public async Task LogOutcomeAsync(AuditLogEntry outcome)
    {
        outcome.Actor = "WorkflowOrchestrator";
        outcome.Action = "WorkflowCompleted";
        outcome.Status = "Completed";
        outcome.Timestamp = DateTime.UtcNow;

        await AppendEntryAsync(outcome);
    }

    /// <summary>
    /// Get complete audit trail for a workflow.
    /// </summary>
    public async Task<List<AuditLogEntry>> GetAuditTrailAsync(string workflowId)
    {
        var filePath = GetAuditFilePath(workflowId);

        if (!File.Exists(filePath))
        {
            _logger?.LogWarning("Audit file not found: {FilePath}", filePath);
            return new();
        }

        var entries = new List<AuditLogEntry>();

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var entry = JsonSerializer.Deserialize<AuditLogEntry>(line, _jsonOptions);
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }
                catch (JsonException ex)
                {
                    _logger?.LogWarning("Failed to deserialize audit entry: {Error}", ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to read audit trail: {FilePath}", filePath);
        }

        return entries;
    }

    /// <summary>
    /// Export audit trail in specified format.
    /// </summary>
    public async Task<string> ExportAuditTrailAsync(string workflowId, string format = "jsonl")
    {
        var entries = await GetAuditTrailAsync(workflowId);

        return format.ToLowerInvariant() switch
        {
            "json" => ExportAsJson(entries),
            "csv" => ExportAsCsv(entries),
            _ => ExportAsJsonL(entries)
        };
    }

    /// <summary>
    /// Get audit file path for a workflow.
    /// </summary>
    public string GetAuditFilePath(string workflowId)
    {
        return Path.Combine(_logsDirectory, $"audit-{workflowId}.jsonl");
    }

    // Private helpers

    private async Task AppendEntryAsync(AuditLogEntry entry)
    {
        var filePath = GetAuditFilePath(entry.WorkflowId);

        try
        {
            var json = JsonSerializer.Serialize(entry, _jsonOptions);
            await File.AppendAllTextAsync(filePath, json + Environment.NewLine);
            _logger?.LogDebug("Audit entry logged: {Action} by {Actor}", entry.Action, entry.Actor);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to write audit entry");
        }
    }

    private static string ExportAsJson(List<AuditLogEntry> entries)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        return JsonSerializer.Serialize(entries, options);
    }

    private static string ExportAsJsonL(List<AuditLogEntry> entries)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };

        var lines = entries.Select(e => JsonSerializer.Serialize(e, options));
        return string.Join(Environment.NewLine, lines);
    }

    private static string ExportAsCsv(List<AuditLogEntry> entries)
    {
        var lines = new List<string>
        {
            "Timestamp,Actor,Action,Status,Description,Error"
        };

        foreach (var entry in entries)
        {
            var escapedDesc = EscapeCsv(entry.Description);
            var escapedError = EscapeCsv(entry.Error ?? "");
            lines.Add(
                $"{entry.Timestamp:O},{entry.Actor},{entry.Action},{entry.Status},\"{escapedDesc}\",\"{escapedError}\""
            );
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string EscapeCsv(string value)
    {
        return value.Replace("\"", "\"\"");
    }

    // Null logger for when no logger is provided
    private class NullLogger : ILogger<AuditLogger>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
