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
        public event Action<string> EngineStatusChanged;

        private static readonly HttpClient client;

        static TranslationService()
        {
            var handler = new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                EnableMultipleHttp2Connections = true
            };
            client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(6)
            };
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            // Pre-warm Bing authentication in background on startup and keep refreshed
            _ = Task.Run(async () =>
            {
                while (true)
                {
                    try { await EnsureBingAuthAsync(System.Threading.CancellationToken.None); }
                    catch { }
                    await Task.Delay(TimeSpan.FromMinutes(30));
                }
            });
        }

        public async Task<string> TranslateAsync(string text, string targetLang = null, string sourceLang = null, string engine = null, System.Threading.CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var config = SettingsManager.Current;
            targetLang ??= config.DefaultTargetLang;
            sourceLang ??= config.DefaultSourceLang;

            string primaryEngine = !string.IsNullOrWhiteSpace(engine) ? engine : config.ActiveEngine;
            ReportEngineStatus($"{primaryEngine} Engine · Translating…");
            var primaryResult = await ExecuteEngineAsync(primaryEngine, text, targetLang, sourceLang, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            if (primaryResult.Success && !string.IsNullOrWhiteSpace(primaryResult.TranslatedText))
            {
                HistoryService.AddEntry(text, primaryResult.TranslatedText, targetLang, primaryResult.EngineUsed, sourceLang);
                ReportEngineStatus($"{primaryResult.EngineUsed} Engine · result");
                return primaryResult.TranslatedText;
            }

            // Auto-fallback if enabled
            if (config.AutoFallback && config.FallbackEngine != "None" && config.FallbackEngine != primaryEngine)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReportEngineStatus($"{primaryEngine} failed · trying {config.FallbackEngine}…");
                var fallbackResult = await ExecuteEngineAsync(config.FallbackEngine, text, targetLang, sourceLang, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (fallbackResult.Success && !string.IsNullOrWhiteSpace(fallbackResult.TranslatedText))
                {
                    HistoryService.AddEntry(text, fallbackResult.TranslatedText, targetLang, fallbackResult.EngineUsed, sourceLang);
                    ReportEngineStatus($"{fallbackResult.EngineUsed} Engine · fallback result");
                    return fallbackResult.TranslatedText;
                }
            }

            // Ultimate fallback to Google if everything else fails
            if (primaryEngine != "Google")
            {
                cancellationToken.ThrowIfCancellationRequested();
                ReportEngineStatus($"{primaryEngine} failed · trying Google…");
                var googleFallback = await ExecuteGoogleAsync(text, targetLang, sourceLang, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (googleFallback.Success && !string.IsNullOrWhiteSpace(googleFallback.TranslatedText))
                {
                    HistoryService.AddEntry(text, googleFallback.TranslatedText, targetLang, "Google", sourceLang);
                    ReportEngineStatus("Google Engine · fallback result");
                    return googleFallback.TranslatedText;
                }
            }

            return primaryResult.ErrorMessage ?? "Translation failed.";
        }

        public async Task<TranslationResult> TestProviderAsync(string engine)
        {
            var sw = Stopwatch.StartNew();
            var res = await ExecuteEngineAsync(engine, "Hello, world!", "zh-CN", "en");
            sw.Stop();
            res.LatencyMs = sw.ElapsedMilliseconds;
            return res;
        }

        private async Task<TranslationResult> ExecuteEngineAsync(string engine, string text, string targetLang, string sourceLang, System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return engine switch
                {
                    "Google" => await ExecuteGoogleAsync(text, targetLang, sourceLang, cancellationToken),
                    "Bing" => await ExecuteBingAsync(text, targetLang, sourceLang, cancellationToken),
                    "DeepL" => await ExecuteDeepLAsync(text, targetLang, sourceLang, cancellationToken),
                    "Baidu" => await ExecuteBaiduAsync(text, targetLang, sourceLang, cancellationToken),
                    "Papago" => await ExecutePapagoAsync(text, targetLang, sourceLang, cancellationToken),
                    "Yandex" => await ExecuteYandexAsync(text, targetLang, sourceLang, cancellationToken),
                    "Youdao" => await ExecuteYoudaoAsync(text, targetLang, sourceLang, cancellationToken),
                    _ => await ExecuteGoogleAsync(text, targetLang, sourceLang, cancellationToken)
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(cancellationToken);

                string message = ex.Message;
                if (ex is TaskCanceledException || ex is TimeoutException || message.Contains("Timeout") || message.Contains("canceled"))
                {
                    message = "Translation request timed out. Please check your network connection.";
                }
                return new TranslationResult { Success = false, ErrorMessage = message, EngineUsed = engine };
            }
        }

        // 1. ── Google Translate (Free Built-in) ──────────────────────────────
        private async Task<TranslationResult> ExecuteGoogleAsync(string text, string targetLang, string sourceLang, System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sl = NormalizeLangCode(sourceLang, "Google");
                var tl = NormalizeLangCode(targetLang, "Google");
                var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sl}&tl={tl}&dt=t&q={HttpUtility.UrlEncode(text)}";
                var response = await client.GetAsync(url, cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return new TranslationResult { Success = false, ErrorMessage = $"Google HTTP {(int)response.StatusCode}", EngineUsed = "Google" };

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var parsed = JsonDocument.Parse(json);
                var root = parsed.RootElement;
                var sentences = root[0];
                var sb = new StringBuilder();
                for (int i = 0; i < sentences.GetArrayLength(); i++)
                {
                    var sentenceElem = sentences[i];
                    if (sentenceElem.ValueKind == JsonValueKind.Array && sentenceElem.GetArrayLength() > 0)
                    {
                        var val = sentenceElem[0].GetString();
                        if (!string.IsNullOrEmpty(val))
                            sb.Append(val);
                    }
                }

                string translated = sb.ToString();
                if (string.IsNullOrWhiteSpace(translated))
                    return new TranslationResult { Success = false, ErrorMessage = "Empty translation result", EngineUsed = "Google" };

                return new TranslationResult { Success = true, TranslatedText = translated, EngineUsed = "Google" };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = "Google" };
            }
        }

        // 2. ── Bing / Edge Translator (Free Built-in) ────────────────────────
        private static string _bingIg;
        private static string _bingIid;
        private static string _bingKey;
        private static string _bingToken;
        private static string _bingHost = "www.bing.com";
        private static DateTime _bingTokenExpiry = DateTime.MinValue;
        private static readonly System.Threading.SemaphoreSlim _bingLock = new System.Threading.SemaphoreSlim(1, 1);

        private static async Task EnsureBingAuthAsync(System.Threading.CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(_bingKey) && !string.IsNullOrEmpty(_bingToken) && DateTime.UtcNow < _bingTokenExpiry)
                return;

            using var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            try
            {
                await _bingLock.WaitAsync(cts.Token);
            }
            catch { return; }

            try
            {
                if (!string.IsNullOrEmpty(_bingKey) && !string.IsNullOrEmpty(_bingToken) && DateTime.UtcNow < _bingTokenExpiry)
                    return;

                using var req = new HttpRequestMessage(HttpMethod.Get, "https://www.bing.com/translator");
                AddBingRequestHeaders(req);

                var resp = await client.SendAsync(req, cts.Token);
                var html = await resp.Content.ReadAsStringAsync(cts.Token);
                LogBingResponse("auth", resp, html);
                if (!resp.IsSuccessStatusCode) return;
                _bingHost = resp.RequestMessage?.RequestUri?.Host ?? "www.bing.com";

                var igMatch = System.Text.RegularExpressions.Regex.Match(html, @"IG:\s*""([^""\s]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (igMatch.Success) _bingIg = igMatch.Groups[1].Value;

                var iidMatch = System.Text.RegularExpressions.Regex.Match(html, @"data-iid\s*=\s*""([^""\s]+)""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (iidMatch.Success) _bingIid = iidMatch.Groups[1].Value;

                var abuseMatch = System.Text.RegularExpressions.Regex.Match(html, @"params_AbusePreventionHelper\s*=\s*\[\s*([0-9]+)\s*,\s*\""([^\""]+)\""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (abuseMatch.Success)
                {
                    _bingKey = abuseMatch.Groups[1].Value;
                    _bingToken = abuseMatch.Groups[2].Value;
                    _bingTokenExpiry = DateTime.UtcNow.AddMinutes(45);
                }
            }
            catch { }
            finally
            {
                _bingLock.Release();
            }
        }

        private async Task<TranslationResult> ExecuteBingAsync(string text, string targetLang, string sourceLang, System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sl = NormalizeBingLang(sourceLang);
                var tl = NormalizeBingLang(targetLang);

                await EnsureBingAuthAsync(cancellationToken);

                if (string.IsNullOrEmpty(_bingKey) || string.IsNullOrEmpty(_bingToken))
                {
                    ReportEngineStatus("Bing auth failed · trying Google…");
                    return await ExecuteGoogleAsync(text, targetLang, sourceLang, cancellationToken);
                }

                var url = $"https://{_bingHost}/ttranslatev3?isVertical=1&IG={_bingIg}&IID={_bingIid ?? "translator.5023"}";
                var form = new Dictionary<string, string>
                {
                    { "text", text },
                    { "fromLang", sl },
                    { "to", tl },
                    { "key", _bingKey },
                    { "token", _bingToken }
                };

                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new FormUrlEncodedContent(form)
                };
                AddBingRequestHeaders(req, _bingHost);

                var resp = await client.SendAsync(req, cancellationToken);
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                LogBingResponse("translate", resp, json);
                if (!resp.IsSuccessStatusCode)
                {
                    _bingTokenExpiry = DateTime.MinValue;
                    ReportEngineStatus($"Bing HTTP {(int)resp.StatusCode} · trying Google…");
                    return await ExecuteGoogleAsync(text, targetLang, sourceLang, cancellationToken);
                }

                if (!LooksLikeBingJson(resp, json))
                {
                    _bingTokenExpiry = DateTime.MinValue;
                    ReportEngineStatus("Bing returned a non-JSON response · refreshing auth…");
                    await EnsureBingAuthAsync(cancellationToken);
                    return await ExecuteGoogleAsync(text, targetLang, sourceLang, cancellationToken);
                }

                using var parsed = JsonDocument.Parse(json);
                var root = parsed.RootElement;

                // Check for status code 205 (token expired)
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("statusCode", out var statusProp) && statusProp.GetInt32() == 205)
                {
                    _bingTokenExpiry = DateTime.MinValue;
                    await EnsureBingAuthAsync(cancellationToken);
                    if (!string.IsNullOrEmpty(_bingKey) && !string.IsNullOrEmpty(_bingToken))
                    {
                        var retryUrl = $"https://{_bingHost}/ttranslatev3?isVertical=1&IG={_bingIg}&IID={_bingIid ?? "translator.5023"}";
                        var retryForm = new Dictionary<string, string>
                        {
                            { "text", text },
                            { "fromLang", sl },
                            { "to", tl },
                            { "key", _bingKey },
                            { "token", _bingToken }
                        };
                        using var retryReq = new HttpRequestMessage(HttpMethod.Post, retryUrl) { Content = new FormUrlEncodedContent(retryForm) };
                        AddBingRequestHeaders(retryReq, _bingHost);
                        var retryResp = await client.SendAsync(retryReq, cancellationToken);
                        var retryJson = await retryResp.Content.ReadAsStringAsync(cancellationToken);
                        LogBingResponse("retry", retryResp, retryJson);
                        if (retryResp.IsSuccessStatusCode)
                        {
                            using var retryParsed = JsonDocument.Parse(retryJson);
                            var resultText = ExtractBingText(retryParsed.RootElement);
                            if (!string.IsNullOrWhiteSpace(resultText))
                                return new TranslationResult { Success = true, TranslatedText = resultText, EngineUsed = "Bing" };
                        }
                    }
                    return await ExecuteGoogleAsync(text, targetLang, sourceLang, cancellationToken);
                }

                string translated = ExtractBingText(root);
                if (string.IsNullOrWhiteSpace(translated))
                {
                    ReportEngineStatus("Bing returned no result · trying Google…");
                    return await ExecuteGoogleAsync(text, targetLang, sourceLang, cancellationToken);
                }

                return new TranslationResult { Success = true, TranslatedText = translated, EngineUsed = "Bing" };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                ReportEngineStatus($"Bing error: {ex.Message} · trying Google…");
                return await ExecuteGoogleAsync(text, targetLang, sourceLang, cancellationToken);
            }
        }

        private void ReportEngineStatus(string status)
        {
            try
            {
                QuickTranslator.Helpers.DebugLog.Write($"TranslationService: {status}");
                EngineStatusChanged?.Invoke(status);
            }
            catch { }
        }

        private static void AddBingRequestHeaders(HttpRequestMessage request, string host = "www.bing.com")
        {
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0");
            request.Headers.Referrer = new Uri($"https://{host}/translator");
            request.Headers.Add("Origin", $"https://{host}");
            request.Headers.Accept.ParseAdd("application/json, text/plain, */*");
        }

        private static void LogBingResponse(string stage, HttpResponseMessage response, string body)
        {
            string contentType = response.Content.Headers.ContentType?.ToString() ?? "(none)";
            string preview = string.IsNullOrWhiteSpace(body)
                ? "(empty)"
                : body.Replace("\r", " ").Replace("\n", " ").Trim();
            if (preview.Length > 500)
                preview = preview.Substring(0, 500) + "…";

            QuickTranslator.Helpers.DebugLog.Write(
                $"Bing {stage} response: HTTP {(int)response.StatusCode} {response.ReasonPhrase}; Content-Type={contentType}; Body={preview}");
        }

        private static bool LooksLikeBingJson(HttpResponseMessage response, string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return false;

            string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            string trimmed = body.TrimStart();
            return contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                && (trimmed.StartsWith("[") || trimmed.StartsWith("{"));
        }

        private static string ExtractBingText(JsonElement root)
        {
            try
            {
                if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0)
                {
                    var item = root[0];
                    if (item.TryGetProperty("translations", out var transArr) && transArr.GetArrayLength() > 0)
                    {
                        return transArr[0].GetProperty("text").GetString();
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("translations", out var transArr) && transArr.GetArrayLength() > 0)
                    {
                        return transArr[0].GetProperty("text").GetString();
                    }
                }
            }
            catch { }
            return null;
        }

        private static string NormalizeBingLang(string lang)
        {
            if (string.IsNullOrWhiteSpace(lang) || lang == "auto") return "auto-detect";
            if (lang.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) || lang.Equals("zh", StringComparison.OrdinalIgnoreCase)) return "zh-Hans";
            if (lang.Equals("zh-TW", StringComparison.OrdinalIgnoreCase) || lang.Equals("zh-HK", StringComparison.OrdinalIgnoreCase)) return "zh-Hant";
            return lang.Split('-')[0];
        }

        // 3. ── DeepL (API Key) ──────────────────────────────────────────────
        private async Task<TranslationResult> ExecuteDeepLAsync(string text, string targetLang, string sourceLang, System.Threading.CancellationToken cancellationToken = default)
        {
            var config = SettingsManager.Current;
            if (string.IsNullOrWhiteSpace(config.DeepLApiKey))
                return new TranslationResult { Success = false, ErrorMessage = "DeepL API Key is missing in Settings.", EngineUsed = "DeepL" };

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
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

                var response = await client.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    return new TranslationResult { Success = false, ErrorMessage = $"DeepL error ({(int)response.StatusCode}): {body}", EngineUsed = "DeepL" };
                }

                using var doc = JsonDocument.Parse(body);
                var translated = doc.RootElement.GetProperty("translations")[0].GetProperty("text").GetString();
                return new TranslationResult { Success = true, TranslatedText = translated, EngineUsed = "DeepL" };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = "DeepL" };
            }
        }

        // 4. ── Baidu Fanyi (AppID + SecretKey) ──────────────────────────────
        private async Task<TranslationResult> ExecuteBaiduAsync(string text, string targetLang, string sourceLang, System.Threading.CancellationToken cancellationToken = default)
        {
            var config = SettingsManager.Current;
            if (string.IsNullOrWhiteSpace(config.BaiduAppId) || string.IsNullOrWhiteSpace(config.BaiduSecretKey))
                return new TranslationResult { Success = false, ErrorMessage = "Baidu AppID & SecretKey required.", EngineUsed = "Baidu" };

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                string salt = new Random().Next(100000, 999999).ToString();
                string rawSign = config.BaiduAppId.Trim() + text + salt + config.BaiduSecretKey.Trim();
                string sign = ComputeMD5(rawSign);

                string from = sourceLang == "auto" ? "auto" : NormalizeBaiduLang(sourceLang);
                string to = NormalizeBaiduLang(targetLang);

                var url = $"https://fanyi-api.baidu.com/api/trans/vip/translate?q={HttpUtility.UrlEncode(text)}&from={from}&to={to}&appid={config.BaiduAppId.Trim()}&salt={salt}&sign={sign}";
                var response = await client.GetAsync(url, cancellationToken);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

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
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = "Baidu" };
            }
        }

        // 5. ── Naver Papago (ClientID + ClientSecret) ────────────────────────
        private async Task<TranslationResult> ExecutePapagoAsync(string text, string targetLang, string sourceLang, System.Threading.CancellationToken cancellationToken = default)
        {
            var config = SettingsManager.Current;
            if (string.IsNullOrWhiteSpace(config.PapagoClientId) || string.IsNullOrWhiteSpace(config.PapagoClientSecret))
                return new TranslationResult { Success = false, ErrorMessage = "Naver Papago ClientID & Secret required.", EngineUsed = "Papago" };

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
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

                var response = await client.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return new TranslationResult { Success = false, ErrorMessage = $"Papago error ({(int)response.StatusCode}): {body}", EngineUsed = "Papago" };

                using var doc = JsonDocument.Parse(body);
                var resultText = doc.RootElement.GetProperty("message").GetProperty("result").GetProperty("translatedText").GetString();
                return new TranslationResult { Success = true, TranslatedText = resultText, EngineUsed = "Papago" };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = "Papago" };
            }
        }

        // 6. ── Yandex Translate (API Key) ───────────────────────────────────
        private async Task<TranslationResult> ExecuteYandexAsync(string text, string targetLang, string sourceLang, System.Threading.CancellationToken cancellationToken = default)
        {
            var config = SettingsManager.Current;
            if (string.IsNullOrWhiteSpace(config.YandexApiKey))
                return new TranslationResult { Success = false, ErrorMessage = "Yandex API Key required.", EngineUsed = "Yandex" };

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tl = targetLang.Split('-')[0];
                using var request = new HttpRequestMessage(HttpMethod.Post, "https://translate.api.cloud.yandex.net/translate/v2/translate");
                request.Headers.Add("Authorization", $"Api-Key {config.YandexApiKey.Trim()}");

                var reqBody = JsonSerializer.Serialize(new
                {
                    targetLanguageCode = tl,
                    texts = new[] { text }
                });
                request.Content = new StringContent(reqBody, Encoding.UTF8, "application/json");

                var response = await client.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return new TranslationResult { Success = false, ErrorMessage = $"Yandex error: {body}", EngineUsed = "Yandex" };

                using var doc = JsonDocument.Parse(body);
                var resultText = doc.RootElement.GetProperty("translations")[0].GetProperty("text").GetString();
                return new TranslationResult { Success = true, TranslatedText = resultText, EngineUsed = "Yandex" };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
                return new TranslationResult { Success = false, ErrorMessage = ex.Message, EngineUsed = "Yandex" };
            }
        }

        // 7. ── Youdao Translate (AppKey + AppSecret) ────────────────────────
        private async Task<TranslationResult> ExecuteYoudaoAsync(string text, string targetLang, string sourceLang, System.Threading.CancellationToken cancellationToken = default)
        {
            var config = SettingsManager.Current;
            if (string.IsNullOrWhiteSpace(config.YoudaoAppKey) || string.IsNullOrWhiteSpace(config.YoudaoAppSecret))
                return new TranslationResult { Success = false, ErrorMessage = "Youdao AppKey & Secret required.", EngineUsed = "Youdao" };

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
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

                var response = await client.SendAsync(request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                using var doc = JsonDocument.Parse(body);
                var errorCode = doc.RootElement.GetProperty("errorCode").GetString();
                if (errorCode != "0")
                {
                    return new TranslationResult { Success = false, ErrorMessage = $"Youdao Error Code: {errorCode}", EngineUsed = "Youdao" };
                }

                var trans = doc.RootElement.GetProperty("translation")[0].GetString();
                return new TranslationResult { Success = true, TranslatedText = trans, EngineUsed = "Youdao" };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested) throw new OperationCanceledException(cancellationToken);
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
