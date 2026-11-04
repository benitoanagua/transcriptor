namespace Transcriptor.Transcription;

/// <summary>Creates unique temporary paths within a caller-selected directory.</summary>
public interface ITemporaryFileProvider
{
    string CreatePath(string directory, string purpose, string extension);
}

/// <summary>Default temporary path provider using the canonical session layout rules.</summary>
public sealed class GuidTemporaryFileProvider : ITemporaryFileProvider
{
    public static GuidTemporaryFileProvider Instance { get; } = new();

    private GuidTemporaryFileProvider() { }

    public string CreatePath(string directory, string purpose, string extension) =>
        new SessionLayout(directory).Temporary(purpose, extension);
}
