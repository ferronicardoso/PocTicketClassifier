using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PocTicketClassifier;

/// <summary>Cliente mínimo da API pública do Movidesk (v1) para consulta de um ticket por id.</summary>
public sealed class MovideskClient(HttpClient http, string token)
{
    public static readonly Uri BaseAddress = new("https://api.movidesk.com/public/v1/");

    public async Task<Ticket?> ObterTicketAsync(string id, CancellationToken ct = default)
    {
        // O Movidesk exige o token na query string. A URL não deve ser logada nem exibida em erros.
        var url = $"tickets?token={Uri.EscapeDataString(token)}&id={Uri.EscapeDataString(id)}"
                  + "&$select=id,subject,status,urgency,category"
                  + "&$expand=actions($select=id,type,origin,description,htmlDescription,createdDate)";

        using var resposta = await http.GetAsync(url, ct);

        if (resposta.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        if (!resposta.IsSuccessStatusCode)
        {
            var retry = resposta.Headers.RetryAfter?.Delta is { } d ? $" (retry-after: {d.TotalSeconds:0}s)" : "";
            throw new InvalidOperationException(
                $"Movidesk respondeu {(int)resposta.StatusCode} {resposta.ReasonPhrase}{retry}.");
        }

        using var doc = await JsonDocument.ParseAsync(await resposta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var raiz = doc.RootElement;

        // Por id, a API retorna um objeto; tolera array por segurança.
        if (raiz.ValueKind == JsonValueKind.Array)
        {
            if (raiz.GetArrayLength() == 0) return null;
            raiz = raiz[0];
        }

        var (descricao, fonte) = ExtrairDescricao(raiz);

        return new Ticket(
            Id: Texto(raiz, "id") ?? id,
            Assunto: Texto(raiz, "subject") ?? string.Empty,
            Status: Texto(raiz, "status"),
            Urgencia: Texto(raiz, "urgency"),
            Categoria: Texto(raiz, "category"),
            Descricao: descricao,
            FonteDescricao: fonte);
    }

    // Origem de ação para e-mail enviado pelo sistema (ex.: resposta automática de registro do ticket).
    // Valor observado em um ticket; deve ser confirmado em outros tickets.
    private const string OrigemEmailEnviado = "4";

    // Usa a primeira ação (menor createdDate) que não seja resposta automática e tenha texto utilizável.
    // Prefere "description"; se vazio, usa "htmlDescription" convertido em texto. Sem texto: (null, null).
    private static (string? Descricao, string? Fonte) ExtrairDescricao(JsonElement ticket)
    {
        if (!ticket.TryGetProperty("actions", out var acoes) || acoes.ValueKind != JsonValueKind.Array)
        {
            return (null, null);
        }

        foreach (var acao in acoes.EnumerateArray().OrderBy(a => Texto(a, "createdDate"), StringComparer.Ordinal))
        {
            if (Texto(acao, "origin") == OrigemEmailEnviado)
            {
                continue;
            }

            var texto = Texto(acao, "description");
            if (TemTexto(texto))
            {
                return (texto!.Trim(), "description");
            }

            texto = HtmlParaTexto(Texto(acao, "htmlDescription"));
            if (TemTexto(texto))
            {
                return (texto, "htmlDescription");
            }
        }

        return (null, null);
    }

    private static bool TemTexto(string? s) => s is not null && s.Any(char.IsLetterOrDigit);

    private static string? HtmlParaTexto(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var semBlocos = Regex.Replace(html, @"<(script|style)\b.*?</\1\s*>|<!--.*?-->", " ",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var semTags = Regex.Replace(semBlocos, @"<[^>]+>", " ");
        var texto = Regex.Replace(WebUtility.HtmlDecode(semTags), @"\s+", " ").Trim();
        return texto.Length == 0 ? null : texto;
    }

    private static string? Texto(JsonElement e, string nome) =>
        e.TryGetProperty(nome, out var v) && v.ValueKind != JsonValueKind.Null
            ? v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString()
            : null;
}
