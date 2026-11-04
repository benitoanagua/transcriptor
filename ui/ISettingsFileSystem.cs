using System.IO;

namespace Transcriptor.Desktop;

/// <summary>
/// File-system boundary for settings persistence. Implementations must publish
/// settings atomically so a failed write never replaces the previous file.
/// </summary>
public interface ISettingsFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    string ReadAllText(string path);
    void CreateDirectory(string path);
    void WriteAllTextAtomically(string path, string content);
    void Move(string source, string destination, bool overwrite);
}

/// <summary>Production implementation using same-directory temporary files.</summary>
public sealed class PhysicalSettingsFileSystem : ISettingsFileSystem
{
    public bool FileExists(string path) => File.Exists(path);
    public bool DirectoryExists(string path) => Directory.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void Move(string source, string destination, bool overwrite) =>
        File.Move(source, destination, overwrite);

    public void WriteAllTextAtomically(string path, string content)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(
                       temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                       4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(content);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            // Cleanup must not mask the original write/publication exception.
            try
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
