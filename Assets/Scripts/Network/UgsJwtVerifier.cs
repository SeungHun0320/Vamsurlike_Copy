using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace Vamsurlike.Network
{
    internal readonly struct JwtVerifyResult
    {
        public bool Success { get; }
        public string PlayerId { get; }
        public string FailureReason { get; }

        private JwtVerifyResult(bool success, string playerId, string failureReason)
        {
            Success = success;
            PlayerId = playerId;
            FailureReason = failureReason;
        }

        public static JwtVerifyResult Ok(string playerId) => new(true, playerId, null);
        public static JwtVerifyResult Fail(string reason) => new(false, null, reason);
    }

    // 서버 전용 — UGS Authentication의 AccessToken(JWT)을 JWKS 공개키로 서명 검증한다.
    // Matchmaker/Multiplay 없이 표준 JWKS(RFC 7517) + RS256(RFC 7515) 검증만으로 신뢰 가능한
    // PlayerId(sub 클레임)를 얻어, 클라이언트 자기신고 값을 대체한다.
    internal static class UgsJwtVerifier
    {
        private const string JwksUrl = "https://player-auth.services.api.unity.com/.well-known/jwks.json";
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(8);

        private static Dictionary<string, RSA> cachedKeys;
        private static DateTime cachedAt = DateTime.MinValue;
        private static Task refreshTask;

        public static async Task<JwtVerifyResult> VerifyAsync(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return JwtVerifyResult.Fail("접속 토큰이 없습니다.");

            string[] parts = accessToken.Split('.');
            if (parts.Length != 3)
                return JwtVerifyResult.Fail("토큰 형식이 올바르지 않습니다.");

            JObject header;
            JObject payload;
            byte[] signature;
            try
            {
                header = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
                payload = JObject.Parse(Encoding.UTF8.GetString(Base64UrlDecode(parts[1])));
                signature = Base64UrlDecode(parts[2]);
            }
            catch (Exception e)
            {
                return JwtVerifyResult.Fail($"토큰 디코딩 실패: {e.Message}");
            }

            string alg = header.Value<string>("alg");
            if (alg != "RS256")
                return JwtVerifyResult.Fail($"지원하지 않는 서명 알고리즘: {alg}");

            string kid = header.Value<string>("kid");
            if (string.IsNullOrEmpty(kid))
                return JwtVerifyResult.Fail("토큰에 kid가 없습니다.");

            RSA key = await GetKeyAsync(kid);
            if (key == null)
                return JwtVerifyResult.Fail("서명 검증용 공개키를 찾을 수 없습니다.");

            byte[] signedData = Encoding.UTF8.GetBytes($"{parts[0]}.{parts[1]}");
            bool signatureValid = key.VerifyData(signedData, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (!signatureValid)
                return JwtVerifyResult.Fail("토큰 서명이 유효하지 않습니다.");

            long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long? exp = payload.Value<long?>("exp");
            if (exp.HasValue && nowUnix >= exp.Value)
                return JwtVerifyResult.Fail("토큰이 만료되었습니다.");

            long? nbf = payload.Value<long?>("nbf");
            if (nbf.HasValue && nowUnix < nbf.Value)
                return JwtVerifyResult.Fail("토큰이 아직 유효하지 않습니다.");

            string sub = payload.Value<string>("sub");
            if (string.IsNullOrEmpty(sub))
                return JwtVerifyResult.Fail("토큰에 sub(PlayerId) 클레임이 없습니다.");

            return JwtVerifyResult.Ok(sub);
        }

        // 캐시에 없는 kid는 키 로테이션일 수 있으므로 1회 재조회 후에도 없으면 포기한다.
        private static async Task<RSA> GetKeyAsync(string kid)
        {
            if (TryGetCached(kid, out RSA key))
                return key;

            await EnsureRefreshedAsync();

            TryGetCached(kid, out key);
            return key;
        }

        private static bool TryGetCached(string kid, out RSA key)
        {
            key = null;
            return cachedKeys != null
                && DateTime.UtcNow - cachedAt < CacheLifetime
                && cachedKeys.TryGetValue(kid, out key);
        }

        // Unity 서버 프로세스는 단일 메인 스레드에서 이 async 흐름을 실행하므로, 동시 요청이 몰려도
        // 진행 중인 refreshTask를 공유해 JWKS를 중복 조회하지 않는다.
        private static Task EnsureRefreshedAsync()
        {
            if (refreshTask == null || refreshTask.IsCompleted)
                refreshTask = RefreshJwksAsync();
            return refreshTask;
        }

        private static async Task RefreshJwksAsync()
        {
            using UnityWebRequest request = UnityWebRequest.Get(JwksUrl);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                ServerConsoleLogger.Log($"[JWKS] 공개키 조회 실패: {request.error}");
                return;
            }

            try
            {
                JObject json = JObject.Parse(request.downloadHandler.text);
                var keys = new Dictionary<string, RSA>();
                foreach (JToken jwk in json["keys"])
                {
                    string keyId = jwk.Value<string>("kid");
                    string keyType = jwk.Value<string>("kty");
                    if (keyType != "RSA" || string.IsNullOrEmpty(keyId)) continue;

                    byte[] modulus = Base64UrlDecode(jwk.Value<string>("n"));
                    byte[] exponent = Base64UrlDecode(jwk.Value<string>("e"));

                    RSA rsa = RSA.Create();
                    rsa.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = exponent });
                    keys[keyId] = rsa;
                }

                cachedKeys = keys;
                cachedAt = DateTime.UtcNow;
                ServerConsoleLogger.Log($"[JWKS] 공개키 {keys.Count}개 갱신 완료.");
            }
            catch (Exception e)
            {
                ServerConsoleLogger.Log($"[JWKS] 파싱 실패: {e.Message}");
            }
        }

        private static byte[] Base64UrlDecode(string input)
        {
            string s = input.Replace('-', '+').Replace('_', '/');
            switch (s.Length % 4)
            {
                case 2: s += "=="; break;
                case 3: s += "="; break;
            }
            return Convert.FromBase64String(s);
        }
    }
}
