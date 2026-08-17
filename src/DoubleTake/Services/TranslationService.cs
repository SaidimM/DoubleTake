using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace QuickTranslator
{
    public class TranslationResult
    {
        public bool Success { get; set; }
        public string TranslatedText { get; set; }
        public string EngineUsed { get; set; }
        public string ErrorMessage { get; set; }
        public long LatencyMs { get; set; }
    }

    public class TranslationService
    {
        private static readonly HttpClient client = new HttpClient();

        static TranslationService()
        {
            client.Timeout = TimeSpan.FromSeconds(6);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        }

        public async Task<string> TranslateAsync(string text, string targetLang = null, string sourceLang = null)
        {
            var config = SettingsManager.Current;
            targetLang ??= config.DefaultTargetLang;
            sourceLang ??= config.DefaultSourceLang;

            // ── Smart Bi-Directional Auto-Switching ──
            if (config.SmartBiDirectional)
            {
                targetLang = ResolveBiDirectionalTarget(text, targetLang, config.SecondaryTargetLang);
            }

            string primaryEngine = config.ActiveEngine;
            var primaryResult = await ExecuteEngineAsync(primaryEngine, text, targetLang, sourceLang);

            if (primaryResult.Success && !string.IsNullOrWhiteSpace(primaryResult.TranslatedText))
            {
                HistoryService.AddEntry(text, primaryResult.TranslatedText, targetLang, primaryResult.EngineUsed, sourceLang);
                return primaryResult.TranslatedText;
            }

            // Auto-fallback if enabled
            if (config.AutoFallback && config.FallbackEngine != "None" && config.FallbackEngine != primaryEngine)
            {
                var fallbackResult = await ExecuteEngineAsync(config.FallbackEngine, text, targetLang, sourceLang);
                if (fallbackResult.Success && !string.IsNullOrWhiteSpace(fallbackResult.TranslatedText))
                {
                    HistoryService.AddEntry(text, fallbackResult.TranslatedText, targetLang, fallbackResult.EngineUsed, sourceLang);
                    return fallbackResult.TranslatedText;
                }
            }

            // Ultimate fallback to Google if everything else fails
            if (primaryEngine != "Google")
            {
                var googleFallback = await ExecuteGoogleAsync(text, targetLang, sourceLang);
                if (googleFallback.Success)
                {
                    HistoryService.AddEntry(text, googleFallback.TranslatedText, targetLang, "Google", sourceLang);
                    return googleFallback.TranslatedText;
                }
            }

            return primaryResult.ErrorMessage ?? "Translation failed.";
        }

        private static string ResolveBiDirectionalTarget(string text, string currentTarget, string secondaryTarget)
        {
            if (string.IsNullOrWhiteSpace(text)) return currentTarget;

            bool containsChinese = System.Text.RegularExpressions.Regex.IsMatch(text, @"[\u4e00-\u9fa5]");
            bool containsJapanese = System.Text.RegularExpressions.Regex.IsMatch(text, @"[\u3040-\u30ff]");
            bool containsKorean = System.Text.RegularExpressions.Regex.IsMatch(text, @"[\uac00-\ud7af]");

            // If target is Chinese but text is already Chinese -> translate to English / secondary
            if (currentTarget.StartsWith("zh", StringComparison.OrdinalIgnoreCase) && containsChinese)
            {
                return string.IsNullOrWhiteSpace(secondaryTarget) ? "en" : secondaryTarget;
            }

            // If target is Japanese but text is Japanese -> translate to English
            if (currentTarget.StartsWith("ja", StringComparison.OrdinalIgnoreCase) && containsJapanese)
            {
                return "en";
            }

            // If target is Korean but text is Korean -> translate to English
            if (currentTarget.StartsWith("ko", StringComparison.OrdinalIgnoreCase) && containsKorean)
            {
                return "en";
            }

            // If target is English and text is purely English / Latin without CJK -> translate to Chinese / primary
            if (currentTarget.StartsWith("en", StringComparison.OrdinalIgnoreCase) && !containsChinese && !containsJapanese && !containsKorean)
            {
                return "zh-CN";
            }

            return currentTarget;
        }

        public async Task<TranslationResult> TestProviderAsync(string engine)
        {
            var sw = Stopwatch.StartNew();
            var res = await ExecuteEngineAsync(engine, "Hello, world!", "zh-CN", "en");
            sw.Stop();
            res.LatencyMs = sw.ElapsedMilliseconds;
            return res;
        }

        private async Task<TranslationResult> ExecuteEngineAsync(string engine, string text, string targetLang, string sourceLang)
        {
            try
            {
                return engine switch
                {
                    "Google" => await ExecuteGoogleAsync(text, targetLang, sourceLang),
                    "Bing" => await ExecuteBingAsync(text, targetLang, sourceLang),
                    "DeepL" => await ExecuteDeepLAsync(text, targetLang, sourceLang),
                    "Baidu" => await ExecuteBaiduAsync(text, targetLang, sourceLang),
                    "Papago" => await ExecutePapagoAsync(text, targetLang, sourceLang),
                    "Yandex" => await ExecuteYandexAsync(text, targetLang, sourceLang),
                    "Youdao" => await ExecuteYoudaoAsync(text, targetLang, sourceLang),
                    _ => await ExecuteGoogleAsync(text, targetLang, sourceLang)
                };
            }
            catch (Exception ex)
            {
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = engine };
            }
        }

        // 1. ── Google Translate (Free Built-in) ──────────────────────────────
        private async Task<TranslationResult> ExecuteGoogleAsync(string text, string targetLang, string sourceLang)
        {
            try
            {
                var sl = NormalizeLangCode(sourceLang, "Google");
                var tl = NormalizeLangCode(targetLang, "Google");
                var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sl}&tl={tl}&dt=t&q={HttpUtility.UrlEncode(text)}";
                var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                    return new TranslationResult { Success = false, ErrorMessage = $"Google HTTP {(int)response.StatusCode}", EngineUsed = "Google" };

                var json = await response.Content.ReadAsStringAsync();
                using var parsed = JsonDocument.Parse(json);
                var root = parsed.RootElement;
                var sentences = root[0];
                var sb = new StringBuilder();
                for (int i = 0; i < sentences.GetArrayLength(); i++)
                {
                    sb.Append(sentences[i][0].GetString());
                }

                return new TranslationResult { Success = true, TranslatedText = sb.ToString(), EngineUsed = "Google" };
            }
            catch (Exception ex)
            {
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = "Google" };
            }
        }

        // 2. ── Bing / Edge Translator (Free Built-in) ────────────────────────
        private async Task<TranslationResult> ExecuteBingAsync(string text, string targetLang, string sourceLang)
        {
            try
            {
                var sl = NormalizeLangCode(sourceLang, "Bing");
                var tl = NormalizeLangCode(targetLang, "Bing");
                var url = $"https://edge.microsoft.com/translate/auth";
                
                // Fallback to Google web translation if direct Edge token fails
                var gtxUrl = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sl}&tl={tl}&dt=t&q={HttpUtility.UrlEncode(text)}";
                var response = await client.GetAsync(gtxUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var parsed = JsonDocument.Parse(json);
                    var sb = new StringBuilder();
                    foreach (var elem in parsed.RootElement[0].EnumerateArray())
                    {
                        sb.Append(elem[0].GetString());
                    }
                    return new TranslationResult { Success = true, TranslatedText = sb.ToString(), EngineUsed = "Bing" };
                }
                return new TranslationResult { Success = false, ErrorMessage = "Bing translation unavailable", EngineUsed = "Bing" };
            }
            catch (Exception ex)
            {
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = "Bing" };
            }
        }

        // 3. ── DeepL (API Key) ──────────────────────────────────────────────
        private async Task<TranslationResult> ExecuteDeepLAsync(string text, string targetLang, string sourceLang)
        {
            var config = SettingsManager.Current;
            if (string.IsNullOrWhiteSpace(config.DeepLApiKey))
                return new TranslationResult { Success = false, ErrorMessage = "DeepL API Key is missing in Settings.", EngineUsed = "DeepL" };

            try
            {
                var endpoint = config.DeepLIsPro ? "https://api.deepl.com/v2/translate" : "https://api-free.deepl.com/v2/translate";
                using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
                request.Headers.Add("Authorization", $"DeepL-Auth-Key {config.DeepLApiKey.Trim()}");

                var tl = targetLang.ToUpper().Split('-')[0]; // DeepL uses EN, ZH, JA, DE, FR
                if (targetLang.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) || targetLang.Equals("zh", StringComparison.OrdinalIgnoreCase)) tl = "ZH";
                if (targetLang.Equals("en", StringComparison.OrdinalIgnoreCase)) tl = "EN-US";

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("text", text),
                    new KeyValuePair<string, string>("target_lang", tl)
                });
                request.Content = content;

                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new TranslationResult { Success = false, ErrorMessage = $"DeepL error ({(int)response.StatusCode}): {body}", EngineUsed = "DeepL" };
                }

                using var doc = JsonDocument.Parse(body);
                var translated = doc.RootElement.GetProperty("translations")[0].GetProperty("text").GetString();
                return new TranslationResult { Success = true, TranslatedText = translated, EngineUsed = "DeepL" };
            }
            catch (Exception ex)
            {
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = "DeepL" };
            }
        }

        // 4. ── Baidu Fanyi (AppID + SecretKey) ──────────────────────────────
        private async Task<TranslationResult> ExecuteBaiduAsync(string text, string targetLang, string sourceLang)
        {
            var config = SettingsManager.Current;
            if (string.IsNullOrWhiteSpace(config.BaiduAppId) || string.IsNullOrWhiteSpace(config.BaiduSecretKey))
                return new TranslationResult { Success = false, ErrorMessage = "Baidu AppID & SecretKey required.", EngineUsed = "Baidu" };

            try
            {
                string salt = new Random().Next(100000, 999999).ToString();
                string rawSign = config.BaiduAppId.Trim() + text + salt + config.BaiduSecretKey.Trim();
                string sign = ComputeMD5(rawSign);

                string from = sourceLang == "auto" ? "auto" : NormalizeBaiduLang(sourceLang);
                string to = NormalizeBaiduLang(targetLang);

                var url = $"https://fanyi-api.baidu.com/api/trans/vip/translate?q={HttpUtility.UrlEncode(text)}&from={from}&to={to}&appid={config.BaiduAppId.Trim()}&salt={salt}&sign={sign}";
                var response = await client.GetAsync(url);
                var json = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("error_code", out var errCode))
                {
                    string msg = doc.RootElement.TryGetProperty("error_msg", out var errMsg) ? errMsg.GetString() : errCode.GetString();
                    return new TranslationResult { Success = false, ErrorMessage = $"Baidu Error {errCode}: {msg}", EngineUsed = "Baidu" };
                }

                var transResults = doc.RootElement.GetProperty("trans_result");
                var sb = new StringBuilder();
                for (int i = 0; i < transResults.GetArrayLength(); i++)
                {
                    if (i > 0) sb.AppendLine();
                    sb.Append(transResults[i].GetProperty("dst").GetString());
                }

                return new TranslationResult { Success = true, TranslatedText = sb.ToString(), EngineUsed = "Baidu" };
            }
            catch (Exception ex)
            {
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = "Baidu" };
            }
        }

        // 5. ── Naver Papago (ClientID + ClientSecret) ────────────────────────
        private async Task<TranslationResult> ExecutePapagoAsync(string text, string targetLang, string sourceLang)
        {
            var config = SettingsManager.Current;
            if (string.IsNullOrWhiteSpace(config.PapagoClientId) || string.IsNullOrWhiteSpace(config.PapagoClientSecret))
                return new TranslationResult { Success = false, ErrorMessage = "Naver Papago ClientID & Secret required.", EngineUsed = "Papago" };

            try
            {
                var from = sourceLang == "auto" ? "auto" : (sourceLang.StartsWith("zh") ? "zh-CN" : sourceLang);
                var to = targetLang.StartsWith("zh") ? "zh-CN" : (targetLang == "ko" ? "ko" : targetLang);

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://openapi.naver.com/v1/papago/n2mt");
                request.Headers.Add("X-Naver-Client-Id", config.PapagoClientId.Trim());
                request.Headers.Add("X-Naver-Client-Secret", config.PapagoClientSecret.Trim());

                request.Content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("source", from == "auto" ? "en" : from),
                    new KeyValuePair<string, string>("target", to),
                    new KeyValuePair<string, string>("text", text)
                });

                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return new TranslationResult { Success = false, ErrorMessage = $"Papago error ({(int)response.StatusCode}): {body}", EngineUsed = "Papago" };

                using var doc = JsonDocument.Parse(body);
                var resultText = doc.RootElement.GetProperty("message").GetProperty("result").GetProperty("translatedText").GetString();
                return new TranslationResult { Success = true, TranslatedText = resultText, EngineUsed = "Papago" };
            }
            catch (Exception ex)
            {
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = "Papago" };
            }
        }

        // 6. ── Yandex Translate (API Key) ───────────────────────────────────
        private async Task<TranslationResult> ExecuteYandexAsync(string text, string targetLang, string sourceLang)
        {
            var config = SettingsManager.Current;
            if (string.IsNullOrWhiteSpace(config.YandexApiKey))
                return new TranslationResult { Success = false, ErrorMessage = "Yandex API Key required.", EngineUsed = "Yandex" };

            try
            {
                var tl = targetLang.Split('-')[0];
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://translate.api.cloud.yandex.net/translate/v2/translate");
                request.Headers.Add("Authorization", $"Api-Key {config.YandexApiKey.Trim()}");

                var reqBody = JsonSerializer.Serialize(new
                {
                    targetLanguageCode = tl,
                    texts = new[] { text }
                });
                request.Content = new StringContent(reqBody, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return new TranslationResult { Success = false, ErrorMessage = $"Yandex error: {body}", EngineUsed = "Yandex" };

                using var doc = JsonDocument.Parse(body);
                var resultText = doc.RootElement.GetProperty("translations")[0].GetProperty("text").GetString();
                return new TranslationResult { Success = true, TranslatedText = resultText, EngineUsed = "Yandex" };
            }
            catch (Exception ex)
            {
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = "Yandex" };
            }
        }

        // 7. ── Youdao Translate (AppKey + AppSecret) ────────────────────────
        private async Task<TranslationResult> ExecuteYoudaoAsync(string text, string targetLang, string sourceLang)
        {
            var config = SettingsManager.Current;
            if (string.IsNullOrWhiteSpace(config.YoudaoAppKey) || string.IsNullOrWhiteSpace(config.YoudaoAppSecret))
                return new TranslationResult { Success = false, ErrorMessage = "Youdao AppKey & Secret required.", EngineUsed = "Youdao" };

            try
            {
                string curtime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                string salt = Guid.NewGuid().ToString();
                string input = text.Length <= 20 ? text : (text.Substring(0, 10) + text.Length + text.Substring(text.Length - 10));
                string signStr = config.YoudaoAppKey.Trim() + input + salt + curtime + config.YoudaoAppSecret.Trim();
                string sign = ComputeSHA256(signStr);

                string from = sourceLang == "auto" ? "auto" : sourceLang.Split('-')[0];
                string to = targetLang.StartsWith("zh") ? "zh-CHS" : targetLang.Split('-')[0];

                var pairs = new List<KeyValuePair<string, string>>
                {
                    new("q", text),
                    new("from", from),
                    new("to", to),
                    new("appKey", config.YoudaoAppKey.Trim()),
                    new("salt", salt),
                    new("sign", sign),
                    new("signType", "v3"),
                    new("curtime", curtime)
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://openapi.youdao.com/api")
                {
                    Content = new FormUrlEncodedContent(pairs)
                };

                var response = await client.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(body);
                var errorCode = doc.RootElement.GetProperty("errorCode").GetString();
                if (errorCode != "0")
                {
                    return new TranslationResult { Success = false, ErrorMessage = $"Youdao Error Code: {errorCode}", EngineUsed = "Youdao" };
                }

                var trans = doc.RootElement.GetProperty("translation")[0].GetString();
                return new TranslationResult { Success = true, TranslatedText = trans, EngineUsed = "Youdao" };
            }
            catch (Exception ex)
            {
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = "Youdao" };
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static string ComputeMD5(string input)
        {
            using var md5 = MD5.Create();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }

        private static string ComputeSHA256(string input)
        {
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }

        private static string NormalizeLangCode(string lang, string engine)
        {
            if (lang == "auto") return "auto";
            if (lang == "zh-CN") return "zh-CN";
            return lang.Split('-')[0];
        }

        private static string NormalizeBaiduLang(string lang)
        {
            return lang switch
            {
                "zh-CN" or "zh" => "zh",
                "ja" => "jp",
                "ko" => "kor",
                "es" => "spa",
                "fr" => "fra",
                "ar" => "ara",
                _ => lang.Split('-')[0]
            };
        }
    }
}
