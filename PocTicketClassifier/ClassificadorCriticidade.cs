using Laya;
using Laya.Runtime;

namespace PocTicketClassifier;

public sealed record ResultadoCriticidade(
    string Nivel,
    IReadOnlyDictionary<string, double> Probabilidades,
    string? Motivo = null)
{
    public const string Indeterminada = "indeterminada";
}

/// <summary>Classifica o nível de criticidade de um ticket com o Laya (uma passagem, sem geração de texto).</summary>
public sealed class ClassificadorCriticidade(Agent agent)
{
    private const int LimiteDescricao = 2000;

    public ResultadoCriticidade Classificar(Ticket ticket)
    {
        // Sem corpo textual o modelo só teria o assunto e escolheria um nível por viés lexical: não classifica.
        if (string.IsNullOrWhiteSpace(ticket.Descricao))
        {
            return new ResultadoCriticidade(
                ResultadoCriticidade.Indeterminada,
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
            "Qual é o nível de criticidade deste ticket de suporte?",
            ("baixa", "dúvida, solicitação simples ou melhoria, sem impacto operacional"),
            ("media", "problema com contorno disponível ou impacto limitado a poucos usuários"),
            ("alta", "funcionalidade importante indisponível ou impacto relevante no negócio"),
            ("critica", "sistema parado, perda de dados, falha de segurança ou prejuízo financeiro imediato"));

        var resposta = agent.SystemOne(estado, new QuestionSet().Add("criticidade", pergunta))["criticidade"];

        var probabilidades = resposta.Probabilities?
            .OrderByDescending(p => p.Value)
            .ToDictionary(p => p.Key, p => (double)p.Value)
            ?? new Dictionary<string, double>();

        return new ResultadoCriticidade(resposta.Choice ?? "indefinida", probabilidades);
    }
}
