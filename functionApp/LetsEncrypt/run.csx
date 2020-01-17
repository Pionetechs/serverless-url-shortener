using System.Net;
using System;
using System.Linq;
using System.Web;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;


public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, string code, TraceWriter log)
    {
        log.Info($"C# HTTP trigger function processed a request. {code}");

        var content = File.ReadAllText(@"D:\home\site\wwwroot\.well-known\acme-challenge\" + code);
        var resp = new HttpResponseMessage(HttpStatusCode.OK);
        resp.Content = new StringContent(content, Encoding.UTF8, "text/plain");
        return resp;
    }