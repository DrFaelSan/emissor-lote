using System.IO;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using NEO_e.Application.Contracts;

namespace NEO_e.Infrastructure.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiCredentialManager : ICredentialManager
{
    private static readonly byte[] StaticEntropy = Encoding.UTF8.GetBytes("NEO-e.Crypto.v1");
    private readonly string _credentialsDir;

    public DpapiCredentialManager()
    {
        _credentialsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NEO-e",
            "Credentials");

        EnsureRestrictedDirectory(_credentialsDir);
    }

    public Task<bool> SavePasswordAsync(string identifier, string password, CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = GetFilePath(identifier);
            var plainBytes = Encoding.UTF8.GetBytes(password);
            var protectedBytes = ProtectedData.Protect(
                plainBytes,
                StaticEntropy,
                DataProtectionScope.CurrentUser);

            File.WriteAllBytes(filePath, protectedBytes);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<string?> GetPasswordAsync(string identifier, CancellationToken cancellationToken = default)
    {
        try
        {
            var filePath = GetFilePath(identifier);
            if (!File.Exists(filePath))
                return Task.FromResult<string?>(null);

            var protectedBytes = File.ReadAllBytes(filePath);
            var plainBytes = ProtectedData.Unprotect(
                protectedBytes,
                StaticEntropy,
                DataProtectionScope.CurrentUser);

            return Task.FromResult<string?>(Encoding.UTF8.GetString(plainBytes));
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    private string GetFilePath(string identifier)
    {
        var safeName = string.Concat(identifier.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_credentialsDir, $"{safeName}.cred");
    }

    private static void EnsureRestrictedDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            var dirInfo = Directory.CreateDirectory(path);
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser != null)
            {
                var security = new DirectorySecurity();
                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
                security.AddAccessRule(new FileSystemAccessRule(
                    currentUser,
                    FileSystemRights.FullControl,
                    AccessControlType.Allow));
                dirInfo.SetAccessControl(security);
            }
        }
    }
}