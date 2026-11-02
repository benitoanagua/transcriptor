using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Transcriptor.Rec;

/// <summary>Protege la carpeta de sesión antes de crear cualquier pista cifrada o temporal.</summary>
public static class RecordingAccessControl
{
    public static void RestrictSessionDirectory(string sessionDirectory)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("La protección ACL de grabaciones requiere Windows y NTFS.");

        var path = Path.GetFullPath(sessionDirectory);
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException("La carpeta de sesión no existe.");
        EnsureNoReparsePointsInPath(path);

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

    private static void EnsureNoReparsePointsInPath(string path)
    {
        for (DirectoryInfo? current = new(path); current is not null; current = current.Parent)
        {
            if (!current.Exists)
                throw new DirectoryNotFoundException("No se pudo validar toda la ruta de la carpeta de sesión.");
            if ((File.GetAttributes(current.FullName) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException(
                    "No se aplicarán permisos sobre una ruta que contenga un enlace o punto de reanálisis.");
        }
    }
}
