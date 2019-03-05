#load ".\models.csx"
#r "Microsoft.WindowsAzure.Storage"
using Microsoft.WindowsAzure.Storage.Table;
using System.Net;
using System;
using System.Linq;
using System.Web;

public static readonly string SHORTENER_URL = System.Environment.GetEnvironmentVariable("SHORTENER_URL");
public static readonly string UTM_SOURCE = System.Environment.GetEnvironmentVariable("UTM_SOURCE");
public static readonly string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
public static readonly int Base = Alphabet.Length;

public static string Encode(int i)
{
    if (i == 0)
        return Alphabet[0].ToString();
    var s = string.Empty;
    while (i > 0)
    {
        s += Alphabet[i % Base];
        i = i / Base;
    }

    return string.Join(string.Empty, s.Reverse());
}

public static string[] UTM_MEDIUMS=new [] {"twitter", "facebook", "linkedin", "googleplus"};

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, NextId keyTable, CloudTable tableOut, TraceWriter log)
{
    log.Info($"C# manually triggered function called with req: {req}");

    if (req == null)
    {
        return req.CreateResponse(HttpStatusCode.NotFound);
    }

    Request input = await req.Content.ReadAsAsync<Request>();

    if (input == null)
    {
        return req.CreateResponse(HttpStatusCode.NotFound);
    }

    var result = new List<Result>();
    var url = input.Input;
    bool tagMediums = input.TagMediums.HasValue ? input.TagMediums.Value : true;
    bool tagSource = (input.TagSource.HasValue ? input.TagSource.Value : true) || tagMediums;

    log.Info($"URL: {url} Tag Source? {tagSource} Tag Mediums? {tagMediums}");
    
    if (String.IsNullOrWhiteSpace(url))
    {
        throw new Exception("Need a URL to shorten!");
    }

    if (keyTable == null)
    {
        keyTable = new NextId
        {
            PartitionKey = "1",
            RowKey = "KEY",
            Id = 1024
        };
        var keyAdd = TableOperation.Insert(keyTable);
        await tableOut.ExecuteAsync(keyAdd); 
    }
    
    log.Info($"Current key: {keyTable.Id}"); 
    
    if (tagSource) 
    {
        url = $"{url}?utm_source={UTM_SOURCE}";
    }

    if (tagMediums) 
    {
        foreach(var medium in UTM_MEDIUMS)
        {
            log.Info("tagMediums true");
            var mediumUrl = $"{url}&utm_medium={medium}";
            var shortUrl = Encode(keyTable.Id++);
            log.Info($"Short URL for {mediumUrl} is {shortUrl}");
            var newUrl = new ShortUrl 
            {
                PartitionKey = $"{shortUrl.First()}",
                RowKey = $"{shortUrl}",
                Medium = medium,
                Url = mediumUrl
            };
            var multiAdd = TableOperation.Insert(newUrl);
            await tableOut.ExecuteAsync(multiAdd); 
            result.Add(new Result 
            { 
                ShortUrl = $"{SHORTENER_URL}{newUrl.RowKey}",
                LongUrl = WebUtility.UrlDecode(newUrl.Url)
            });
        }
    }
    else 
    {
        log.Info("tagMediums false");
        var newUrl = GenerateShortUrl(keyTable.Id++, url);

        try{
            // May fail if another function instance encoded the same keyTable.Id and tries to insert the same row
            InsertUrlRow(newUrl, tableOut);

            result.Add(new Result 
            {
                ShortUrl = $"{SHORTENER_URL}{newUrl.RowKey}",
                LongUrl = WebUtility.UrlDecode(newUrl.Url)
            }); 
        }
        catch (Exception e) {
            log.Info($"got exception, falling back on GUID: {e}");

            var fallbackUrl = GenerateFallbackUrl(url);
            InsertUrlRow(fallbackUrl, tableOut);

            result.Add(new Result 
            {
                ShortUrl = $"{SHORTENER_URL}{fallbackUrl.RowKey}",
                LongUrl = WebUtility.UrlDecode(fallbackUrl.Url)
            }); 
        }

    }

    var operation = TableOperation.Replace(keyTable);
    await tableOut.ExecuteAsync(operation);

    log.Info($"Done.");
    return req.CreateResponse(HttpStatusCode.OK, result);    
}

public static ShortUrl GenerateShortUrl (int i, string url) {
    var shortUrl = Encode(i);
    var newUrl = new ShortUrl 
    {
        PartitionKey = $"{shortUrl.First()}",
        RowKey = $"{shortUrl}",
        Url = url
    };

    return newUrl;
}

public static ShortUrl GenerateFallbackUrl (string url) {
    var shortUrl = Guid.NewGuid().ToString();
    var newUrl = new ShortUrl 
    {
        PartitionKey = $"{shortUrl.First()}",
        RowKey = $"{shortUrl}",
        Url = url
    };

    return newUrl;
}

public static async void InsertUrlRow(ShortUrl url, CloudTable tableOut) {
    var singleAdd = TableOperation.Insert(url);
    await tableOut.ExecuteAsync(singleAdd);
}