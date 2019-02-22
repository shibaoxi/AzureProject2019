using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ImageResizer;
using MyIntellipix.Models;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Configuration;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;

namespace MyIntellipix.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index(string id)
        {
            //在view包中传递一个blob URI的列表
            CloudStorageAccount account = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("photos");
            List<BlobInfo> blobs = new List<BlobInfo>();
            foreach(IListBlobItem item in container.ListBlobs())
            {
                var blob = item as CloudBlockBlob;
                if(blob !=null)
                {
                    blob.FetchAttributes();//获取blob元数据
                    if(string.IsNullOrEmpty(id)||HasMatchingMetadata(blob,id))
                    {
                        var caption = blob.Metadata.ContainsKey("Caption") ? blob.Metadata["Caption"] : blob.Name;
                        blobs.Add(new BlobInfo()
                        {
                            ImageUri = blob.Uri.ToString(),
                            ThumbnailUri = blob.Uri.ToString().Replace("/photos/", "/thumbnails/"),
                            Caption = caption
                        }
                            );
                    }
                    
                }
            }
            ViewBag.Blobs = blobs.ToArray();
            return View();
        }

        private bool HasMatchingMetadata(CloudBlockBlob blob, string term)
        {
            foreach(var item in blob.Metadata)
            {
                if (item.Key.StartsWith("Tag") && item.Value.Equals(term, StringComparison.InvariantCultureIgnoreCase))
                    return true;
            }
            return false;
        }

        [HttpPost]
        public async Task<ActionResult> Upload(HttpPostedFileBase file)
        {
            //确认用户选择了图像文件
            if(!file.ContentType.StartsWith("image"))
            {
                TempData["Message"] = "只有图像文件可以上传";
            }
            else
            {
                try
                {
                    //保存原文件在photos容器中
                    CloudStorageAccount account = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
                    CloudBlobClient client = account.CreateCloudBlobClient();
                    CloudBlobContainer container = client.GetContainerReference("photos");
                    CloudBlockBlob photo = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                    await photo.UploadFromStreamAsync(file.InputStream);
                    
                    //生成缩略图并保存在 "thumbnails" 容器中
                    using (var outputStream = new MemoryStream())
                    {
                        file.InputStream.Seek(0L, SeekOrigin.Begin);
                        var settings = new ResizeSettings { MaxWidth = 192 };
                        ImageBuilder.Current.Build(file.InputStream, outputStream, settings);
                        outputStream.Seek(0L, SeekOrigin.Begin);
                        container = client.GetContainerReference("thumbnails");
                        CloudBlockBlob thumbnail = container.GetBlockBlobReference(Path.GetFileName(file.FileName));
                        await thumbnail.UploadFromStreamAsync(outputStream);

                        //提交图像到azure 视觉服务api接口去分析
                        ComputerVisionClient vision = new ComputerVisionClient(
                            new ApiKeyServiceClientCredentials(ConfigurationManager.AppSettings["SubscriptionKey"]),
                            new System.Net.Http.DelegatingHandler[] { });
                        vision.Endpoint = ConfigurationManager.AppSettings["VisionEndpoint"];
                        VisualFeatureTypes[] features = new VisualFeatureTypes[] { VisualFeatureTypes.Description };
                        var result = await vision.AnalyzeImageAsync(photo.Uri.ToString(), features);
                        //在blob元数据中记录图像描述和标记
                        photo.Metadata.Add("Caption", result.Description.Captions[0].Text);
                        for (int i = 0; i < result.Description.Tags.Count; i++)
                        {
                            string key = string.Format("Tag{0}", i);
                            photo.Metadata.Add(key, result.Description.Tags[i]);
                        }
                        await photo.SetMetadataAsync();
                    }
                    

                }
                catch (Exception ex)
                {
                    TempData["Message"] = ex.Message;
                }
            }
            return RedirectToAction("Index");
        }
        
        [HttpPost]
        public ActionResult Search(String term)
        {
            return RedirectToAction("Index", new { id = term });
        }
        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}