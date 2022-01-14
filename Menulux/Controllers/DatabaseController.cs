using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Menulux.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DatabaseController : ControllerBase
    {
        private static CloudBlobClient _blobClient;
        private const string _blobContainerName = "imagecontainer";
        private readonly IConfiguration _configuration;
        private static CloudBlobContainer _blobContainer;

        public DatabaseController(IConfiguration configuration)
        {
            _configuration = configuration;
        }


        // GET: api/<DatabaseController>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var storageConnectionString = _configuration.GetValue<string>("StorageConnectionString");
                var storageAccount = CloudStorageAccount.Parse(storageConnectionString);

                _blobClient = storageAccount.CreateCloudBlobClient();
                _blobContainer = _blobClient.GetContainerReference(_blobContainerName);
                await _blobContainer.CreateIfNotExistsAsync();

                await _blobContainer.SetPermissionsAsync(new BlobContainerPermissions { PublicAccess = BlobContainerPublicAccessType.Blob });

                List<Uri> allBlobs = new List<Uri>();
                BlobContinuationToken blobContinuationToken = null;
                do
                {
                    var response = await _blobContainer.ListBlobsSegmentedAsync(blobContinuationToken);
                    foreach (IListBlobItem blob in response.Results)
                    {
                        if (blob.GetType() == typeof(CloudBlockBlob))
                            allBlobs.Add(blob.Uri);
                    }
                    blobContinuationToken = response.ContinuationToken;
                } while (blobContinuationToken != null);

                return Ok(allBlobs);
            }
            catch (Exception ex)
            {
                return NotFound("Hata: "+ ex.Message);
            }
        }

        // POST api/<DatabaseController>
        [HttpPost]
        public async Task<IActionResult> Post()
        {
            try
            {
                var request = await HttpContext.Request.ReadFormAsync();
                if (request.Files == null)
                {
                    return BadRequest("Dosya yüklenemedi");
                }
                var files = request.Files;
                if (files.Count == 0)
                {
                    return BadRequest("Dosya boş olduğu için yüklenemedi");
                }

                for (int i = 0; i < files.Count; i++)
                {
                    var blob = _blobContainer.GetBlockBlobReference(GetRandomBlobName(files[i].FileName));
                    using (var stream = files[i].OpenReadStream())
                    {
                        await blob.UploadFromStreamAsync(stream);

                    }
                }
                return RedirectToAction("Get");
            }
            catch (Exception ex)
            {
                return Conflict("Hata: "+ex.Message);
            }
        }

        private string GetRandomBlobName(string filename)
        {
            string ext = Path.GetExtension(filename);
            return string.Format("{0:10}_{1}{2}", DateTime.Now.Ticks, Guid.NewGuid(), ext);
        }
    }
}
