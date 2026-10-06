using Ecommerce.Domain;
using Ecommerce.Application;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Infrastructure;
public class LedgerService(AppDb db) {
 public async Task PostAsync(string reference,string description,params (string Code,decimal Debit,decimal Credit,string Party)[] lines){
  if(await db.Set<AccountingPeriod>().AnyAsync(x=>x.Closed&&x.StartsAt<=DateTime.UtcNow&&x.EndsAt>=DateTime.UtcNow))throw new BusinessException("The current accounting period is closed.");
  CommerceRules.Balanced(lines.Select(x=>(x.Debit,x.Credit)));
  if(await db.Set<Journal>().AnyAsync(x=>x.Reference==reference))return;
  var accounts=await db.Set<AccountHead>().ToDictionaryAsync(x=>x.Code);
  var j=new Journal{Number="JV-"+Guid.NewGuid().ToString("N"),Reference=reference,Description=description};
  foreach(var l in lines){if(!accounts.TryGetValue(l.Code,out var a))throw new BusinessException("Account missing: "+l.Code);j.Lines.Add(new JournalLine{AccountHeadId=a.Id,Debit=l.Debit,Credit=l.Credit,Party=l.Party});}
  db.Add(j);
 }
 public void Audit(string actor,string action,Entity e,string before="",string after="")=>db.Add(new AuditEntry{ActorId=actor,Action=action,EntityType=e.GetType().Name,EntityId=e.Id.ToString(),Before=before,After=after});
 public void Notify(string uid,string message){db.Add(new Notification{UserId=uid,Message=message});db.Add(new OutboxMessage{UserId=uid,Message=message});}
}
