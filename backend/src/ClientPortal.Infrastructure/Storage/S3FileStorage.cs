using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClientPortal.Infrastructure.Storage;

internal sealed class S3FileStorage(IAmazonS3 client, IOptions<StorageOptions> options, ILogger<S3FileStorage> logger) : IFileStorage
{
    private readonly StorageOptions _options = options.Value;
    private readonly SemaphoreSlim _bucketGate = new(1, 1);
    private bool _bucketReady;

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);

        await client.PutObjectAsync(
            new PutObjectRequest
            {
                BucketName = _options.Bucket,
                Key = key,
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false,
            },
            cancellationToken);
    }

    public async Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        await EnsureBucketAsync(cancellationToken);

        try
        {
            var response = await client.GetObjectAsync(_options.Bucket, key, cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new NotFoundException("The file's content was not found.");
        }
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            await client.DeleteObjectAsync(_options.Bucket, key, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not delete object {Key} from bucket {Bucket}; it stays behind as an orphan.", key, _options.Bucket);
        }
    }

    private async Task EnsureBucketAsync(CancellationToken cancellationToken)
    {
        if (!_options.CreateBucketIfMissing || _bucketReady)
        {
            return;
        }

        await _bucketGate.WaitAsync(cancellationToken);
        try
        {
            if (!_bucketReady)
            {
                if (!await AmazonS3Util.DoesS3BucketExistV2Async(client, _options.Bucket))
                {
                    await client.PutBucketAsync(_options.Bucket, cancellationToken);
                }

                _bucketReady = true;
            }
        }
        finally
        {
            _bucketGate.Release();
        }
    }
}
