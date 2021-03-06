﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Storage.Streams;
using Windows.Foundation;
using Windows.Web.Http;
using Windows.Web.Http.Filters;
using Windows.Web.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Matrix_UWP {
  namespace Helpers {
    class HttpJsonRequest {
      private HttpClient httpClient;
      public HttpJsonRequest() {
        this.init();
      }

      public async Task<JObject> getAsync(Uri uri) {
        HttpResponseMessage response = null;
        string meta = $"GET {uri}";
        try {
          Debug.WriteLine($"Requesting: {meta}");
          response = await httpClient.GetAsync(uri);
        } catch (Exception e) {
          throw new MatrixException.NetworkError(meta, e);
        }
        return await parseResponseAsJson(response);
      }

      public async Task<JObject> postAsync(Uri uri, JObject body) {
        IHttpContent jsonContent = new HttpJsonContent(body);
        HttpResponseMessage response = null;
        string meta = $"POST {uri}";
        try {
          Debug.WriteLine($"Requesting: {meta}");
          Debug.WriteLine($"with body: {JsonConvert.SerializeObject(body, Formatting.Indented)}");
          response = await httpClient.PostAsync(uri, jsonContent);
        } catch (Exception e) {
          throw new MatrixException.NetworkError(meta, e);
        }
        return await parseResponseAsJson(response);
      }

      private async Task<JObject> parseResponseAsJson(HttpResponseMessage response) {
        var text = await response.Content.ReadAsStringAsync();
        try {
          return JsonConvert.DeserializeObject(text) as JObject;
        } catch (Exception e) {
          throw new MatrixException.ParseError(e, text);
        }
      }

      private void init() {
        // HttpClient functionality can be extended by plugging multiple filters together and providing
        // HttpClient with the configured filter pipeline.
        IHttpFilter filter = new HttpBaseProtocolFilter();
        filter = new PlugInFilter(filter); // Adds a custom header to every request and response message.
        this.httpClient = new HttpClient(filter);

        // 使用谷歌浏览器的用户代理
        string userAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_12_4) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.81 Safari/537.36";
        if (!this.httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd(userAgent)) {
          Debug.Fail("Failed to use Chrome User Agent");
        }

      }
    }

    // 我也是从网上 copy 下来的啊
    // 这个是改 headers 用的
    public sealed class PlugInFilter : IHttpFilter {
      private IHttpFilter innerFilter;

      public PlugInFilter(IHttpFilter innerFilter) {
        if (innerFilter == null) {
          throw new ArgumentException("innerFilter cannot be null.");
        }
        this.innerFilter = innerFilter;
      }

      public IAsyncOperationWithProgress<HttpResponseMessage, HttpProgress> SendRequestAsync(HttpRequestMessage request) {
        return AsyncInfo.Run<HttpResponseMessage, HttpProgress>(async (cancellationToken, progress) => {
          Uri requestUri = request.RequestUri;

          HttpBaseProtocolFilter filter = new HttpBaseProtocolFilter();
          HttpCookieCollection cookieCollection = filter.CookieManager.GetCookies(requestUri);

          //string text = cookieCollection.Count + " cookies found.\r\n";
          foreach (HttpCookie cookie in cookieCollection) {
            //text += "--------------------\r\n";
            //text += "Name: " + cookie.Name + "\r\n";
            //text += "Domain: " + cookie.Domain + "\r\n";
            //text += "Path: " + cookie.Path + "\r\n";
            //text += "Value: " + cookie.Value + "\r\n";
            //text += "Expires: " + cookie.Expires + "\r\n";
            //text += "Secure: " + cookie.Secure + "\r\n";
            //text += "HttpOnly: " + cookie.HttpOnly + "\r\n";
            if (cookie.Name == "X-CSRF-Token") {
              request.Headers.Add(cookie.Name, cookie.Value);
              Debug.WriteLine("csrf token added");
            }
          }
          //Debug.WriteLine(text);
          //request.Headers.Add("Custom-Header", "CustomRequestValue");
          HttpResponseMessage response = await innerFilter.SendRequestAsync(request).AsTask(cancellationToken, progress);
          cancellationToken.ThrowIfCancellationRequested();
          //response.Headers.Add("Custom-Header", "CustomResponseValue");
          return response;
        });
      }

      public void Dispose() {
        innerFilter.Dispose();
        GC.SuppressFinalize(this);
      }
    }

    // 我也是从网上 copy 下来的啊
    // 这个大概是发 json 用的
    class HttpJsonContent : IHttpContent {
      JObject json;
      HttpContentHeaderCollection headers;

      public HttpContentHeaderCollection Headers {
        get {
          return headers;
        }
      }

      public HttpJsonContent(JObject json) {
        this.json = json;
        headers = new HttpContentHeaderCollection();
        headers.ContentType = new HttpMediaTypeHeaderValue("application/json");
        headers.ContentType.CharSet = "UTF-8";
      }

      public IAsyncOperationWithProgress<ulong, ulong> BufferAllAsync() {
        return AsyncInfo.Run<ulong, ulong>((cancellationToken, progress) => {
          return Task<ulong>.Run(() => {
            ulong length = GetLength();

            // Report progress.
            progress.Report(length);

            // Just return the size in bytes.
            return length;
          });
        });
      }

      public IAsyncOperationWithProgress<IBuffer, ulong> ReadAsBufferAsync() {
        return AsyncInfo.Run<IBuffer, ulong>((cancellationToken, progress) => {
          return Task<IBuffer>.Run(() => {
            DataWriter writer = new DataWriter();
            writer.WriteString(JsonConvert.SerializeObject(json));

            // Make sure that the DataWriter destructor does not free the buffer.
            IBuffer buffer = writer.DetachBuffer();

            // Report progress.
            progress.Report(buffer.Length);

            return buffer;
          });
        });
      }

      public IAsyncOperationWithProgress<IInputStream, ulong> ReadAsInputStreamAsync() {
        return AsyncInfo.Run<IInputStream, ulong>(async (cancellationToken, progress) => {
          InMemoryRandomAccessStream randomAccessStream = new InMemoryRandomAccessStream();
          DataWriter writer = new DataWriter(randomAccessStream);
          writer.WriteString(JsonConvert.SerializeObject(json));

          uint bytesStored = await writer.StoreAsync().AsTask(cancellationToken);

          // Make sure that the DataWriter destructor does not close the stream.
          writer.DetachStream();

          // Report progress.
          progress.Report(randomAccessStream.Size);

          return randomAccessStream.GetInputStreamAt(0);
        });
      }

      public IAsyncOperationWithProgress<string, ulong> ReadAsStringAsync() {
        return AsyncInfo.Run<string, ulong>((cancellationToken, progress) => {
          return Task<string>.Run(() => {
            string jsonString = JsonConvert.SerializeObject(json);

            // Report progress (length of string).
            progress.Report((ulong)jsonString.Length);

            return jsonString;
          });
        });
      }

      public bool TryComputeLength(out ulong length) {
        length = GetLength();
        return true;
      }

      public IAsyncOperationWithProgress<ulong, ulong> WriteToStreamAsync(IOutputStream outputStream) {
        return AsyncInfo.Run<ulong, ulong>(async (cancellationToken, progress) => {
          DataWriter writer = new DataWriter(outputStream);
          writer.WriteString(JsonConvert.SerializeObject(json));
          uint bytesWritten = await writer.StoreAsync().AsTask(cancellationToken);

          // Make sure that DataWriter destructor does not close the stream.
          writer.DetachStream();

          // Report progress.
          progress.Report(bytesWritten);

          return bytesWritten;
        });
      }

      public void Dispose() {
        return;
      }

      private ulong GetLength() {
        DataWriter writer = new DataWriter();
        writer.WriteString(JsonConvert.SerializeObject(json));

        IBuffer buffer = writer.DetachBuffer();
        return buffer.Length;
      }
    }
  }
  namespace MatrixException {
    class MatrixException : Exception {
      public MatrixException(string message) : base(message) {
        Debug.WriteLine($"创建了 Matrix 异常: {this.Message}\n{this.StackTrace}");
      }
    }
    class FatalError : MatrixException {
      public FatalError(string message) : base(message) { }
    }

    class NetworkError : FatalError {
      public NetworkError(string meta, Exception e) : base("网络异常，请检查网络设置") {
        Debug.Fail($"{meta}: 网络异常: {e.Message}");
      }
    }

    class ParseError : FatalError {
      public ParseError(Exception e, string text) : base("无法解析 Matrix 的响应") {
        Debug.Fail($"响应无法解析成 json: {e.Message}\n原文: {text}");
      }
    }
  }
  
}
