using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

const string replaceFrom = "Consumption";
const string replaceTo = "Production";

var token = File.ReadAllText("token.txt");

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

var url = "https://app.tabidoo.cloud/api/v2/apps/enerspot-c3nu/schemas/wascenarios/data";

var responseBody = await httpClient.GetStringAsync($"{url}?limit=50");

dynamic responseObject = JObject.Parse(responseBody);

var workflows = ((IEnumerable<dynamic>?) responseObject.data)?.Select(r => new Record
{
    Id = r.id.ToString(),
    Fields = new Fields
    {
        Name = r.fields.name.ToString(),
        Definition = r.fields.definition
    }
}).ToList() ?? new List<Record>();

var workflowToUpdate = new List<Record>();

foreach (var workflow in workflows)
{
    var updateWorkflow = false;
    
    var steps = (IEnumerable<dynamic>) workflow.Fields.Definition.steps;
    
    foreach (var step in steps)
    {
        var writtenTypeScript = (string?) step.data?.script?.writtenTypeScript?.ToString();
        var runableSript = (string?) step.data?.script?.runableSript?.ToString();
        
        if (writtenTypeScript == null || !writtenTypeScript.Contains(replaceFrom))
            continue;

        writtenTypeScript = writtenTypeScript.Replace(replaceFrom, replaceTo);
        runableSript = runableSript?.Replace(replaceFrom, replaceTo);
        step.data.script.writtenTypeScript = writtenTypeScript;
        step.data.script.runableSript = runableSript!;

        updateWorkflow = true;
    }
    
    if (updateWorkflow)
        workflowToUpdate.Add(workflow);
}

if (workflowToUpdate.Count == 0)
    return;

var updateBody = new BulkUpdate
{
    Bulk = workflowToUpdate
};

var jsonBody = JsonConvert.SerializeObject(updateBody);

var updateResponse = await httpClient.PatchAsync($"{url}/bulk", new StringContent(jsonBody, Encoding.UTF8, "application/json"));
var updateResponseBody = await updateResponse.Content.ReadAsStringAsync();

Console.WriteLine(updateResponseBody);



class BulkUpdate
{
    [JsonProperty("bulk")]
    public required IEnumerable<Record> Bulk { get; set; }
}

class Record
{
    [JsonProperty("id")]
    public required string Id { get; set; }
    
    [JsonProperty("fields")]
    public Fields Fields { get; set; }
}

struct Fields
{
    [JsonProperty("name")]
    public string? Name { get; set; }
    
    [JsonProperty("definition")]
    public dynamic Definition { get; set; }
}