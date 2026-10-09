using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace SwiftyS26.Models
{
    public class DetectedFoodLabel
    {
        public string Label { get; set; }

        public double Confidence { get; set; }

        // Helpful when debugging.
        public string Source { get; set; }

        // Full image / Top Left / Bottom Right etc.
        public string Region { get; set; }
    }


    public static class VisionService
    {
        private static readonly string ApiKey =
            ConfigurationManager.AppSettings[
                "AzureVisionKey"
            ];

        private static readonly string Endpoint =
            ConfigurationManager.AppSettings[
                "AzureVisionEndpoint"
            ];


        // =========================================================
        // MAIN METHOD USED BY MEAL CONTROLLER
        // =========================================================

        public static async Task<List<DetectedFoodLabel>>
            DetectFoodLabels(byte[] imageBytes)
        {
            ServicePointSetup();

            var allResults =
                new List<DetectedFoodLabel>();

            if (imageBytes == null ||
                imageBytes.Length == 0)
            {
                return allResults;
            }

            if (string.IsNullOrWhiteSpace(ApiKey) ||
                string.IsNullOrWhiteSpace(Endpoint))
            {
                System.Diagnostics.Debug.WriteLine(
                    "[VISION ERROR] Azure Vision key or endpoint is missing."
                );

                return allResults;
            }

            try
            {
                // =================================================
                // PASS 1:
                // Analyze the complete meal photo.
                // =================================================

                var fullImageResults =
                    await AnalyzeImage(
                        imageBytes,
                        "Full Image"
                    );

                allResults.AddRange(
                    fullImageResults
                );


                // =================================================
                // PASS 2:
                // Crop the meal into sections.
                //
                // This improves the chance of Azure recognizing
                // separate foods on the same plate.
                // =================================================

                var crops =
                    CreateImageCrops(
                        imageBytes
                    );

                foreach (var crop in crops)
                {
                    try
                    {
                        var cropResults =
                            await AnalyzeImage(
                                crop.ImageBytes,
                                crop.RegionName
                            );

                        allResults.AddRange(
                            cropResults
                        );
                    }
                    catch (Exception ex)
                    {
                        // One failed region should not stop
                        // the entire meal scan.
                        System.Diagnostics.Debug.WriteLine(
                            "[VISION CROP ERROR] "
                            + crop.RegionName
                            + ": "
                            + ex.Message
                        );
                    }
                }


                // =================================================
                // MERGE DUPLICATE LABELS
                // =================================================

                var mergedResults =
                    MergeDetectedLabels(
                        allResults
                    );

                return mergedResults;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[VISION ERROR] "
                    + ex.Message
                );

                var inner =
                    ex.InnerException;

                while (inner != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[VISION INNER ERROR] "
                        + inner.Message
                    );

                    inner =
                        inner.InnerException;
                }

                return MergeDetectedLabels(
                    allResults
                );
            }
        }


        // =========================================================
        // ANALYZE ONE IMAGE / ONE REGION
        // =========================================================

        private static async Task<List<DetectedFoodLabel>>
            AnalyzeImage(
                byte[] imageBytes,
                string regionName)
        {
            var results =
                new List<DetectedFoodLabel>();

            string requestUrl =
                Endpoint.TrimEnd('/')
                +
                "/vision/v3.2/analyze"
                +
                "?visualFeatures=Tags,Objects,Description";


            using (var client =
                new HttpClient())
            {
                client.Timeout =
                    TimeSpan.FromSeconds(30);

                client.DefaultRequestHeaders.Add(
                    "Ocp-Apim-Subscription-Key",
                    ApiKey
                );

                using (var content =
                    new ByteArrayContent(
                        imageBytes
                    ))
                {
                    content.Headers.ContentType =
                        new MediaTypeHeaderValue(
                            "application/octet-stream"
                        );

                    HttpResponseMessage response;

                    try
                    {
                        response =
                            await client.PostAsync(
                                requestUrl,
                                content
                            );
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "[VISION REQUEST ERROR] "
                            + regionName
                            + ": "
                            + ex.Message
                        );

                        return results;
                    }

                    string responseJson =
                        await response
                            .Content
                            .ReadAsStringAsync();

                    if (!response.IsSuccessStatusCode)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            "[VISION ERROR] "
                            + regionName
                            + " Status "
                            + response.StatusCode
                            + ": "
                            + responseJson
                        );

                        return results;
                    }

                    JObject parsed =
                        JObject.Parse(
                            responseJson
                        );


                    // =============================================
                    // TAGS
                    // =============================================

                    var tags =
                        parsed["tags"];

                    if (tags != null)
                    {
                        foreach (var tag in tags)
                        {
                            string name =
                                tag["name"]
                                    ?.ToString();

                            double confidence =
                                tag["confidence"]
                                    ?.ToObject<double>()
                                ?? 0;

                            AddResult(
                                results,
                                name,
                                confidence,
                                "Tag",
                                regionName
                            );
                        }
                    }


                    // =============================================
                    // OBJECT DETECTION
                    // =============================================

                    var objects =
                        parsed["objects"];

                    if (objects != null)
                    {
                        foreach (var obj in objects)
                        {
                            string objectName =
                                obj["object"]
                                    ?.ToString();

                            double confidence =
                                obj["confidence"]
                                    ?.ToObject<double>()
                                ?? 0;

                            AddResult(
                                results,
                                objectName,
                                confidence,
                                "Object",
                                regionName
                            );


                            // Some Azure object results contain
                            // parent categories. Keep those too.
                            var parent =
                                obj["parent"];

                            if (parent != null)
                            {
                                string parentName =
                                    parent["object"]
                                        ?.ToString();

                                double parentConfidence =
                                    parent["confidence"]
                                        ?.ToObject<double>()
                                    ?? confidence;

                                AddResult(
                                    results,
                                    parentName,
                                    parentConfidence,
                                    "Object Parent",
                                    regionName
                                );
                            }
                        }
                    }


                    // =============================================
                    // DESCRIPTION TAGS
                    // =============================================

                    var description =
                        parsed["description"];

                    if (description != null)
                    {
                        var descriptionTags =
                            description["tags"];

                        if (descriptionTags != null)
                        {
                            foreach (var tag
                                in descriptionTags)
                            {
                                string name =
                                    tag.ToString();

                                // Description tags don't always
                                // provide individual confidence.
                                AddResult(
                                    results,
                                    name,
                                    0.50,
                                    "Description Tag",
                                    regionName
                                );
                            }
                        }


                        // =========================================
                        // CAPTIONS
                        //
                        // Example:
                        // "a plate of chicken rice and vegetables"
                        //
                        // Keeping the caption gives our database
                        // matcher another useful candidate.
                        // =========================================

                        var captions =
                            description["captions"];

                        if (captions != null)
                        {
                            foreach (var caption
                                in captions)
                            {
                                string text =
                                    caption["text"]
                                        ?.ToString();

                                double confidence =
                                    caption["confidence"]
                                        ?.ToObject<double>()
                                    ?? 0;

                                if (!string.IsNullOrWhiteSpace(
                                    text))
                                {
                                    AddResult(
                                        results,
                                        text,
                                        confidence,
                                        "Caption",
                                        regionName
                                    );

                                    // Extract useful words from
                                    // captions as separate labels.
                                    AddCaptionWords(
                                        results,
                                        text,
                                        confidence,
                                        regionName
                                    );
                                }
                            }
                        }
                    }
                }
            }

            return results;
        }


        // =========================================================
        // ADD RESULT
        // =========================================================

        private static void AddResult(
            List<DetectedFoodLabel> results,
            string label,
            double confidence,
            string source,
            string region)
        {
            if (string.IsNullOrWhiteSpace(
                label))
            {
                return;
            }

            if (confidence < 0.10)
                return;

            label =
                label.Trim()
                    .ToLowerInvariant();

            results.Add(
                new DetectedFoodLabel
                {
                    Label =
                        label,

                    Confidence =
                        confidence,

                    Source =
                        source,

                    Region =
                        region
                }
            );
        }


        // =========================================================
        // EXTRACT WORDS FROM DESCRIPTION CAPTION
        // =========================================================

        private static void AddCaptionWords(
            List<DetectedFoodLabel> results,
            string caption,
            double confidence,
            string regionName)
        {
            if (string.IsNullOrWhiteSpace(
                caption))
            {
                return;
            }

            string cleaned =
                caption
                    .ToLowerInvariant()
                    .Replace(",", " ")
                    .Replace(".", " ")
                    .Replace("(", " ")
                    .Replace(")", " ")
                    .Replace("-", " ");

            var ignoredWords =
                new HashSet<string>(
                    new[]
                    {
                        "a",
                        "an",
                        "the",
                        "of",
                        "with",
                        "and",
                        "on",
                        "in",
                        "at",
                        "to",
                        "from",
                        "some",
                        "several",
                        "food",
                        "meal",
                        "plate",
                        "bowl",
                        "dish",
                        "table",
                        "image",
                        "picture",
                        "photo"
                    },
                    StringComparer.OrdinalIgnoreCase
                );

            var words =
                cleaned
                    .Split(
                        new[] { ' ' },
                        StringSplitOptions
                            .RemoveEmptyEntries
                    )
                    .Select(x =>
                        x.Trim())
                    .Where(x =>
                        x.Length >= 3)
                    .Where(x =>
                        !ignoredWords.Contains(x))
                    .Distinct()
                    .ToList();

            foreach (string word
                in words)
            {
                AddResult(
                    results,
                    word,

                    // Caption words should be slightly less
                    // trusted than the complete caption.
                    Math.Max(
                        0.20,
                        confidence * 0.85
                    ),

                    "Caption Word",
                    regionName
                );
            }
        }


        // =========================================================
        // CREATE IMAGE REGIONS
        // =========================================================

        private static List<ImageCrop>
            CreateImageCrops(
                byte[] imageBytes)
        {
            var crops =
                new List<ImageCrop>();

            try
            {
                using (var input =
                    new MemoryStream(
                        imageBytes
                    ))
                using (var original =
                    Image.FromStream(input))
                {
                    int width =
                        original.Width;

                    int height =
                        original.Height;

                    // Very small images don't benefit
                    // much from cropping.
                    if (width < 400 ||
                        height < 400)
                    {
                        return crops;
                    }

                    int halfWidth =
                        width / 2;

                    int halfHeight =
                        height / 2;


                    // =============================================
                    // TOP LEFT
                    // =============================================

                    crops.Add(
                        CreateCrop(
                            original,
                            new Rectangle(
                                0,
                                0,
                                halfWidth,
                                halfHeight
                            ),
                            "Top Left"
                        )
                    );


                    // =============================================
                    // TOP RIGHT
                    // =============================================

                    crops.Add(
                        CreateCrop(
                            original,
                            new Rectangle(
                                halfWidth,
                                0,
                                width - halfWidth,
                                halfHeight
                            ),
                            "Top Right"
                        )
                    );


                    // =============================================
                    // BOTTOM LEFT
                    // =============================================

                    crops.Add(
                        CreateCrop(
                            original,
                            new Rectangle(
                                0,
                                halfHeight,
                                halfWidth,
                                height - halfHeight
                            ),
                            "Bottom Left"
                        )
                    );


                    // =============================================
                    // BOTTOM RIGHT
                    // =============================================

                    crops.Add(
                        CreateCrop(
                            original,
                            new Rectangle(
                                halfWidth,
                                halfHeight,
                                width - halfWidth,
                                height - halfHeight
                            ),
                            "Bottom Right"
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[VISION CROP CREATION ERROR] "
                    + ex.Message
                );
            }

            return crops;
        }


        // =========================================================
        // CREATE ONE CROPPED IMAGE
        // =========================================================

        private static ImageCrop CreateCrop(
            Image source,
            Rectangle rectangle,
            string regionName)
        {
            using (var bitmap =
                new Bitmap(
                    rectangle.Width,
                    rectangle.Height
                ))
            {
                using (Graphics graphics =
                    Graphics.FromImage(bitmap))
                {
                    graphics.DrawImage(
                        source,

                        new Rectangle(
                            0,
                            0,
                            bitmap.Width,
                            bitmap.Height
                        ),

                        rectangle,

                        GraphicsUnit.Pixel
                    );
                }

                using (var ms =
                    new MemoryStream())
                {
                    bitmap.Save(
                        ms,
                        ImageFormat.Jpeg
                    );

                    return new ImageCrop
                    {
                        RegionName =
                            regionName,

                        ImageBytes =
                            ms.ToArray()
                    };
                }
            }
        }


        // =========================================================
        // MERGE RESULTS
        // =========================================================

        private static List<DetectedFoodLabel>
            MergeDetectedLabels(
                List<DetectedFoodLabel> labels)
        {
            if (labels == null)
            {
                return
                    new List<DetectedFoodLabel>();
            }

            var merged =
                labels
                    .Where(x =>
                        x != null &&
                        !string.IsNullOrWhiteSpace(
                            x.Label
                        ))
                    .GroupBy(x =>
                        NormalizeLabel(
                            x.Label
                        ))
                    .Where(g =>
                        !string.IsNullOrWhiteSpace(
                            g.Key
                        ))
                    .Select(group =>
                    {
                        // Keep strongest occurrence
                        var strongest =
                            group
                                .OrderByDescending(
                                    x => x.Confidence
                                )
                                .First();

                        // Slight bonus when Azure sees the same
                        // item in more than one image region.
                        int occurrenceCount =
                            group.Count();

                        double bonus =
                            Math.Min(
                                occurrenceCount - 1,
                                3
                            )
                            * 0.03;

                        double combinedConfidence =
                            Math.Min(
                                0.99,
                                strongest.Confidence
                                + bonus
                            );

                        return new
                        {
                            Item =
                                new DetectedFoodLabel
                                {
                                    Label =
                                        strongest.Label,

                                    Confidence =
                                        combinedConfidence,

                                    Source =
                                        strongest.Source,

                                    Region =
                                        strongest.Region
                                },

                            Occurrences =
                                occurrenceCount
                        };
                    })
                    .OrderByDescending(x =>
                        x.Item.Confidence)
                    .ThenByDescending(x =>
                        x.Occurrences)
                    .Select(x =>
                        x.Item)
                    .Take(30)
                    .ToList();

            return merged;
        }


        // =========================================================
        // NORMALIZE LABELS
        // =========================================================

        private static string NormalizeLabel(
            string value)
        {
            if (string.IsNullOrWhiteSpace(
                value))
            {
                return "";
            }

            string result =
                value
                    .Trim()
                    .ToLowerInvariant()
                    .Replace("-", " ")
                    .Replace("_", " ");

            while (result.Contains("  "))
            {
                result =
                    result.Replace(
                        "  ",
                        " "
                    );
            }

            // strawberry / strawberries
            if (result.EndsWith("ies") &&
                result.Length > 4)
            {
                result =
                    result.Substring(
                        0,
                        result.Length - 3
                    )
                    + "y";
            }

            // eggs -> egg
            // bananas -> banana
            else if (
                result.EndsWith("s") &&
                !result.EndsWith("ss") &&
                result.Length > 3)
            {
                result =
                    result.Substring(
                        0,
                        result.Length - 1
                    );
            }

            return result;
        }


        // =========================================================
        // TLS SETUP
        // =========================================================

        private static void ServicePointSetup()
        {
            System.Net.ServicePointManager
                .SecurityProtocol =
                System.Net.SecurityProtocolType
                    .Tls12;
        }


        // =========================================================
        // INTERNAL CROP MODEL
        // =========================================================

        private class ImageCrop
        {
            public string RegionName
            {
                get;
                set;
            }

            public byte[] ImageBytes
            {
                get;
                set;
            }
        }
    }
}