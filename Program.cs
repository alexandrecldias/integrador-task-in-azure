using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json.Linq;

class Program
{
    //// CONFIGURAÇÕES
    //private static string origemUrl = Environment.GetEnvironmentVariable("ORIGEM_URL");
    //private static string destinoUrl = Environment.GetEnvironmentVariable("DESTINO_URL");
    //private static string projetoDestino = Environment.GetEnvironmentVariable("PROJETO_DESTINO");
    //private static int idWorkItemOrigem = int.Parse(Environment.GetEnvironmentVariable("ID_WORK_ITEM_ORIGEM"));
    //private static int idPbiDestino = int.Parse(Environment.GetEnvironmentVariable("ID_PBI_DESTINO"));

    //private static string patOrigem = Environment.GetEnvironmentVariable("PAT_ORIGEM");
    //private static string patDestino = Environment.GetEnvironmentVariable("PAT_DESTINO");
    //private static string iterationPath = Environment.GetEnvironmentVariable("ITERATION_PATH");
    //private static string atividade = Environment.GetEnvironmentVariable("ATIVIDADE_TASK");
    //private static string responsavel = Environment.GetEnvironmentVariable("RESPONSAVEL_TASK");


    static async Task Main()
    {
        DotNetEnv.Env.Load("C:\\GITHUB\\IntegradorTaskAzure\\.env"); // Carrega o .env automaticamente
                              // Agora você pode acessar as variáveis com segurança
        string origemUrl = Environment.GetEnvironmentVariable("ORIGEM_URL");
        string destinoUrl = Environment.GetEnvironmentVariable("DESTINO_URL");
        string projetoDestino = Environment.GetEnvironmentVariable("PROJETO_DESTINO");
        int idWorkItemOrigem = int.Parse(Environment.GetEnvironmentVariable("ID_WORK_ITEM_ORIGEM"));
        int idPbiDestino = int.Parse(Environment.GetEnvironmentVariable("ID_PBI_DESTINO"));
        string patOrigem = Environment.GetEnvironmentVariable("PAT_ORIGEM");
        string patDestino = Environment.GetEnvironmentVariable("PAT_DESTINO");
        string iterationPath = Environment.GetEnvironmentVariable("ITERATION_PATH");
        string atividade = Environment.GetEnvironmentVariable("ATIVIDADE_TASK");
        string responsavel = Environment.GetEnvironmentVariable("RESPONSAVEL_TASK");


        var workItem = await ObterWorkItem(origemUrl, patOrigem, idWorkItemOrigem);

        // LEITURA DOS DADOS
        string titulo = workItem["fields"]["System.Title"]?.ToString();
        string descricao = workItem["fields"]["System.Description"]?.ToString();
        string horasEstimadas = workItem["fields"]["Microsoft.VSTS.Scheduling.OriginalEstimate"]?.ToString();
        string horasRestantes = workItem["fields"]["Microsoft.VSTS.Scheduling.RemainingWork"]?.ToString();
        string horasCompletadas = workItem["fields"]["Microsoft.VSTS.Scheduling.CompletedWork"]?.ToString();
        string prioridade = workItem["fields"]["Microsoft.VSTS.Common.Priority"]?.ToString();



        // Aqui você escolhe um dos valores válidos
        string tipoTask = "Atendimento ao cliente"; // <- Altere conforme desejado

        // Criação da nova task
        await CriarTaskDestino(destinoUrl, patDestino, projetoDestino,
            titulo, descricao, horasEstimadas, horasRestantes, horasCompletadas,
            prioridade, tipoTask, idPbiDestino,
            iterationPath, atividade, responsavel, idWorkItemOrigem);

        Console.WriteLine("Task criada com sucesso!");
    }

    static async Task<JObject> ObterWorkItem(string baseUrl, string pat, int id)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

        var response = await client.GetAsync($"_apis/wit/workitems/{id}?api-version=6.0");
        response.EnsureSuccessStatusCode();
        string result = await response.Content.ReadAsStringAsync();
        return JObject.Parse(result);
    }

    static async Task CriarTaskDestino(
        string baseUrl,
        string pat,
        string projeto,
        string titulo,
        string descricao,
        string horasEstimadas,
        string horasRestantes,
        string horasCompletadas,
        string prioridade,
        string tipoTask,
        int idPbi,
        string iterationPath,
        string atividade,
        string responsavel,
        int idWorkItemOrigem)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

        var json = new JArray
        {
            new JObject { { "op", "add" }, { "path", "/fields/System.Title" }, { "value",$"{idWorkItemOrigem}-{titulo}"  } },
            new JObject { { "op", "add" }, { "path", "/fields/System.Description" }, { "value", descricao } },
         // ADIÇÃO NO PATCH JSON
            new JObject { { "op", "add" }, { "path", "/fields/Microsoft.VSTS.Scheduling.OriginalEstimate" }, { "value", horasEstimadas } },
            new JObject { { "op", "add" }, { "path", "/fields/Microsoft.VSTS.Scheduling.Effort" }, { "value", horasCompletadas } },
            new JObject { { "op", "add" }, { "path", "/fields/Custom.DateWork" }, { "value", DateTime.UtcNow.ToString("o") } },
            new JObject { { "op", "add" }, { "path", "/fields/System.IterationPath" }, { "value", iterationPath } },
            new JObject { { "op", "add" }, { "path", "/fields/Microsoft.VSTS.Common.Activity" }, { "value", atividade } },
            new JObject { { "op", "add" }, { "path", "/fields/System.AssignedTo" }, { "value", responsavel } },


            new JObject { { "op", "add" }, { "path", "/fields/Microsoft.VSTS.Scheduling.CompletedWork" }, { "value", horasCompletadas } },
            new JObject { { "op", "add" }, { "path", "/fields/Microsoft.VSTS.Common.Priority" }, { "value", prioridade } },
            new JObject { { "op", "add" }, { "path", "/fields/Custom.TypeTask" }, { "value", tipoTask } }, // Ajuste o campo se necessário
            new JObject { { "op", "add" }, { "path", "/relations/-" }, {
                "value", new JObject {
                    { "rel", "System.LinkTypes.Hierarchy-Reverse" },
                    { "url", $"{baseUrl}_apis/wit/workItems/{idPbi}" },
                    { "attributes", new JObject { { "comment", "Vinculado ao PBI" } } }
                }
            }}
        };

        var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json-patch+json");

        var response = await client.PostAsync($"{projeto}/_apis/wit/workitems/$Task?api-version=6.0", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Erro na requisição:");
            Console.WriteLine($"StatusCode: {response.StatusCode}");
            Console.WriteLine($"Resposta: {responseContent}");
        }
        response.EnsureSuccessStatusCode();

    }
}
