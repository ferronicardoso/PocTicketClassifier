using Laya;
using PocTicketClassifier;

Console.InputEncoding = System.Text.Encoding.UTF8;
Console.OutputEncoding = System.Text.Encoding.UTF8;

var diretorioModelo = Environment.GetEnvironmentVariable("LAYA_MODEL_DIR");
if (string.IsNullOrWhiteSpace(diretorioModelo) || !Directory.Exists(diretorioModelo))
{
    Console.Error.WriteLine("Defina a variável de ambiente LAYA_MODEL_DIR com o diretório do checkpoint do Laya.");
    return 1;
}

var token = Environment.GetEnvironmentVariable("MOVIDESK_API_TOKEN");
if (string.IsNullOrWhiteSpace(token))
{
    Console.Error.WriteLine("Defina a variável de ambiente MOVIDESK_API_TOKEN com a chave da API do Movidesk.");
    return 1;
}

using var http = new HttpClient { BaseAddress = MovideskClient.BaseAddress, Timeout = TimeSpan.FromSeconds(30) };
var movidesk = new MovideskClient(http, token);

using var agent = Agent.FromDirectory(diretorioModelo);
var classificador = new ClassificadorCriticidade(agent);
var detectorSuspeita = new ClassificadorSuspeita(agent);

Console.WriteLine("=== Classificação de tickets (Movidesk + Laya) ===");
Console.WriteLine("Informe o número do ticket. 'sair' encerra.");

while (true)
{
    Console.WriteLine();
    Console.Write("Ticket> ");
    var entrada = Console.ReadLine()?.Trim();

    if (entrada is null || entrada.Equals("sair", StringComparison.OrdinalIgnoreCase))
    {
        break;
    }

    // Aceita apenas numérico: evita injeção de parâmetros na query string.
    if (entrada.Length == 0 || !entrada.All(char.IsAsciiDigit))
    {
        Console.WriteLine("Informe um número de ticket válido.");
        continue;
    }

    try
    {
        var ticket = await movidesk.ObterTicketAsync(entrada);
        if (ticket is null)
        {
            Console.WriteLine($"Ticket {entrada} não encontrado.");
            continue;
        }

        var resultado = classificador.Classificar(ticket);
        var suspeita = detectorSuspeita.Classificar(ticket);

        Console.WriteLine();
        Console.WriteLine($"Ticket         : {ticket.Id}");
        Console.WriteLine($"Assunto        : {ticket.Assunto}");
        Console.WriteLine($"Status/Urgência: {ticket.Status ?? "-"} / {ticket.Urgencia ?? "-"} (Movidesk)");
        Console.WriteLine($"Conteúdo       : {ticket.FonteDescricao ?? "ausente"}");
        Console.WriteLine($"Criticidade    : {resultado.Nivel}{(resultado.Motivo is null ? "" : $" ({resultado.Motivo})")}");
        foreach (var (chave, probabilidade) in resultado.Probabilidades)
        {
            Console.WriteLine($"  {chave,-10} {probabilidade:P1}");
        }

        Console.WriteLine($"Suspeito       : {suspeita.Veredito}{(suspeita.Motivo is null ? "" : $" ({suspeita.Motivo})")}");
        foreach (var (chave, probabilidade) in suspeita.Probabilidades)
        {
            Console.WriteLine($"  {chave,-10} {probabilidade:P1}");
        }
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
    {
        Console.Error.WriteLine($"Falha ao consultar o ticket: {ex.Message}");
    }
}

return 0;
