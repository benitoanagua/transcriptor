using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Transcriptor.Transcription;

namespace Transcriptor.Platform.Windows;

/// <summary>
/// Windows/NTFS implementation of the session directory security port. The code is
/// the original RecordingAccessControl surface moved unchanged; callers reach it
/// through the portable ISessionDirectorySecurity port.
/// </summary>
public sealed class WindowsSessionDirectorySecurity : ISessionDirectorySecurity
{
    public void ValidateDirectoryPath(string directory)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("La protección ACL de grabaciones requiere Windows y NTFS.");

        var path = Path.GetFullPath(directory);
        for (DirectoryInfo? current = new(path); current is not null; current = current.Parent)
        {
            if (!current.Exists)
                throw new DirectoryNotFoundException("No se pudo validar toda la ruta de la carpeta de sesión.");
            if ((File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("No se utilizarán rutas que contengan enlaces o puntos de reanálisis.");
        }
    }

    public void ValidateSessionDirectory(string sessionDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionDirectory);
        var path = Path.GetFullPath(sessionDirectory);
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException("La carpeta de sesión no existe.");
        ValidateDirectoryPath(path);
        EnsureNoReparsePointsInside(path);
    }

    public void RestrictSessionDirectory(string sessionDirectory)
    {
        var path = Path.GetFullPath(sessionDirectory);
        ValidateSessionDirectory(path);

        using var identity = WindowsIdentity.GetCurrent();
        var userSid = identity.User
            ?? throw new InvalidOperationException("No se pudo identificar al usuario actual de Windows.");

        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        const FileSystemRights rights = FileSystemRights.FullControl;
        const InheritanceFlags inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;

        security.AddAccessRule(new FileSystemAccessRule(userSid, rights, inheritance,
            PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), rights, inheritance,
            PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), rights, inheritance,
            PropagationFlags.None, AccessControlType.Allow));

        new DirectoryInfo(path).SetAccessControl(security);
    }

    private static void EnsureNoReparsePointsInside(string directory)
    {
        var pending = new Stack<string>();
        pending.Push(directory);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidOperationException("La sesión contiene un enlace o punto de reanálisis; se cancela la operación.");
                if (Directory.Exists(entry)) pending.Push(entry);
            }
        }
    }
}
