using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using Transcriptor.Platform.Windows;
using Xunit;

namespace Transcriptor.Transcription.Tests.Windows;

public sealed class RecordingAccessControlTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "TranscriptorAccessTests", Guid.NewGuid().ToString("N"));
    private readonly WindowsSessionDirectorySecurity _security = new();

    [Fact]
    public void RestrictSessionDirectory_ProtectsDaclAndAllowsOnlyExpectedPrincipals()
    {
        var session = Path.Combine(_root, "2026-01-01", "call-a");
        Directory.CreateDirectory(session);

        _security.RestrictSessionDirectory(session);

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
                () => _security.RestrictSessionDirectory(session));
        }
        finally
        {
            if (Directory.Exists(linkedRoot)) Directory.Delete(linkedRoot);
            if (Directory.Exists(actualRoot)) Directory.Delete(actualRoot, recursive: true);
        }
    }

    [Fact]
    public void RestrictSessionDirectory_RejectsReparsePointInsideSession()
    {
        var session = Path.Combine(_root, "2026-01-01", "call-a");
        var outside = Path.Combine(_root, "outside");
        Directory.CreateDirectory(session);
        Directory.CreateDirectory(outside);
        var link = Path.Combine(session, "linked");

        try
        {
            try { Directory.CreateSymbolicLink(link, outside); }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
            {
                return;
            }

            Assert.Throws<InvalidOperationException>(
                () => _security.RestrictSessionDirectory(session));
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
            if (Directory.Exists(session)) Directory.Delete(session, recursive: true);
            if (Directory.Exists(outside)) Directory.Delete(outside, recursive: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
