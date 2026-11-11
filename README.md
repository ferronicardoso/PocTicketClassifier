# PocTicketClassifier

Prova de conceito em .NET que classifica tickets do Movidesk. O usuário informa o número do ticket no console; o programa consulta a API do Movidesk e classifica o conteúdo com o modelo [Laya](https://github.com/NandhaKishorM/laya), executado localmente.

## Classificações

Cada classificação retorna o resultado e a distribuição de probabilidades:

| Classificação | Valores possíveis |
|---|---|
| Nível de criticidade | `baixa`, `media`, `alta`, `critica` |
| Suspeita de golpe ou phishing | `legitimo`, `suspeito` |

Quando o ticket não possui conteúdo textual utilizável, o resultado é `indeterminada`, acompanhado do motivo.

## Requisitos

- .NET SDK 10.0
- Pacote NuGet [`Laya`](https://www.nuget.org/packages/Laya), restaurado automaticamente no build
- Checkpoint multilíngue do Laya, em um diretório local (ver abaixo)
- Token da API do Movidesk

## Obtendo o modelo

Os pesos não fazem parte deste repositório. Baixe o checkpoint multilíngue de [`convaiinnovations/laya`](https://huggingface.co/convaiinnovations/laya) (Hugging Face) para um diretório local e aponte `LAYA_MODEL_DIR` para ele.

## Configuração

O programa lê a configuração de variáveis de ambiente:

| Variável | Descrição |
|---|---|
| `LAYA_MODEL_DIR` | Diretório do checkpoint do Laya |
| `MOVIDESK_API_TOKEN` | Token da API do Movidesk |

```powershell
$env:LAYA_MODEL_DIR = "C:\models\laya-multilingual"
$env:MOVIDESK_API_TOKEN = "<token>"
```

O token não é gravado em arquivo nem exibido em saídas ou mensagens de erro.

## Uso

```powershell
dotnet run --project PocTicketClassifier
```

Informe o número do ticket no prompt. O comando `sair` encerra o programa.

```text
Ticket> 12345

Ticket         : 12345
Assunto        : Sistema fora do ar
Status/Urgência: Novo / Urgente (Movidesk)
Conteúdo       : description
Criticidade    : critica
  critica    91,3%
  alta       7,9%
  ...
Suspeito       : legitimo
  legitimo   98,2%
  suspeito   1,8%
```

O campo `Conteúdo` indica a origem do texto analisado (`description`, `htmlDescription` ou `ausente`).

## Como testar

O projeto não possui testes automatizados. A validação é manual, executando o programa contra a API do Movidesk.

1. Verifique se o projeto compila:

   ```powershell
   dotnet build
   ```

2. Execute o programa e confirme os cenários abaixo:

   | Cenário | Entrada | Resultado esperado |
   |---|---|---|
   | Ticket com texto | Número de um ticket existente | Assunto, criticidade e suspeita com as probabilidades |
   | Ticket inexistente | Número que não existe | `Ticket <n> não encontrado.` |
   | Entrada inválida | `abc`, `12a` ou texto vazio | `Informe um número de ticket válido.` |
   | Ticket sem texto utilizável | Ticket sem descrição ou apenas com resposta automática | Resultado `indeterminada`, com o motivo, e `Conteúdo: ausente` |
   | Variável ausente | Executar sem `LAYA_MODEL_DIR` ou sem `MOVIDESK_API_TOKEN` | Mensagem de erro indicando a variável e encerramento do programa |
   | Token inválido | Token incorreto | `Falha ao consultar o ticket: Movidesk respondeu 401 ...`, sem exibir o token |
   | Encerramento | `sair` | Programa encerrado |

3. Nos cenários com ticket real, avalie se a criticidade e a suspeita são coerentes com o conteúdo. O resultado é indicativo e deve ser usado como apoio à triagem, não como decisão.

## Como funciona

1. `MovideskClient` consulta `GET /public/v1/tickets?id=` com `$expand=actions` e extrai assunto, status, urgência, categoria e a descrição original do ticket.
2. A descrição é a primeira ação que não seja resposta automática (origem 4, e-mail enviado). Usa-se `description` ou, se vazio, `htmlDescription` convertido em texto. A origem 4 como resposta automática foi observada em um ticket e deve ser confirmada em outros.
3. `ClassificadorCriticidade` envia o assunto e a descrição (limitada a 2000 caracteres) ao modelo como uma pergunta de múltipla escolha com quatro níveis descritos.
4. `ClassificadorSuspeita` faz uma segunda passagem com o mesmo texto, avaliando indícios de golpe, phishing ou fraude: pedido de credenciais ou dados financeiros, falsa urgência, links suspeitos, cobrança inesperada e personificação.

Novas classificações (categoria, setor, sentimento) podem ser adicionadas como perguntas no mesmo `QuestionSet`.

## Estrutura

```text
PocTicketClassifier/
├── Program.cs                    # Loop de console
├── MovideskClient.cs             # Consulta de ticket por id
├── Ticket.cs                     # Modelo mínimo do ticket
├── ClassificadorCriticidade.cs   # Classificação de criticidade
└── ClassificadorSuspeita.cs      # Classificação de suspeita de golpe/phishing
```

## Segurança e privacidade

- O Movidesk exige o token na query string; por isso a URL nunca é registrada em log nem exibida em erros.
- O número do ticket aceita apenas dígitos, o que evita injeção de parâmetros na query string.
- O conteúdo dos tickets pode conter dados pessoais ou sensíveis. O assunto é exibido no console; evite copiar essa saída para repositórios, issues ou documentação.
- A classificação é executada localmente, em CPU, pelo pacote `Laya`, um port em C# puro, sem Python, PyTorch ou dependências nativas. No código deste projeto, a única comunicação externa é a consulta à API do Movidesk.

## Limitações

- Prova de conceito: sem testes automatizados e sem retry ou controle de taxa de requisições (apenas exibe `retry-after` quando informado).
- Processa um ticket por vez, de forma interativa.
- Os rótulos de criticidade são definidos no prompt e não foram calibrados com tickets rotulados.

## Licença

O código deste repositório é distribuído sob a licença [MIT](LICENSE). Ela não se aplica a componentes de terceiros: o pacote `Laya` é distribuído sob Apache-2.0, e os checkpoints possuem termos próprios, indicados na página do modelo no Hugging Face. Verifique esses termos antes de usar o modelo em contexto corporativo.
