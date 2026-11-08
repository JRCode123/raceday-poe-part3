// ── ADD THIS FILE TO: RaceDay.API/Services/BlobStorageService.cs ────────────
// Also add to RaceDay.API.csproj:
//   <PackageReference Include="Azure.Storage.Blobs" Version="12.19.1" />
// And in Program.cs:
//   builder.Services.AddSingleton<BlobStorageService>();
// And in appsettings.json:
//   "AzureBlob": { "ConnectionString": "...", "ContainerName": "raceday-images" }
// ────────────────────────────────────────────────────────────────────────────

using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RaceDay.API.Services;

/// <summary>
/// Handles uploading files to Azure Blob Storage.
/// Called only by API controllers — the MVC app NEVER uploads directly to Azure.
/// </summary>
public class BlobStorageService
{
    private readonly string? _connectionString;
    private readonly string _containerName;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IConfiguration config, ILogger<BlobStorageService> logger)
    {
        _connectionString = config["AzureBlob:ConnectionString"];
        _containerName    = config["AzureBlob:ContainerName"] ?? "raceday-images";
        _logger           = logger;
    }

    /// <summary>
    /// Uploads a file stream to Azure Blob Storage and returns its public URL.
    /// Returns null when Azure is not configured (e.g. development without credentials).
    /// </summary>
    public async Task<string?> UploadAsync(Stream fileStream, string fileName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(_connectionString) ||
            _connectionString.StartsWith("YOUR_"))
        {
            _logger.LogWarning("Azure Blob Storage not configured — skipping upload for '{FileName}'", fileName);
            return null;
        }

        try
        {
            var serviceClient   = new BlobServiceClient(_connectionString);
            var containerClient = serviceClient.GetBlobContainerClient(_containerName);

            // Create container if it doesn't exist and make it public-read
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            // Use a GUID prefix so filenames never collide
            var blobName   = $"{Guid.NewGuid():N}_{fileName}";
            var blobClient = containerClient.GetBlobClient(blobName);

            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            };

            await blobClient.UploadAsync(fileStream, uploadOptions);

            _logger.LogInformation("Uploaded blob '{BlobName}' to container '{Container}'",
                blobName, _containerName);

            return blobClient.Uri.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload '{FileName}' to Azure Blob Storage", fileName);
            return null;
        }
    }

    public async Task DeleteAsync(string blobUrl)
    {
        if (string.IsNullOrEmpty(blobUrl)) return;
        try
        {
            var uri        = new Uri(blobUrl);
            var blobName   = uri.Segments.Last();
            var svc        = new BlobServiceClient(_connectionString);
            var container  = svc.GetBlobContainerClient(_containerName);
            await container.GetBlobClient(blobName).DeleteIfExistsAsync();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete blob {Url}", blobUrl); }
    }
}

// BlobStorageService handles Azure uploads for the API
