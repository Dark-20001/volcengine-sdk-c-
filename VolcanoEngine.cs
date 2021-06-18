using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace APITest
{
    public class VolcanoEngine
    {
        public string Host { get; set; }
        public string URL { get; set; }
        public string APIKey { get; set; }
        public string APISecret { get; set; }

        const string Service = "translate";
        const string Version = "1.0.16";
        const string Region = "cn-north-1";
        const string UrlQuery = "Action=TranslateText&Version=2020-06-01";
        const string Algorithm = "HMAC-SHA256";


        List<string> H_INCLUDE;

        string NowDate;
        string NowTime;
        string dateTimeSignStr;

        public VolcanoEngine()
        { }
        public VolcanoEngine(string apikey, string apisecret)
        {
            this.Host = "open.volcengineapi.com";
            this.APIKey = apikey;
            this.APISecret = apisecret;
            this.URL = "HTTP://" + Host+ "/?"+ UrlQuery;

            H_INCLUDE = new List<string>();
            H_INCLUDE.Add("Content-Type");
            H_INCLUDE.Add("Content-Md5");
            H_INCLUDE.Add("Host");
        }

        public string translateText(string text, string sourceLanguage, string targetLanguage)
        {
            string requestBody = BuildRequestJson(text, sourceLanguage, targetLanguage);

            DateTime dateTimeSign = DateTime.UtcNow;
            NowDate = dateTimeSign.ToString("yyyyMMdd");
            NowTime = dateTimeSign.ToString("hhmmss");
            dateTimeSignStr  = NowDate + "T" + NowTime + "Z";

            //debug
            //dateTimeSignStr = "20210618T064729Z";

            //send request
            string translated = sendRequest(requestBody);

            //parse json
            translated = connectStrings(ParseResponseJson(translated));

            return translated;
        }

        protected static string ComputeHash256(string input, HashAlgorithm algorithm)
        {
            Byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            Byte[] hashedBytes = algorithm.ComputeHash(inputBytes);

            return ToHexString(hashedBytes);
        }


        private static byte[] hmacsha256(string text, byte[] secret)
        {
            //string signRet = string.Empty;
            byte[] hash;
            using (HMACSHA256 mac = new HMACSHA256(secret))
            {
                hash = mac.ComputeHash(Encoding.UTF8.GetBytes(text));
               // signRet = Convert.ToBase64String(hash);
            }
            return hash;

        }

        /*
         * Example
        {
            "SourceLanguage": "en"
            "TargetLanguage": "zh",
            "TextList": [
                "Hello world"
            ]
        }
        */
        private string BuildRequestJson(string text, string sourceLanguage, string TargetLanguage)
        {
            VolcanoRequest volcanoRequest = new VolcanoRequest();
            volcanoRequest.SourceLanguage = sourceLanguage;
            volcanoRequest.TargetLanguage = TargetLanguage;
            volcanoRequest.TextList = new string[] { text };
            return JsonConvert.SerializeObject(volcanoRequest);
        }

        private string BuildRequestJson(string[] text, string sourceLanguage, string TargetLanguage)
        {
            VolcanoRequest volcanoRequest = new VolcanoRequest();
            volcanoRequest.SourceLanguage = sourceLanguage;
            volcanoRequest.TargetLanguage = TargetLanguage;
            volcanoRequest.TextList = text;
            return JsonConvert.SerializeObject(volcanoRequest);
        }


        /*
         {
            "TranslationList": [
                {
                    "Translation": "你好世界",
                    "DetectedSourceLanguage": "en"
                }
            ],
            "ResponseMetadata": {
                "RequestId": "202004092306480100140440781F5D7119",
                "Action": "TranslateText",
                "Version": "2020-06-01",
                "Service": "translate",
                "Region": "cn-north-1",
                "Error": null
            }
        } 
         *
         */
        private string[] ParseResponseJson(string jsonstring)
        {
            dynamic TempResult = JsonConvert.DeserializeObject(jsonstring);
            JArray TranslationList = TempResult["TranslationList"];

            string[] Translations = new string[TranslationList.Count];
            for (int i = 0; i < TranslationList.Count; i++)
            {
                Translations[i] = Convert.ToString(TranslationList[i]["Translation"]);
            }
            return Translations;
        }

        private string connectStrings(string[] text)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                builder.Append(text[i]);
                builder.Append("\n");
            }

            string result = builder.ToString();
            result = result.Substring(0, result.Length - 1);
            return result;
        }

        private string sendRequest(string requestBody)
        {
            
            //[Content-Type: text/plain; charset=UTF-8, Content-Length: 86, Chunked: false]

        HttpWebRequest request = null;
            if (URL.ToLower().StartsWith("https", StringComparison.OrdinalIgnoreCase))
            {
                request = WebRequest.Create(URL) as HttpWebRequest;
                ServicePointManager.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(CheckValidationResult);
                request.ProtocolVersion = HttpVersion.Version11;
                // 这里设置了协议类型。
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;// SecurityProtocolType.Tls1.2; 
                request.KeepAlive = false;
                ServicePointManager.CheckCertificateRevocationList = true;
                ServicePointManager.DefaultConnectionLimit = 100;
                ServicePointManager.Expect100Continue = false;
            }
            else
            {
                request = (HttpWebRequest)WebRequest.Create(URL);
            }

            byte[] byteArray = Encoding.UTF8.GetBytes(requestBody);
            //sign
            string bodyHash = ComputeHash256(requestBody, new SHA256CryptoServiceProvider());


            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = byteArray.Length;
            request.Accept = "application/json";

            request.Host = Host;
            request.UserAgent = "volc-sdk-java/v" + Version;
            //request.Date = dateTime;  X-Date: 20210616T024810Z
            request.Headers.Add("X-Date", dateTimeSignStr);


            request.Headers.Add("X-Content-Sha256", bodyHash);

            List<string> signedHeaders = new List<string>();

            for(int i=0;i< request.Headers.Count; i++)
            {
                string headerName = request.Headers.GetKey(i);

                if (H_INCLUDE.Contains(headerName) || headerName.StartsWith("X-"))
                {
                    signedHeaders.Add(headerName.ToLower());
                }
            }
            signedHeaders.Add("host");
            signedHeaders.Sort();
            StringBuilder signedHeadersToSignStr = new StringBuilder();

            string headerValue;
            foreach (string signedHeader in signedHeaders)
            {
                if (signedHeader.Equals("host"))
                {
                    headerValue = Host;
                }
                else
                {
                    headerValue = request.Headers.Get(signedHeader).Trim();
                }
                 
                signedHeadersToSignStr.Append(signedHeader).Append(":").Append(headerValue).Append("\n");
            }

            string signedHeadersStr = JoinString(signedHeaders.ToArray(), ";");

            string canonicalRequest = JoinString(new string[] {
                request.Method,
                "/",
                UrlQuery,
                signedHeadersToSignStr.ToString(),
                signedHeadersStr,
                bodyHash
                },
                "\n");
            //step 1
            string hashedCanonReq = ComputeHash256(canonicalRequest, new SHA256CryptoServiceProvider());
            //step 2
            String stringToSign = Algorithm + "\n" + 
                                    dateTimeSignStr + "\n" + 
                                    JoinString(
                                    new string[] {
                                        NowDate,
                                        Region,
                                        Service,
                                        "request"
                                    }, 
                                    "/") + "\n" +
                                    hashedCanonReq
                                    ;
            //step 3
            //String secretKey, String date, String region, String service
            byte[] kDate = hmacsha256(NowDate, Encoding.UTF8.GetBytes(APISecret));
            byte[] kRegion = hmacsha256(Region, kDate);
            byte[] kService = hmacsha256(Service, kRegion);
            byte[] signingKey = hmacsha256("request", kService);       

            byte[] signature = hmacsha256(stringToSign, signingKey);

            string AuthHeader = Algorithm + " Credential=" + APIKey + "/" +NowDate + "/" + Region+"/"+Service+"/request"+
                ", SignedHeaders=" + signedHeadersStr +
                ", Signature=" + ToHexString(signature);

            request.Headers.Add("Authorization", AuthHeader);

            request.KeepAlive = false;


            //send
            int respondCode = 0;
            string respondStr = string.Empty;

            try
            {
                using (Stream reqStream = request.GetRequestStream())
                {
                    reqStream.Write(byteArray, 0, byteArray.Length);
                }

                using (HttpWebResponse webResponse = (HttpWebResponse)request.GetResponse())
                {
                    respondCode = (int)webResponse.StatusCode;
                    if (respondCode == 200)
                    {
                        using (StreamReader sr = new StreamReader(webResponse.GetResponseStream(), Encoding.UTF8))
                        {
                            respondStr = sr.ReadToEnd();
                        }

                        //sw.WriteLine("result");                     

                    }

                }

                return respondStr;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return null;
            }
        }

        private static bool CheckValidationResult(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
        }

        private string JoinString(string[] strings, string seperator)
        {
            StringBuilder builder = new StringBuilder();
            foreach (string s in strings)
            {
                builder.Append(s).Append(seperator);
            }
            builder.Remove(builder.Length - 1, 1);
            return builder.ToString();
        }

        public static string ToHexString(byte[] bytes) // 0xae00cf => "AE00CF "
        {
            string hexString = string.Empty;
            if (bytes != null)
            {
                StringBuilder strB = new StringBuilder();

                for (int i = 0; i < bytes.Length; i++)
                {
                    strB.Append(bytes[i].ToString("X2"));
                }
                hexString = strB.ToString();
            }
            return hexString.ToLower();
        }
    }

    public class VolcanoRequest
    {
        [JsonProperty("SourceLanguage")]
        public string SourceLanguage { get; set; }

        [JsonProperty("TargetLanguage")]
        public string TargetLanguage { get; set; }

        [JsonProperty("TextList")]
        public string[] TextList { get; set; }
    }

}
