# volcengine-sdk-c#

字节跳动 火山引擎 机器翻译调用 C#版
参考
https://github.com/volcengine/volc-sdk-java

region
- cn-north-1 (默认)
- ap-singapore-1
- us-east-1

# 字节跳动最近发布了火山引擎，支持机器翻译
官方的文档给出了Python，Go的示例
[https://www.volcengine.com/docs/4640/65067](https://www.volcengine.com/docs/4640/65067)
同时在github开源了java版的sdk
[https://github.com/volcengine/volc-sdk-java](https://github.com/volcengine/volc-sdk-java)


## 既然没有.NET版本的实现，我用C#来做一个实现
官方文档并没有对细节做过多说明，全部都封装在SDK中，因此需要从头做起

经过整理，得出以下的流程：（放大看）
![在这里插入图片描述](https://img-blog.csdnimg.cn/20210618181742524.png?x-oss-process=image/watermark,type_ZmFuZ3poZW5naGVpdGk,shadow_10,text_aHR0cHM6Ly9ibG9nLmNzZG4ubmV0L2RhcmtfMjAwMQ==,size_16,color_FFFFFF,t_70#pic_center)

- 首先注册并开通火山引擎账号，得到APIKey和APISecret

- 先将要翻译的内容和语言方向结合成json
```
{
"SourceLanguage":"en"
,"TargetLanguage":"zh",
"TextList":["Hello World"]
}
```
顺便将这个json SHA256处理 RequestJsonHash
```
c10bf741ac14393bec67f6a6f44163915ae6982c4e1bd5ebbf377ca2f5d29ea0
```
- 获取一个系统时间，UTC格式
分别处理为日期和时间，再结合成日期T时间Z 格式 （典型的Trados风格）
```
dateTimeSign = DateTime.UtcNow
dateSign = ToString("yyyyMMdd")
//20210618
timeSign =ToString("hhmmss")
//092822
//Merge
dateSign  + "T" + timeSign + "Z"
//20210618T092822Z
```
- 准备request Header
-- URI `HTTP://open.volcengineapi.com/?Action=TranslateText&Version=2020-06-01`
-- ContentType = "application/json"
-- Accept = "application/json"
--HOST = "open.volcengineapi.com"
--UserAgent = "volc-sdk-java/v1.0.16"
--X-Date = 上一步的组合日期时间 //20210618T092822Z
--X-Content-Sha256 = 上一步json的RequestJsonHash
--最后Authorization用下面的方法做
- Authorization 使用hmac-sha256 生成，因此需要一个string和一个byte[] 做key
--string
string来自于字符串的拼接组合，content-type;host;x-content-sha256;x-date 加上RequestJsonHash等等进行一系列复杂组合
```
POST
/
Action=TranslateText&Version=2020-06-01
content-type:application/json
host:open.volcengineapi.com
x-content-sha256:c10bf741ac14393bec67f6a6f44163915ae6982c4e1bd5ebbf377ca2f5d29ea0
x-date:20210618T092822Z
content-type;host;x-content-sha256;x-date
c10bf741ac14393bec67f6a6f44163915ae6982c4e1bd5ebbf377ca2f5d29ea0
```
之后进行sha256
```
e63964174aa1b21ec105371eb64e7a88b708ff02f751226581166fe1ebf9c34d
```
再和时间日期等进一步组合
```
stringToSign
HMAC-SHA256
20210618T092822Z
20210618/cn-north-1/translate/request
e63964174aa1b21ec105371eb64e7a88b708ff02f751226581166fe1ebf9c34d
```
--key 
1. 首先日期运算```kDate = hmacsha256(NowDate, APISecret)```
2. 日期运算的结果进入下一步```kRegion = hmacsha256("cn-north-1", kDate)```
3. 运算的结果进入再下一步```kService = hmacsha256("translate", kRegion )```
4. 运算的结果进入再下一步```signingKey = hmacsha256("request", kService )```

最后signingKey 和stringToSign 进行hmacsha256
```
Signature=682ab697257b1e17089f8100119b4c26c3bb71b7257c303b32cb39beba596bec
```
组合为Header
```
HMAC-SHA256 Credential=xxxxxxxxxxxxx/20210618/cn-north-1/translate/request, SignedHeaders=content-type;host;x-content-sha256;x-date, Signature=682ab697257b1e17089f8100119b4c26c3bb71b7257c303b32cb39beba596bec
```

加入request后和json一起发送给HTTP://open.volcengineapi.com/?Action=TranslateText&Version=2020-06-01
得到 repond
```json
{"TranslationList":[{"Translation":"世界你好","DetectedSourceLanguage":"","Extra":null}],"ResponseMetadata":{"RequestId":"02162401024121600000000000000000000ffff0ac264104e27ea","Action":"TranslateText","Version":"2020-06-01","Service":"translate","Region":"cn-north-1"}}

//不应该是*你好世界*吗？
```
最后本文中的源代码在
[https://github.com/Dark-20001/volcengine-sdk-c-](https://github.com/Dark-20001/volcengine-sdk-c-)


