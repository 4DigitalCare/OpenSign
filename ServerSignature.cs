using iText.Signatures;
using Newtonsoft.Json.Linq;
using OtpNet;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
public class ServerSignature : IExternalSignature
{
    private string _sourcePlainPdf;
    private string _authenticationToken;
    private string _qscdServerBaseAddress;
    private string _certificateAlias;
    private string _seed;
    private string _secret;
    private string _hashAlgorithmOID;
    public ServerSignature(string sourcePlainPdf, string authenticationToken, string qscdServerBaseAddress,
        string certificateAlias, string seed, string secret, string hashAlgorithmOID)
    {
        _sourcePlainPdf = sourcePlainPdf;
        _authenticationToken = authenticationToken;
        _qscdServerBaseAddress = qscdServerBaseAddress;
        _certificateAlias = certificateAlias;
        _seed = seed;
        _secret = secret;
        _hashAlgorithmOID = hashAlgorithmOID;
    }
    public String GetHashAlgorithm()
    {
        return DigestAlgorithms.SHA256;
    }
    public String GetEncryptionAlgorithm()
    {
        return "RSA";
    }
    public byte[]? Sign(byte[] message)
    {
        try
        {
            string digest;
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(message);
                digest = Convert.ToBase64String(hash);
            }
            List<JObject> docsToSign = new List<JObject>();
            dynamic docToSignInfo = new JObject();
            docToSignInfo.docAlias = System.IO.Path.GetFileName(_sourcePlainPdf);
            docToSignInfo.hashAlg = _hashAlgorithmOID;
            docToSignInfo.sigType = 1;
            docToSignInfo.hashToSign_64 = digest;
            docsToSign.Add(docToSignInfo);
            dynamic jsonSigCompleteTOTPRequest = new JObject();
            jsonSigCompleteTOTPRequest.certAlias = _certificateAlias;
            jsonSigCompleteTOTPRequest["docsToSign"] = JToken.FromObject(docsToSign);
            jsonSigCompleteTOTPRequest.sigReqDescr = Guid.NewGuid().ToString();
            jsonSigCompleteTOTPRequest.totpID = _seed;
            var totp = new Totp(Base32Encoding.ToBytes(_secret));
            jsonSigCompleteTOTPRequest.totpValue = totp.ComputeTotp(DateTime.UtcNow);
            byte[]? signedHash = null;
            using (var handler = new HttpClientHandler())
            {
                //create an HttpClient and pass a handler to it
                using var httpClient = new HttpClient(handler);
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpClient.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authenticationToken);
                // ******WS - Polling 
                HttpResponseMessage responseMessage = httpClient.PostAsync(new Uri(_qscdServerBaseAddress + "/totp/sigCompleteTOTPPolling"), new StringContent(jsonSigCompleteTOTPRequest.ToString(), UnicodeEncoding.UTF8, "application/json")).Result;
                if (responseMessage.IsSuccessStatusCode)
                {
                    HttpContent responseContent = responseMessage.Content;
                    JObject responsePollingQSCDService = JObject.Parse(responseContent.ReadAsStringAsync().Result);
                    int i = 0;
                    do
                    {
                        // ******WS - Finalize 
                        HttpResponseMessage responseFinalize = httpClient.PostAsync(new Uri(_qscdServerBaseAddress + "/totp/sigFinalize"),
                        new StringContent(responsePollingQSCDService.ToString(), UnicodeEncoding.UTF8, "application/json")).Result;
                        if (responseFinalize.IsSuccessStatusCode)
                        {
                            HttpContent responseContentFinalize = responseFinalize.Content;
                            JObject responseFinalizeQSCDService = JObject.Parse(responseContentFinalize.ReadAsStringAsync().Result);
                            JToken? DocInfo = responseFinalizeQSCDService.GetValue("signedDocsInfo");
                            if (DocInfo != null)
                            {
                                dynamic responseFinalizeHashSign = JArray.Parse(DocInfo.ToString());
                                foreach (JObject item in responseFinalizeHashSign)
                                {
                                    JToken? HashSigB64 = item.GetValue("hashSig");
                                    if (HashSigB64 != null) { signedHash = Convert.FromBase64String(HashSigB64.ToString()); }
                                    break;
                                }
                            }
                            i = 5;
                        }
                        else
                        {
                            HttpContent responseContentFinalize = responseFinalize.Content;
                            JObject responseFinalizeQSCDService = JObject.Parse(responseContentFinalize.ReadAsStringAsync().Result);
                            bool DarErro = true;
                            if (responseFinalizeQSCDService.ContainsKey("errorCode"))
                            {
                                JToken? fErro = responseFinalizeQSCDService.GetValue("errorCode");
                                if (fErro != null)
                                {
                                    if (fErro.ToString() == "028001") // Not Ready
                                    {
                                        int secPause = 0;
                                        if (responseFinalizeQSCDService.ContainsKey("retryAfter"))
                                        {
                                            JToken? fApos = responseFinalizeQSCDService.GetValue("retryAfter");
                                            if (fApos != null) { int.TryParse(fApos.ToString(), out secPause); }
                                        }
                                        if (secPause > 0)
                                        {
                                            DarErro = false;
                                            System.Threading.Thread.Sleep(secPause * 1000);
                                        }
                                    }
                                }
                            }
                            if (DarErro) { throw new iText.Kernel.Exceptions.PdfException(responseFinalize.Content.ReadAsStringAsync().Result); }
                        }
                        i++;
                    } while (i < 5);
                }
                else
                {
                    throw new iText.Kernel.Exceptions.PdfException(responseMessage.Content.ReadAsStringAsync().Result);
                }
            }
            return signedHash;
        }
        catch (IOException e)
        {
            throw new iText.Kernel.Exceptions.PdfException(e);
        }
    }
}
