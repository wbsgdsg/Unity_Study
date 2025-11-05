using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using UnityEngine;
using System.Linq;

[System.Serializable]
public class TencentTranslationRequest
{
    public string SourceText;
    public string Source;
    public string Target;
    public int ProjectId;
}

[System.Serializable]
public class TencentTranslationResponse
{
    public ResponseData Response;
    public ErrorInfo Error;
}

[System.Serializable]
public class ResponseData
{
    public string TargetText;
    public string Source;
    public string Target;
    public string RequestId;
}

[System.Serializable]
public class ErrorInfo
{
    public string Code;
    public string Message;
}

public class TencentCloudTranslator
{
    private string secretId;
    private string secretKey;
    private string region = "ap-beijing";
    private string service = "tmt";
    private string version = "2018-03-21";
    private string action = "TextTranslate";

    private string GetEndpoint()
    {
        var regionDomainMap = new Dictionary<string, string>
        {
            ["ap-guangzhou"] = "tmt.ap-guangzhou.tencentcloudapi.com",
            ["ap-shanghai"] = "tmt.ap-shanghai.tencentcloudapi.com",
            ["ap-nanjing"] = "tmt.ap-nanjing.tencentcloudapi.com",
            ["ap-beijing"] = "tmt.ap-beijing.tencentcloudapi.com",
            ["ap-chengdu"] = "tmt.ap-chengdu.tencentcloudapi.com",
            ["ap-chongqing"] = "tmt.ap-chongqing.tencentcloudapi.com",
            ["ap-hongkong"] = "tmt.ap-hongkong.tencentcloudapi.com",
            ["ap-singapore"] = "tmt.ap-singapore.tencentcloudapi.com",
            ["ap-jakarta"] = "tmt.ap-jakarta.tencentcloudapi.com",
            ["ap-bangkok"] = "tmt.ap-bangkok.tencentcloudapi.com",
            ["ap-seoul"] = "tmt.ap-seoul.tencentcloudapi.com",
            ["ap-tokyo"] = "tmt.ap-tokyo.tencentcloudapi.com",
            ["na-ashburn"] = "tmt.na-ashburn.tencentcloudapi.com",
            ["na-siliconvalley"] = "tmt.na-siliconvalley.tencentcloudapi.com",
            ["sa-saopaulo"] = "tmt.sa-saopaulo.tencentcloudapi.com",
            ["eu-frankfurt"] = "tmt.eu-frankfurt.tencentcloudapi.com"
        };

        return regionDomainMap.TryGetValue(region, out var domain) ? domain : "tmt.tencentcloudapi.com";
    }

    public async Task<string> TranslateWithTencent(string text, string targetLanguage)
    {
        if (string.IsNullOrEmpty(secretId) || secretId == "YOUR_SECRET_ID" ||
            string.IsNullOrEmpty(secretKey) || secretKey == "YOUR_SECRET_KEY")
        {
            Debug.LogError("腾讯云凭证未设置");
            return text;
        }

        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = TimeSpan.FromSeconds(30);

                var requestData = new TencentTranslationRequest
                {
                    SourceText = text,
                    Source = "auto",
                    Target = GetTencentLanguageCode(targetLanguage),
                    ProjectId = 0
                };

                string endpoint = GetEndpoint();
                string requestPayload = JsonUtility.ToJson(requestData);

                // 使用当前时间
                DateTime requestDate = DateTime.UtcNow;

                // 使用官方示例的签名方法
                var headers = BuildHeaders(secretId, secretKey, service, endpoint, region, action, version, requestDate, requestPayload);

                string url = $"https://{endpoint}";
                var request = new HttpRequestMessage(HttpMethod.Post, url);

                // 添加所有头部
                foreach (var header in headers)
                {
                    if (header.Key == "Host")
                    {
                        request.Headers.Add("Host", header.Value);
                    }
                    else if (header.Key == "Content-Type")
                    {
                        // Content-Type 会在 StringContent 中设置，这里跳过
                        continue;
                    }
                    else
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }

                // 创建内容
                var content = new StringContent(requestPayload, Encoding.UTF8, "application/json");
                request.Content = content;

                Debug.Log($"请求URL: {url}");
                Debug.Log($"请求负载: {requestPayload}");

                var response = await client.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();

                Debug.Log($"响应状态: {response.StatusCode}");
                Debug.Log($"响应内容: {responseString}");

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonUtility.FromJson<TencentTranslationResponse>(responseString);
                    if (result?.Response != null)
                    {
                        string translatedText = result.Response.TargetText;
                        Debug.Log($"翻译成功: '{text}' -> '{translatedText}'");
                        return CleanTranslationResult(translatedText);
                    }
                    else if (result?.Error != null)
                    {
                        Debug.LogError($"API返回错误: {result.Error.Code} - {result.Error.Message}");
                        return text;
                    }
                }

