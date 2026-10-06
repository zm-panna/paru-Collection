using System.Net;
using System.Net.Mail;
using Ecommerce.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
namespace Ecommerce.Infrastructure;
public class EmailOutboxWorker(IServiceScopeFactory scopes,IConfiguration config):BackgroundService{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken){while(!stoppingToken.IsCancellationRequested){try{if(config.GetValue<bool>("Notifications:EmailEnabled"))await Dispatch(stoppingToken);}catch(Exception) when(!stoppingToken.IsCancellationRequested){/* A failed dispatch stays in the durable outbox for retry. */}try{await Task.Delay(TimeSpan.FromSeconds(30),stoppingToken);}catch(OperationCanceledException){break;}}}
 async Task Dispatch(CancellationToken ct){if(string.IsNullOrWhiteSpace(config["Smtp:Host"])||string.IsNullOrWhiteSpace(config["Smtp:From"]))return;using var scope=scopes.CreateScope();var db=scope.ServiceProvider.GetRequiredService<AppDb>();var now=DateTime.UtcNow;var ids=await db.Set<OutboxMessage>().Where(x=>(x.Status=="Pending"||x.Status=="Processing")&&x.NextAttemptAt<=now&&x.Attempts<5).OrderBy(x=>x.CreatedAt).Select(x=>x.Id).Take(20).ToListAsync(ct);
  foreach(var id in ids){var claimed=await db.Set<OutboxMessage>().Where(x=>x.Id==id&&(x.Status=="Pending"||x.Status=="Processing")&&x.NextAttemptAt<=now).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,"Processing").SetProperty(x=>x.NextAttemptAt,now.AddMinutes(5)).SetProperty(x=>x.Attempts,x=>x.Attempts+1),ct);if(claimed==0)continue;var m=await db.Set<OutboxMessage>().SingleAsync(x=>x.Id==id,ct);var email=await db.Users.Where(x=>x.Id==m.UserId).Select(x=>x.Email).FirstOrDefaultAsync(ct);if(email?.EndsWith("@guest.invalid",StringComparison.OrdinalIgnoreCase)==true)email=await db.Set<Order>().Where(x=>x.UserId==m.UserId).OrderByDescending(x=>x.CreatedAt).Select(x=>x.Email).FirstOrDefaultAsync(ct);try{if(string.IsNullOrWhiteSpace(email)){m.Status="Skipped";}else{using var smtp=new SmtpClient(config["Smtp:Host"],config.GetValue<int>("Smtp:Port",587)){EnableSsl=true,Credentials=new NetworkCredential(config["Smtp:Username"],config["Smtp:Password"])};using var message=new MailMessage(config["Smtp:From"]!,email,"Paru Collection update",m.Message);await smtp.SendMailAsync(message,ct);m.Status="Sent";}}catch(Exception) when(!ct.IsCancellationRequested){m.Status=m.Attempts>=5?"Failed":"Pending";m.NextAttemptAt=DateTime.UtcNow.AddMinutes(Math.Pow(2,m.Attempts));}await db.SaveChangesAsync(ct);}
 }
}
