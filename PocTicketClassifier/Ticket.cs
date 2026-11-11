namespace PocTicketClassifier;

/// <summary>Visão mínima do ticket do Movidesk usada na classificação.</summary>
/// <param name="Descricao">Texto do corpo do ticket; nulo quando não há conteúdo textual utilizável.</param>
/// <param name="FonteDescricao">Origem do texto (ex.: "description", "htmlDescription"); nulo quando não há texto.</param>
public sealed record Ticket(
    string Id,
    string Assunto,
    string? Status,
    string? Urgencia,
    string? Categoria,
    string? Descricao,
    string? FonteDescricao);
