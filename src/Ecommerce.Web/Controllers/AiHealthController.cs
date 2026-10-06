using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Ecommerce.Web.Controllers;
[Authorize(Roles="ADMIN,Admin,SuperAdmin"),Route("admin/ai-health")]
public class AiHealthController(IHttpClientFactory clients,IConfiguration config):Controller
{
 [HttpGet("")] public async Task<IActionResult> Index(CancellationToken ct) {
 try {
 using var client=clients.CreateClient();client.BaseAddress=new Uri(config["Ollama:Local:BaseUrl"]??"http://localhost:11434/");client.Timeout=TimeSpan.FromSeconds(15);
 using var response=await client.GetAsync("api/tags",ct);response.EnsureSuccessStatusCode();using var doc=JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
 var model=config["Ollama:Local:Model"]??"qwen3.5:0.8b";var installed=doc.RootElement.GetProperty("models").EnumerateArray().Select(x=>x.GetProperty("name").GetString()).ToArray();
 return Json(new {reachable=true,model,installed,modelInstalled=installed.Contains(model),note="Reachability and installed models only. Run an Assistant question to verify inference."});
 } catch(Exception ex) when(!ct.IsCancellationRequested&&(ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)) {return StatusCode(503,new{reachable=false,message="Check Ollama service, server URL and model installation."});}
 }
}
