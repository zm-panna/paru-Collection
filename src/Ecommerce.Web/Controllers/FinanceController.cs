using System.Security.Claims;
using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;
[Route("admin/finance")]
public class FinanceController(AppDb db,ReconciliationService service,RefundGatewayService refunds):Controller{
 string Actor=>User.FindFirstValue(ClaimTypes.NameIdentifier)!;
 [HttpGet(""),Permit("accounts.view")]public async Task<IActionResult> Index(){ViewBag.Heads=await db.Set<AccountHead>().Where(x=>x.Active).ToListAsync();ViewBag.Payments=await db.Set<PaymentTransaction>().OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync();ViewBag.Bank=await db.Set<BankStatementLine>().OrderByDescending(x=>x.Date).Take(100).ToListAsync();return View(await db.Set<PaymentReconciliation>().Include(x=>x.PaymentTransaction).OrderByDescending(x=>x.CreatedAt).Take(100).ToListAsync());}
 [HttpPost("payment"),Permit("payments.edit")]public async Task<IActionResult> Payment(Guid id,decimal amount,string reference){await service.PaymentAsync(id,amount,reference,Actor);return RedirectToAction(nameof(Index));}
 [HttpPost("bank"),Permit("accounts.edit")]public async Task<IActionResult> Bank(string reference,DateTime date,decimal amount,Guid? lineId){await service.BankAsync(reference,date,amount,lineId,Actor);return RedirectToAction(nameof(Index));}
 [HttpPost("voucher"),Permit("accounts.edit")]public async Task<IActionResult> Voucher(string type,string party,decimal amount,string account,string description){await service.VoucherAsync(type,party??"",amount,account,description,Actor);return RedirectToAction(nameof(Index));}
 [HttpPost("refund-start"),Permit("payments.refund")]public async Task<IActionResult> RefundStart(Guid id,CancellationToken ct){await refunds.StartAsync(id,Actor,ct);return Redirect("/admin/operations/returns");}
 [HttpPost("refund-query"),Permit("payments.refund")]public async Task<IActionResult> RefundQuery(Guid id,CancellationToken ct){await refunds.QueryAsync(id,Actor,ct);return Redirect("/admin/operations/returns");}
 [HttpPost("store-credit"),Permit("payments.refund")]public async Task<IActionResult> Credit(Guid id){await new StoreCreditService(db,new LedgerService(db)).IssueAsync(id,Actor);return Redirect("/admin/operations/returns");}

}
