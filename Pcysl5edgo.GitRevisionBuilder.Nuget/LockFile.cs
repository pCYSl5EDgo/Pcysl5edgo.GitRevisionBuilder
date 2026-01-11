namespace Pcysl5edgo.GitRevisionBuilder.Nuget;

internal sealed class LockFile : IDisposable
{
    private FileStream? stream;

    private LockFile(FileStream stream) => this.stream = stream;

    public static LockFile Lock(string lockFilePath) => new(new(lockFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 0, FileOptions.DeleteOnClose));

    public void Dispose()
    {
        if (stream is not null)
        {
            stream.Dispose();
            stream = default;
        }
    }
}