using System.Text.Json;
using System.Text.Json.Serialization;
using LocalRepoAuto.Core.Models;
using Microsoft.Extensions.Logging;

namespace LocalRepoAuto.Core.State;

/// <summary>
/// Implementation of state manager.
/// Persists workflow checkpoints to JSON files for resumable workflows.
/// </summary>
public class StateManager : IStateManager
{
    private readonly string _checkpointDirectory;
    private readonly ILogger<StateManager> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public StateManager(
        string? checkpointDirectory = null,
        ILogger<StateManager>? logger = null)
    {
        _checkpointDirectory = checkpointDirectory ?? Path.Combine(".localrepoauto", "state", "checkpoints");
        _logger = logger ?? new NullLogger();

        // Ensure checkpoint directory exists
        Directory.CreateDirectory(_checkpointDirectory);

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    /// <summary>
    /// Save a checkpoint.
    /// </summary>
    public async Task<string> SaveCheckpointAsync(WorkflowCheckpoint checkpoint)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(checkpoint.Id))
            {
                checkpoint.Id = GenerateCheckpointId(checkpoint.WorkflowId, checkpoint.Label);
            }

            checkpoint.SavedAt = DateTime.UtcNow;

            var filePath = GetCheckpointFilePath(checkpoint.Id);
            var json = JsonSerializer.Serialize(checkpoint, _jsonOptions);

            await File.WriteAllTextAsync(filePath, json);
            _logger?.LogInformation(
                "Checkpoint saved: {CheckpointId} ({Label})", checkpoint.Id, checkpoint.Label);

            return checkpoint.Id;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save checkpoint");
            throw;
        }
    }

    /// <summary>
    /// Load a checkpoint by ID.
    /// </summary>
    public async Task<WorkflowCheckpoint?> LoadCheckpointAsync(string checkpointId)
    {
        try
        {
            var filePath = GetCheckpointFilePath(checkpointId);

            if (!File.Exists(filePath))
            {
                _logger?.LogWarning("Checkpoint not found: {CheckpointId}", checkpointId);
                return null;
            }

            var json = await File.ReadAllTextAsync(filePath);
            var checkpoint = JsonSerializer.Deserialize<WorkflowCheckpoint>(json, _jsonOptions);

            _logger?.LogInformation("Checkpoint loaded: {CheckpointId}", checkpointId);
            return checkpoint;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to load checkpoint: {CheckpointId}", checkpointId);
            return null;
        }
    }

    /// <summary>
    /// List all checkpoints for a workflow.
    /// </summary>
    public async Task<List<WorkflowCheckpoint>> ListCheckpointsAsync(string workflowId)
    {
        try
        {
            var files = Directory.GetFiles(_checkpointDirectory, $"{workflowId}-*.json");
            var checkpoints = new List<WorkflowCheckpoint>();

            foreach (var file in files.OrderByDescending(f => new FileInfo(f).CreationTime))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var checkpoint = JsonSerializer.Deserialize<WorkflowCheckpoint>(json, _jsonOptions);
                    if (checkpoint != null)
                    {
                        checkpoints.Add(checkpoint);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to deserialize checkpoint: {File}", file);
                }
            }

            _logger?.LogInformation(
                "Listed {CheckpointCount} checkpoints for workflow {WorkflowId}",
                checkpoints.Count, workflowId);

            return checkpoints;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list checkpoints for workflow: {WorkflowId}", workflowId);
            return new();
        }
    }

    /// <summary>
    /// Delete a checkpoint.
    /// </summary>
    public async Task DeleteCheckpointAsync(string checkpointId)
    {
        try
        {
            var filePath = GetCheckpointFilePath(checkpointId);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger?.LogInformation("Checkpoint deleted: {CheckpointId}", checkpointId);
            }
            else
            {
                _logger?.LogWarning("Checkpoint not found for deletion: {CheckpointId}", checkpointId);
            }

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete checkpoint: {CheckpointId}", checkpointId);
            throw;
        }
    }

    /// <summary>
    /// Get checkpoint directory.
    /// </summary>
    public string GetCheckpointDirectory()
    {
        return _checkpointDirectory;
    }

    /// <summary>
    /// Get file path for a checkpoint.
    /// </summary>
    public string GetCheckpointFilePath(string checkpointId)
    {
        return Path.Combine(_checkpointDirectory, $"{checkpointId}.json");
    }

    // Helper methods

    private static string GenerateCheckpointId(string workflowId, string label)
    {
        return $"{workflowId}-{label.ToLowerInvariant().Replace(" ", "-")}";
    }

    // Null logger for when no logger is provided
    private class NullLogger : ILogger<StateManager>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
