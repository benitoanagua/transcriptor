using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Transcriptor.Desktop;
using Xunit;

namespace Transcriptor.Transcription.Tests;

public sealed class RecordingAccessControlTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "TranscriptorAccessTests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void RestrictSessionDirectory_ProtectsDaclAndAllowsOnlyExpectedPrincipals()
    {
        var session = Path.Combine(_root, "2026-01-01", "call-a");
        Directory.CreateDirectory(session);

        RecordingAccessControl.RestrictSessionDirectory(session);

        var security = new DirectoryInfo(session).GetAccessControl(AccessControlSections.Access);
        Assert.True(security.AreAccessRulesProtected);

        var rules = security.GetAccessRules(true, true, typeof(SecurityIdentifier))
            .Cast<FileSystemAccessRule>().ToArray();
        Assert.Equal(3, rules.Length);
        Assert.All(rules, rule =>
        {
            Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
            Assert.Equal(FileSystemRights.FullControl, rule.FileSystemRights);
            Assert.Equal(InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, rule.InheritanceFlags);
        });

        using var identity = WindowsIdentity.GetCurrent();
        var expectedSids = new[]
        {
            identity.User!,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)
        };
        Assert.Equal(expectedSids.OrderBy(sid => sid.Value), rules
            .Select(rule => (SecurityIdentifier)rule.IdentityReference)
            .OrderBy(sid => sid.Value));
    }

    [Fact]
    public void RestrictSessionDirectory_RejectsReparsePointInAnAncestor()
    {
        var actualRoot = Path.Combine(_root, "actual");
        var linkedRoot = Path.Combine(_root, "linked");
        var session = Path.Combine(linkedRoot, "2026-01-01", "call-a");
        Directory.CreateDirectory(Path.Combine(actualRoot, "2026-01-01", "call-a"));

        try
        {
            try { Directory.CreateSymbolicLink(linkedRoot, actualRoot); }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return; // El agente puede no permitir crear enlaces simbólicos.
            }

            Assert.Throws<InvalidOperationException>(
                () => RecordingAccessControl.RestrictSessionDirectory(session));
        }
        finally
        {
            if (Directory.Exists(linkedRoot)) Directory.Delete(linkedRoot);
            if (Directory.Exists(actualRoot)) Directory.Delete(actualRoot, recursive: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
