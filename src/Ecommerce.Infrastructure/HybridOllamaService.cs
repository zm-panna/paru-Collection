using Ecommerce.Application;
namespace Ecommerce.Infrastructure;
public class HybridOllamaService(OllamaService local):IAIService
{
 public Task<string> AskAsync(string question,string userId,bool admin,CancellationToken ct)=>local.AskAsync(question,userId,admin,ct);
}
