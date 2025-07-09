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
        //int idPbiDestino = int.Parse(Environment.GetEnvironmentVariable("ID_PBI_DESTINO"));
        string patOrigem = Environment.GetEnvironmentVariable("PAT_ORIGEM");
        string patDestino = Environment.GetEnvironmentVariable("PAT_DESTINO");
        string iterationPath = Environment.GetEnvironmentVariable("ITERATION_PATH");
        string atividade = Environment.GetEnvironmentVariable("ATIVIDADE_TASK");
        string responsavel = Environment.GetEnvironmentVariable("RESPONSAVEL_TASK");


        var workItem = await ObterWorkItem(origemUrl, patOrigem, idWorkItemOrigem);

        // Obtém o ID da US associada à task
        var relations = workItem["relations"];
        int idUS = 0;
        if (relations != null)
        {
            foreach (var relation in relations)
            {
                var rel = relation["rel"]?.ToString();
                var url = relation["url"]?.ToString();
                if (rel == "System.LinkTypes.Hierarchy-Reverse" && url.Contains("/workItems/"))
                {
                    var idStr = url.Split("/").Last();
                    idUS = int.Parse(idStr);
                    break;
                }
            }
        }

        if (idUS == 0)
        {
            Console.WriteLine("Não foi possível encontrar a US associada.");
            return;
        }

        var usWorkItem = await ObterWorkItem(origemUrl, patOrigem, idUS);
        string tituloUS = usWorkItem["fields"]["System.Title"]?.ToString();

        //string tituloPbiEsperado = $"{idUS}-{tituloUS}";
        string tituloPbiEsperado = idUS.ToString();

        int idPbiDestino = await BuscarPbiDestino(destinoUrl, patDestino, projetoDestino, tituloPbiEsperado);

        if (idPbiDestino == 0)
        {
            string responsavelUSOrigem = usWorkItem["fields"]["System.AssignedTo"]?.ToString();
            string tituloCompleto = $"{idUS} - {tituloUS}";
            string descricaoUS = usWorkItem["fields"]["System.Description"]?.ToString();
            string criteriosAceite = usWorkItem["fields"]["Microsoft.VSTS.Common.AcceptanceCriteria"]?.ToString();
            string horasEstimadasUS = usWorkItem["fields"]["Microsoft.VSTS.Scheduling.OriginalEstimate"]?.ToString();

            double.TryParse(horasEstimadasUS, out double horas);
            string complexidade;

            if (horas < 8)
                complexidade = "Muito Simples";
            else if (horas < 16)
                complexidade = "Simples";
            else
                complexidade = "Complexa";


            if (!string.IsNullOrEmpty(responsavelUSOrigem) &&
                responsavelUSOrigem.Contains(responsavel, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("PBI não encontrado. Criando novo PBI no destino...");

                idPbiDestino = await CriarPbiDestino(
                    destinoUrl,
                    patDestino,
                    projetoDestino,
                    tituloCompleto,
                    descricaoUS,
                    criteriosAceite,
                    horasEstimadasUS,
                    responsavel,
                    iterationPath,
                    complexidade
                );

                if (idPbiDestino == 0)
                {
                    Console.WriteLine("Erro ao criar o PBI.");
                    return;
                }

                Console.WriteLine($"Novo PBI criado com ID: {idPbiDestino}");
            }
            else
            {
                Console.WriteLine("PBI não encontrado e a US origem não está atribuída ao usuário atual. Nenhuma ação será tomada.");
                return;
            }
        }





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
        int idNovaTask = await CriarTaskDestino(destinoUrl, patDestino, projetoDestino,
            titulo, descricao, horasEstimadas, horasRestantes, horasCompletadas,
            prioridade, tipoTask, idPbiDestino,
            iterationPath, atividade, responsavel, idWorkItemOrigem);

        Console.WriteLine($"Task criada com sucesso! ID: {idNovaTask.ToString()}");
    }

    static async Task<int> CriarPbiDestino(
    string baseUrl,
    string pat,
    string projeto,
    string titulo,
    string descricao,
    string criteriosAceite,
    string horasEstimadas,
    string responsavel,
    string iterationPath,
    string complexidade)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

        var json = new JArray
            {
                new JObject { { "op", "add" }, { "path", "/fields/System.Title" }, { "value", titulo } },
                new JObject { { "op", "add" }, { "path", "/fields/System.Description" }, { "value", descricao } },
                new JObject { { "op", "add" }, { "path", "/fields/Microsoft.VSTS.Common.AcceptanceCriteria" }, { "value", criteriosAceite } },
                new JObject { { "op", "add" }, { "path", "/fields/System.AssignedTo" }, { "value", responsavel } },
                new JObject { { "op", "add" }, { "path", "/fields/System.IterationPath" }, { "value", iterationPath } },
                new JObject { { "op", "add" }, { "path", "/fields/Microsoft.VSTS.Scheduling.Estimate" }, { "value", horasEstimadas } },
                new JObject { { "op", "add" }, { "path", "/fields/Microsoft.VSTS.Scheduling.OriginalEstimate" }, { "value", horasEstimadas } },
                new JObject { { "op", "add" }, { "path", "/fields/Custom.Complexity" }, { "value", complexidade } },
            };

        var content = new StringContent(json.ToString(), Encoding.UTF8, "application/json-patch+json");

        var response = await client.PostAsync($"{projeto}/_apis/wit/workitems/$Product%20Backlog%20Item?api-version=6.0", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine("Erro ao criar PBI:");
            Console.WriteLine($"StatusCode: {response.StatusCode}");
            Console.WriteLine($"Resposta: {responseContent}");
            return 0;
        }

        var jsonResponse = JObject.Parse(responseContent);
        return (int)jsonResponse["id"];
    }



    static async Task<JObject> ObterWorkItem(string baseUrl, string pat, int id)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

        var response = await client.GetAsync($"_apis/wit/workitems/{id}?$expand=relations&api-version=6.0");
        response.EnsureSuccessStatusCode();
        string result = await response.Content.ReadAsStringAsync();
        return JObject.Parse(result);
    }

    static async Task<int> CriarTaskDestino(
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

        var jsonResponse = JObject.Parse(responseContent);
        return (int)jsonResponse["id"];

    }

    static async Task<int> BuscarPbiDestino(string baseUrl, string pat, string projeto, string tituloEsperado)
    {
        using var client = new HttpClient();
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($":{pat}")));

        var wiql = new
        {
            query = $"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = '{projeto}' AND [System.WorkItemType] = 'Product Backlog Item' AND [System.Title] CONTAINS '{tituloEsperado}'"
        };

        var content = new StringContent(JObject.FromObject(wiql).ToString(), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("_apis/wit/wiql?api-version=6.0", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Erro ao buscar PBI no destino. {response.StatusCode}: {responseContent}");
            return 0;
        }

        var json = JObject.Parse(responseContent);
        var workItems = json["workItems"] as JArray;
        if (workItems != null && workItems.Count > 0)
        {
            return (int)workItems[0]["id"];
        }

        return 0;
    }

}