                Debug.LogError($"HTTP错误: {response.StatusCode}");
                return text;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"腾讯云翻译失败: {e.Message}");
            Debug.LogError($"完整异常: {e}");
            return text;
        }
    }

    // 基于官方示例的签名方法
    private Dictionary<string, string> BuildHeaders(string secretId, string secretKey, string service,
        string endpoint, string region, string action, string version, DateTime date, string requestPayload)
    {
        string datestr = date.ToString("yyyy-MM-dd");

        // 修正时间戳计算 - 使用秒级时间戳
        DateTime startTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        long requestTimestamp = (long)(date - startTime).TotalSeconds;

        // ************* 步骤 1：拼接规范请求串 *************
        string algorithm = "TC3-HMAC-SHA256";
        string httpRequestMethod = "POST";
        string canonicalUri = "/";
        string canonicalQueryString = "";
        string contentType = "application/json; charset=utf-8";

        // 修正规范头格式 - 注意结尾的 \n
        string canonicalHeaders = "content-type:" + contentType + "\n"
            + "host:" + endpoint + "\n";

        string signedHeaders = "content-type;host";
        string hashedRequestPayload = SHA256Hex(requestPayload);

        string canonicalRequest = httpRequestMethod + "\n"
            + canonicalUri + "\n"
            + canonicalQueryString + "\n"
            + canonicalHeaders + "\n"
            + signedHeaders + "\n"
            + hashedRequestPayload;

        Debug.Log("=== 规范请求 ===");
        Debug.Log(canonicalRequest);
        Debug.Log("================");

        // ************* 步骤 2：拼接待签名字符串 *************
        string credentialScope = datestr + "/" + service + "/" + "tc3_request";
        string hashedCanonicalRequest = SHA256Hex(canonicalRequest);
        string stringToSign = algorithm + "\n"
            + requestTimestamp.ToString() + "\n"
            + credentialScope + "\n"
            + hashedCanonicalRequest;

        Debug.Log("=== 待签名字符串 ===");
        Debug.Log(stringToSign);
        Debug.Log("===================");

        // ************* 步骤 3：计算签名 *************
        byte[] tc3SecretKey = Encoding.UTF8.GetBytes("TC3" + secretKey);
        byte[] secretDate = HmacSHA256(tc3SecretKey, Encoding.UTF8.GetBytes(datestr));
        byte[] secretService = HmacSHA256(secretDate, Encoding.UTF8.GetBytes(service));
        byte[] secretSigning = HmacSHA256(secretService, Encoding.UTF8.GetBytes("tc3_request"));
        byte[] signatureBytes = HmacSHA256(secretSigning, Encoding.UTF8.GetBytes(stringToSign));
        string signature = BitConverter.ToString(signatureBytes).Replace("-", "").ToLower();

        //  Debug.Log($"签名: {signature}");

        // ************* 步骤 4：拼接 Authorization *************
        string authorization = algorithm + " "
            + "Credential=" + secretId + "/" + credentialScope + ", "
            + "SignedHeaders=" + signedHeaders + ", "
            + "Signature=" + signature;

        Debug.Log($"Authorization: {authorization}");

        // 构建头部
        Dictionary<string, string> headers = new Dictionary<string, string>();
        headers.Add("Authorization", authorization);
        headers.Add("Host", endpoint);
        headers.Add("Content-Type", contentType);
        headers.Add("X-TC-Timestamp", requestTimestamp.ToString());
        headers.Add("X-TC-Version", version);
        headers.Add("X-TC-Action", action);
        headers.Add("X-TC-Region", region);

        Debug.Log("=== 最终请求头 ===");
        foreach (var header in headers)
        {
            Debug.Log($"{header.Key}: {header.Value}");
        }
        Debug.Log("================");

        return headers;
    }

    private static string SHA256Hex(string s)
    {
        using (SHA256 algo = SHA256.Create())
        {
            byte[] hashbytes = algo.ComputeHash(Encoding.UTF8.GetBytes(s));
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < hashbytes.Length; ++i)
            {
                builder.Append(hashbytes[i].ToString("x2"));
            }
            return builder.ToString();
        }
    }

    private static byte[] HmacSHA256(byte[] key, byte[] msg)
    {
        using (HMACSHA256 mac = new HMACSHA256(key))
        {
            return mac.ComputeHash(msg);
        }
    }

    private string GetTencentLanguageCode(string language)
    {
        var languageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "en",
            ["zh"] = "zh",
            ["zh-CN"] = "zh",
            ["zh-TW"] = "zh-TW",
            ["ja"] = "ja",
            ["ko"] = "ko",
            ["fr"] = "fr",
            ["es"] = "es",
            ["de"] = "de",
            ["it"] = "it",
            ["ru"] = "ru",
            ["pt"] = "pt",
            ["ar"] = "ar",
            ["hi"] = "hi",
            ["th"] = "th",
            ["vi"] = "vi"
        };
        return languageMap.TryGetValue(language, out var code) ? code : language;
    }

    private string CleanTranslationResult(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return text.Trim('"', '\'', ' ').Replace("\\\"", "\"").Replace("\\n", "\n");
    }

    public void SetCredentials(string newSecretId, string newSecretKey, string newRegion = null)
    {
        secretId = newSecretId;
        secretKey = newSecretKey;
        if (!string.IsNullOrEmpty(newRegion))
            region = newRegion;
    }
}