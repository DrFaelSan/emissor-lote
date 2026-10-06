using System.Security.Cryptography;
using NEO_e.Application.Contracts;

namespace NEO_e.Infrastructure.Security;

public sealed class DpapiCredentialManager : ICredentialManager
{
    private readonly string _basePath;

    public DpapiCredentialManager(string? basePath = null)
    {
        _basePath = basePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NEO-e",
            "Credentials");
        
        Directory.CreateDirectory(_basePath);
    }

    public async Task<bool> SavePasswordAsync(string key, string password, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        
        try
        {
            var data = System.Text.Encoding.UTF8.GetBytes(password);
            var entropy = GetEntropy(key);
            var protectedData = ProtectedData.Protect(data, entropy, DataProtectionScope.CurrentUser);
            
            var filePath = GetFilePath(key);
            await File.WriteAllBytesAsync(filePath, protectedData, ct);
            
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string?> GetPasswordAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        
        try
        {
            var filePath = GetFilePath(key);
            if (!File.Exists(filePath))
                return null;

            var protectedData = await File.ReadAllBytesAsync(filePath, ct);
            var entropy = GetEntropy(key);
            var data = ProtectedData.Unprotect(protectedData, entropy, DataProtectionScope.CurrentUser);
            
            return System.Text.Encoding.UTF8.GetString(data);
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DeletePasswordAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        
        try
        {
            var filePath = GetFilePath(key);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var filePath = GetFilePath(key);
        return await Task.FromResult(File.Exists(filePath));
    }

    private string GetFilePath(string key)
    {
        // Use thumbprint as filename (sanitized)
        var safeKey = key.Replace(":", "").Replace(" ", "");
        return Path.Combine(_basePath, $"{safeKey}.cred");
    }

    private byte[] GetEntropy(string key)
    {
        // Use a hash of the key as entropy for additional security
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(key));
    }
}