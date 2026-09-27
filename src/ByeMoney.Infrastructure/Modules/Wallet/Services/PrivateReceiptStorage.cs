using ByeMoney.Application.Common.Interfaces;
using ByeMoney.Infrastructure.Resources;

namespace ByeMoney.Infrastructure.Modules.Wallet.Services;

public sealed class PrivateReceiptStorage : IReceiptStorage
{
    private readonly string _rootPath;

    public PrivateReceiptStorage(string rootPath)
    {
        _rootPath = Path.GetFullPath(rootPath);
        if (_rootPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment.Equals("wwwroot", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(InfrastructureErrors.ReceiptStorage_MustBePrivate);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string idempotencyKey, CancellationToken ct = default)
    {
        Directory.CreateDirectory(_rootPath);
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            throw new InvalidDataException(InfrastructureErrors.ReceiptStorage_InvalidFormat);
        var header = new byte[12];
        var headerLength = await content.ReadAsync(header.AsMemory(0, header.Length), ct);
        var validImage = extension switch
        {
            ".png" => headerLength >= 8 && header.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
            ".jpg" or ".jpeg" => headerLength >= 3 && header[0] == 255 && header[1] == 216 && header[2] == 255,
            ".webp" => headerLength >= 12 && System.Text.Encoding.ASCII.GetString(header, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(header, 8, 4) == "WEBP",
            _ => false
        };
        if (!validImage) throw new InvalidDataException(InfrastructureErrors.ReceiptStorage_InvalidFormat);

        var temporaryPath = Path.Combine(_rootPath, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                await output.WriteAsync(header.AsMemory(0, headerLength), ct);
                await content.CopyToAsync(output, ct);
            }
            byte[] contentHash;
            await using (var savedContent = File.OpenRead(temporaryPath))
                contentHash = await System.Security.Cryptography.SHA256.HashDataAsync(savedContent, ct);
            var keyHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(idempotencyKey));
            var id = $"{Convert.ToHexString(keyHash)}-{Convert.ToHexString(contentHash)}{extension}";
            var targetPath = GetPath(id);
            if (File.Exists(targetPath)) return id;
            try { File.Move(temporaryPath, targetPath); }
            catch (IOException) when (File.Exists(targetPath) && !ct.IsCancellationRequested) { File.Delete(temporaryPath); }
            return id;
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public Task<bool> ExistsAsync(string receiptId, CancellationToken ct = default)
        => Task.FromResult(File.Exists(GetPath(receiptId)));

    public Task<Stream?> OpenReadAsync(string receiptId, CancellationToken ct = default)
        => Task.FromResult<Stream?>(File.Exists(GetPath(receiptId)) ? File.OpenRead(GetPath(receiptId)) : null);

    public Task DeleteAsync(string receiptId, CancellationToken ct = default)
    {
        var path = GetPath(receiptId);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id != Path.GetFileName(id) || id.Contains(".."))
            throw new InvalidDataException(InfrastructureErrors.ReceiptStorage_NotFound);
        return Path.Combine(_rootPath, id);
    }
}
