namespace Transcriptor.Transcription;

public interface IAudioNormalizer
{
    void Normalize(string sourcePath, string destinationPath);
}
