using Laya;
using Laya.Runtime;

namespace PocTicketClassifier;

public sealed record ResultadoSuspeita(
    string Veredito,
    IReadOnlyDictionary<string, double> Probabilidades,
    string? Motivo = null)
{
    public const string Suspeito = "suspeito";
    public const string Legitimo = "legitimo";
    public const string Indeterminada = "indeterminada";
}

/// <summary>Indica se o conteúdo de um ticket tem características de golpe ou phishing (uma passagem do Laya).</summary>
public sealed class ClassificadorSuspeita(Agent agent)
{
    private const int LimiteDescricao = 2000;

    public ResultadoSuspeita Classificar(Ticket ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket.Descricao))
        {
            return new ResultadoSuspeita(
                ResultadoSuspeita.Indeterminada,
                new Dictionary<string, double>(),
                "sem conteúdo textual na descrição do ticket");
        }

        var descricao = ticket.Descricao is { Length: > LimiteDescricao } d ? d[..LimiteDescricao] : ticket.Descricao;

        var estado = new Dictionary<string, object?>
        {
            ["assunto"] = ticket.Assunto,
            ["descricao"] = descricao,
        };

        var pergunta = Question.Choice(
            "Este ticket apresenta características de golpe, phishing ou tentativa de fraude?",
            (ResultadoSuspeita.Legitimo,
                "solicitação de suporte coerente, sem pedido de credenciais, pagamento ou ação urgente suspeita"),
            (ResultadoSuspeita.Suspeito,
                "indícios de golpe ou phishing: pedido de senha, código ou dados financeiros, links suspeitos, " +
                "falsa urgência, ameaça, promessa de prêmio, cobrança inesperada ou personificação de terceiros"));

        var resposta = agent.SystemOne(estado, new QuestionSet().Add("suspeita", pergunta))["suspeita"];

        var probabilidades = resposta.Probabilities?
            .OrderByDescending(p => p.Value)
            .ToDictionary(p => p.Key, p => (double)p.Value)
            ?? new Dictionary<string, double>();

        return new ResultadoSuspeita(resposta.Choice ?? "indefinida", probabilidades);
    }
}
