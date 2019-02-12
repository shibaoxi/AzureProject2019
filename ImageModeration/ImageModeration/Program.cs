using Microsoft.Azure.CognitiveServices.ContentModerator;
//using Microsoft.CognitiveServices.ContentModerator;
//using Microsoft.CognitiveServices.ContentModerator.Models;
using Microsoft.Azure.CognitiveServices.ContentModerator.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Configuration;

namespace ImageModeration
{
    class Program
    {
        //The name of the file that contains the image URLs to evaluate.
        private static string ImageUrlFile = "ImageFiles.txt";
    //The name of the file to contain the output from the evaluation.
        private static string OutputFile = "ModerationOutput.json";
    static void Main(string[] args)
    {
        // Create an object to store the image moderation results.
        List<EvaluationData> evaluationData = new List<EvaluationData>();
        // Create an instance of the Content Moderator API wrapper.
        using (var client = Clients.NewClient())
        {
            // Read image URLs from the input file and evaluate each one.
            using (StreamReader inputReader = new StreamReader(ImageUrlFile))
            {
                while (!inputReader.EndOfStream)
                {
                    string line = inputReader.ReadLine().Trim();
                    if (line != string.Empty)
                    {
                        EvaluationData imageData = EvaluateImage(client, line);
                           // Console.WriteLine($"IsImageAdultClassified: {imageModeration.IsImageAdultClassified}");
                            evaluationData.Add(imageData);
                    }
                }
            }

        }
        using (StreamWriter outputWriter = new StreamWriter(OutputFile, false))
        {
            outputWriter.WriteLine(JsonConvert.SerializeObject(evaluationData, Formatting.Indented));
            outputWriter.Flush();
            outputWriter.Close();
        }
    }
    private static EvaluationData EvaluateImage(ContentModeratorClient client, string ImageUrl)
    {
        var url = new BodyModel("URL", ImageUrl.Trim());
        var imageData = new EvaluationData();
        imageData.ImageUrl = url.Value;
        // Evaluate for adult and racy content.
        imageData.ImageModeration = client.ImageModeration.EvaluateUrlInput("application/json", url,true);
        Thread.Sleep(1000);
        // Detect and extract text.
        imageData.TextDetection = client.ImageModeration.OCRUrlInput("eng", "application/json", url, true);
        Thread.Sleep(1000);
        // Detect faces.
        imageData.FaceDetection = client.ImageModeration.FindFacesUrlInput("application/json", url, true);
        Thread.Sleep(1000);
        return imageData;
    }
}
// Wraps the creation and configuration of a Content Moderator client.
public static class Clients
{
    // The region/location for your Content Moderator account, 
    // for example, westus.
    private static readonly string AzureRegion = "eastasia";
    // The base URL fragment for Content Moderator calls.
    private static readonly string AzureBaseURL = $"https://{AzureRegion}.api.cognitive.microsoft.com";
    // Your Content Moderator subscription key.
    private static readonly string CMSubscriptionKey = "Your API Key";
    // Returns a new Content Moderator client for your subscription.
    public static ContentModeratorClient NewClient()
    {
        //Create and initialize an instance of the Content Moderator API wrapper.
        ContentModeratorClient client = new ContentModeratorClient(new ApiKeyServiceClientCredentials(CMSubscriptionKey));
        client.Endpoint = AzureBaseURL;
        return client;
    }
}
// Contains the image moderation results for an image, 
// including text and face detection results.
public class EvaluationData
{
    // The URL of the evaluated image.
    public string ImageUrl;
    // The image moderation results.
    public Evaluate ImageModeration;
    // The text detection results.
    public OCR TextDetection;
    // The face detection results;
    public FoundFaces FaceDetection;

}
}
