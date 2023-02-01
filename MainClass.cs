public class AssinarPedido
{
    public string FicheiroOrigemB64 { get; set; } = "";
    public string CertificadoB64 { get; set; } = "";
    public string CertificadoAlias { get; set; } = "";
    public int SeloPagina { get; set; } = 1;
    public int[] SeloTamanho { get; set; } = { 240, 790, 100, 45 };
    public float SeloFonte { get; set; } = 5;
    public string Motivo { get; set; } = "";
    public string Localizacao { get; set; } = "";
    public string Token { get; set; } = "";
    public string EndPoint { get; set; } = "";
    public string AutorizadorID { get; set; } = "";
    public string AutorizadorSecret { get; set; } = "";
}
public class AssinarResposta
{
    public string FileB64 { get; set; } = "";
    public string Erro { get; set; } = "";
}
public class MainClass
{
    public static async Task Hello(HttpRequest aRequest)
    {
        string HTMLTxt = "";
        HTMLTxt += "<html>";
        HTMLTxt += "<head>";
        HTMLTxt += "<meta charset='UTF-8'>";
        HTMLTxt += "<title>OpenSign</title>";
        HTMLTxt += "<link rel='shortcut icon' href='/favicon.ico' type='image/x-icon'>";
        HTMLTxt += "<style>";
        
        HTMLTxt += "body {";
        HTMLTxt += "padding-top: 0px;";
        HTMLTxt += "text-align: center;";
        HTMLTxt += "font-family: \"Courier New\", Courier;";
        HTMLTxt += "background-color: #2A2826;";
        HTMLTxt += "color: #c4c4c4;";
        HTMLTxt += "}";

        HTMLTxt += "a, a:visited {";
        HTMLTxt += "color: steelblue;";
        HTMLTxt += "text-decoration: none;";
        HTMLTxt += "}";

        HTMLTxt += "a:hover {";
        HTMLTxt += "color: ghostwhite;";
        HTMLTxt += "text-decoration: none;";
        HTMLTxt += "}";

        HTMLTxt += "</style>";
        HTMLTxt += "</head>";
        HTMLTxt += "<body>";
        HTMLTxt += "<div align=\"center\">OpenSign " + GetVersao() + "</div>";
        HTMLTxt += "<a href=\"https://github.com/4DigitalCare/OpenSign\">GitHub</a>";
        HTMLTxt += "</body>";
        HTMLTxt += "</html>";
        aRequest.HttpContext.Response.StatusCode = 200;
        aRequest.HttpContext.Response.ContentType = "text/html; charset=utf-8";
        var bytes = System.Text.Encoding.UTF8.GetBytes(HTMLTxt);
        await aRequest.HttpContext.Response.Body.WriteAsync(bytes);
    }
    private static string GetVersao()
    {
        string Retorno = "";
        var Versao = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        if (Versao != null)
        {
            int iMajor = Versao.Major;
            int iMinor = Versao.Minor;
            int iBuild = Versao.Build;
            int iRevision = Versao.Revision;
            Retorno = $"{iMajor}.{iMinor}.{iBuild}";
            if ((iRevision > 0)) Retorno += " Rev. " + iRevision;
        }
        return Retorno;
    }
    public static async Task Assinar(HttpRequest aRequest, System.Text.Json.Nodes.JsonObject? aPedido)
    {
        int Estado = 400;
        string Resultado = "";
        if ((aRequest.IsHttps) && (aPedido != null))
        {
            try
            {
                aRequest.Headers.TryGetValue("Authorization", out Microsoft.Extensions.Primitives.StringValues AuthStr);
                if (
                        (AuthStr == "Bearer VGhlIEVhcnRoIGlzIGEgdmVyeSBzbWFsbCBzdGFnZSBpbiBhIHZhc3QgY29zbWljIGFyZW5hLg==") &&
                        (aPedido.ContainsKey("FicheiroOrigemB64")) &&
                        (aPedido.ContainsKey("CertificadoB64")) &&
                        (aPedido.ContainsKey("CertificadoAlias")) &&
                        (aPedido.ContainsKey("SeloPagina")) &&
                        (aPedido.ContainsKey("SeloTamanho")) &&
                        (aPedido.ContainsKey("SeloFonte")) &&
                        (aPedido.ContainsKey("Motivo")) &&
                        (aPedido.ContainsKey("Localizacao")) &&
                        (aPedido.ContainsKey("Token")) &&
                        (aPedido.ContainsKey("EndPoint")) &&
                        (aPedido.ContainsKey("AutorizadorID")) &&
                        (aPedido.ContainsKey("AutorizadorSecret"))
                    )
                {
                    var PedidoJSON = System.Text.Json.JsonSerializer.Deserialize<AssinarPedido>(aPedido);
                    if (PedidoJSON != null) {
                        Org.BouncyCastle.Cms.CmsSignedData certificateStore = new(new System.IO.MemoryStream(Convert.FromBase64String(PedidoJSON.CertificadoB64)));
                        Org.BouncyCastle.X509.Store.IX509Store x509Certs = certificateStore.GetCertificates("Collection");
                        System.Collections.ArrayList certificateArray = new(x509Certs.GetMatches(null));
                        List<Org.BouncyCastle.X509.X509Certificate> CertificadosLista = new();
                        foreach (var certificate in certificateArray)
                            CertificadosLista.Add((Org.BouncyCastle.X509.X509Certificate)certificate);
                        string FicheiroOrigem = Path.GetTempFileName();
                        string FicheiroDestino = Path.GetTempFileName();
                        File.WriteAllBytes(FicheiroOrigem, Convert.FromBase64String(PedidoJSON.FicheiroOrigemB64));
                        iText.Kernel.Pdf.PdfReader pdfReader = new(FicheiroOrigem);
                        iText.Kernel.Pdf.StampingProperties stampingProperties = new();
                        stampingProperties.UseAppendMode();
                        iText.Signatures.PdfSigner pdfSigner = new(pdfReader, new System.IO.FileStream(FicheiroDestino, System.IO.FileMode.OpenOrCreate), stampingProperties);
                        var Aparencia = pdfSigner.GetSignatureAppearance();
                        Aparencia.SetReason(PedidoJSON.Motivo);
                        Aparencia.SetLocation(PedidoJSON.Localizacao);
                        if (PedidoJSON.SeloPagina > 0)
                        {
                            Aparencia.SetPageNumber(PedidoJSON.SeloPagina).SetPageRect(new iText.Kernel.Geom.Rectangle(PedidoJSON.SeloTamanho[0], PedidoJSON.SeloTamanho[1], PedidoJSON.SeloTamanho[2], PedidoJSON.SeloTamanho[3]));
                            Aparencia.SetLayer2FontSize(PedidoJSON.SeloFonte);
                            iText.Signatures.CertificateInfo.X500Name subjectFields = iText.Signatures.CertificateInfo.GetSubjectFields(CertificadosLista[0]);
                            System.Text.StringBuilder stringBuilder = new();
                            stringBuilder.AppendLine($"Assinado por: {subjectFields?.GetField("CN") ?? subjectFields?.GetField("E") ?? ""}");
                            stringBuilder.AppendLine($"Data: {DateTime.Now}");
                            if (!string.IsNullOrEmpty(PedidoJSON.Motivo))
                                stringBuilder.AppendLine($"Motivo: {PedidoJSON.Motivo ?? ""}");
                            if (!string.IsNullOrEmpty(PedidoJSON.Localizacao))
                                stringBuilder.AppendLine($"Localização: {PedidoJSON.Localizacao ?? ""}");
                            Aparencia.SetLayer2Text(stringBuilder.ToString());
                        }
                        pdfSigner.SetFieldName("Assinatura1");
                        iText.Signatures.IExternalSignature pks = new ServerSignature(FicheiroOrigem, PedidoJSON.Token, PedidoJSON.EndPoint, PedidoJSON.CertificadoAlias, PedidoJSON.AutorizadorID, PedidoJSON.AutorizadorSecret, "2.16.840.1.101.3.4.2.1");
                        pdfSigner.SignDetached(pks, CertificadosLista.ToArray(), null, null, null, 0, iText.Signatures.PdfSigner.CryptoStandard.CMS);
                        string FicheiroDestinoB64 = Convert.ToBase64String(System.IO.File.ReadAllBytes(FicheiroDestino));
                        AssinarResposta fResposta = new()
                        {
                            FileB64 = FicheiroDestinoB64
                        };
                        Resultado = System.Text.Json.JsonSerializer.Serialize(fResposta);
                        Estado = 200;
                    }
                }
            }
            catch (Exception ex)
            {
                AssinarResposta fResposta = new();
                fResposta.Erro = ex.Message;
                Resultado = System.Text.Json.JsonSerializer.Serialize(fResposta);
                Estado = 200;
            }
        }
        aRequest.HttpContext.Response.StatusCode = Estado;
        aRequest.HttpContext.Response.ContentType = "application/json";
        var bytes = System.Text.Encoding.UTF8.GetBytes(Resultado);
        await aRequest.HttpContext.Response.Body.WriteAsync(bytes);
    }
}