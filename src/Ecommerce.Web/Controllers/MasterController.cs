using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Ecommerce.Domain;
using Ecommerce.Application;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;
[Authorize,Route("admin/data/{module}")]
public class MasterController(AppDb db,AccessService access,LedgerService ledger,IWebHostEnvironment env):Controller {
 IQueryable<Entity> Query(Type t)=>(IQueryable<Entity>)typeof(DbContext).GetMethod(nameof(DbContext.Set),Type.EmptyTypes)!.MakeGenericMethod(t).Invoke(db,null)!;
 async Task<bool> Allowed(string m,string action)=>MasterModules.Types.ContainsKey(m)&&await access.HasAsync(User,MasterModules.Permission(m,action));
 [HttpGet("")]public async Task<IActionResult> Index(string module,string q="",int page=1){if(!await Allowed(module,"view"))return Forbid();var t=MasterModules.Types[module];var rows=await Query(t).OrderByDescending(x=>x.CreatedAt).Take(2000).ToListAsync();var fields=MasterModules.Fields(t).Where(f=>f.Name!="Description"&&f.Name!="Body").OrderBy(f=>f.Name=="Name"?0:1).Take(8).ToArray();if(q.Length>0)rows=rows.Where(x=>fields.Any(f=>f.GetValue(x)?.ToString()?.Contains(q,StringComparison.OrdinalIgnoreCase)==true)).ToList();var data=rows.Skip((Math.Max(1,page)-1)*50).Take(50).Select(x=>new[]{x.Id.ToString()}.Concat(fields.Select(f=>f.GetValue(x)?.ToString()??"")).ToArray()).ToList();ViewBag.Q=q;ViewBag.Page=page;return View(new TableVm(module,new[]{"Id"}.Concat(fields.Select(f=>f.Name)).ToArray(),data,module));}
 async Task<EditorVm> Editor(string module,Entity entity){var fields=MasterModules.Fields(entity.GetType());var choices=new Dictionary<string,List<(string,string)>>();foreach(var f in fields.Where(f=>f.PropertyType==typeof(Guid)||f.PropertyType==typeof(Guid?))){var nav=entity.GetType().GetProperty(f.Name[..^2]);if(nav==null||!typeof(Entity).IsAssignableFrom(nav.PropertyType))continue;var rows=await Query(nav.PropertyType).Take(1000).ToListAsync();choices[f.Name]=rows.Select(x=>(x.Id.ToString(),(x as NamedEntity)?.Name??x.Id.ToString())).ToList();}return new EditorVm(module,entity,fields,choices);}
 [HttpGet("edit/{id:guid?}")]public async Task<IActionResult> Edit(string module,Guid? id){if(!await Allowed(module,id.HasValue?"edit":"create"))return Forbid();var type=MasterModules.Types[module];var e=id.HasValue?await db.FindAsync(type,id.Value) as Entity:Activator.CreateInstance(type) as Entity;if(e==null)return NotFound();return View(await Editor(module,e));}
 [HttpPost("save")]
 public async Task<IActionResult> Save(string module,Guid id,string? version){
  if(!MasterModules.Types.TryGetValue(module,out var type))return NotFound();var entity=await db.FindAsync(type,id) as Entity;var create=entity==null;
  if(!await Allowed(module,create?"create":"edit"))return Forbid();entity??=(Entity)Activator.CreateInstance(type)!;
  if(!create){if(string.IsNullOrEmpty(version))return BadRequest("Reload this record before saving.");db.Entry(entity).Property(x=>x.Version).OriginalValue=Convert.FromBase64String(version);}
  var previousStoreWarehouse=(entity as Store)?.WarehouseId;
  var previousAccountCode=(entity as AccountHead)?.Code;
  var before=Snapshot(entity);var fields=MasterModules.Fields(type);
  foreach(var field in fields){var raw=Request.Form[field.Name].FirstOrDefault();var target=Nullable.GetUnderlyingType(field.PropertyType)??field.PropertyType;try{object? value;if(target==typeof(bool))value=raw=="true";else if(string.IsNullOrWhiteSpace(raw)&&Nullable.GetUnderlyingType(field.PropertyType)!=null)value=null;else if(target==typeof(Guid))value=Guid.Parse(raw??"");else if(target==typeof(DateTime))value=DateTime.Parse(raw??"",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal);else value=Convert.ChangeType(raw??"",target,CultureInfo.InvariantCulture);field.SetValue(entity,value);}catch{ModelState.AddModelError(field.Name,"Invalid "+field.Name);}}
  var errors=new List<ValidationResult>();Validator.TryValidateObject(entity,new ValidationContext(entity),errors,true);foreach(var e in errors)ModelState.AddModelError(e.MemberNames.FirstOrDefault()??"",e.ErrorMessage??"Invalid value");
  if(entity is Store store){if(create)ModelState.AddModelError("","Create stores from Operations / Inventory to allocate an independent stock location.");else if(store.WarehouseId!=previousStoreWarehouse)ModelState.AddModelError("WarehouseId","Use stock transfers; store inventory locations cannot be remapped.");}
  if(entity is Category c){var seen=new HashSet<Guid>{c.Id};var parent=c.ParentId;while(parent.HasValue){if(!seen.Add(parent.Value)){ModelState.AddModelError("ParentId","Category hierarchy cannot contain a cycle.");break;}parent=await db.Set<Category>().Where(x=>x.Id==parent).Select(x=>x.ParentId).SingleOrDefaultAsync();}}
  if(entity is AccountHead head&&!create&&previousAccountCode!=head.Code&&new[]{"1000","1010","1100","1200","2000","2100","2200","3100","4000","4100","4200","5000","6000"}.Contains(previousAccountCode))ModelState.AddModelError("Code","System control account code cannot be renamed.");
  if(entity is Banner b&&(!b.Link.StartsWith('/')||b.Link.StartsWith("//")||!b.ImagePath.StartsWith('/')||b.ImagePath.StartsWith("//")||b.EndsAt<=b.StartsAt))ModelState.AddModelError("","Use local banner paths and valid dates.");
  if(entity is Campaign ca&&(ca.EndsAt<=ca.StartsAt||ca.MaximumDiscount<0||ca.MinimumPurchase<0||ca.UsageLimit<1||ca.PerCustomerLimit<1))ModelState.AddModelError("","Invalid campaign dates.");
  if(entity is AccountingPeriod period&&period.EndsAt<period.StartsAt)ModelState.AddModelError("","Invalid accounting period.");
  if(entity is Coupon coupon&&(coupon.EndsAt<=coupon.StartsAt||coupon.MaximumDiscount<0||coupon.MinimumPurchase<0||coupon.UsageLimit<1||coupon.PerCustomerLimit<1))ModelState.AddModelError("","Invalid coupon dates or limits.");
  if(entity is DeliveryZone z&&(z.Charge<0||z.FreeAbove<0))ModelState.AddModelError("","Shipping values cannot be negative.");
  if(entity is MenuItem menu&&(!menu.Url.StartsWith('/')||menu.Url.StartsWith("//")))ModelState.AddModelError("Url","Use a local application URL.");
  if(!ModelState.IsValid)return View("Edit",await Editor(module,entity));
  if(create)db.Add(entity);ledger.Audit(User.FindFirstValue(ClaimTypes.NameIdentifier)!,create?"Create":"Edit",entity,before,Snapshot(entity));await db.SaveChangesAsync();
  if(entity is Product product&&Request.Form.Files.Count>0)await SaveProductImages(product.Id,Request.Form.Files);
  TempData["Message"]=entity is Product&&Request.Form.Files.Count>0?"Product and images saved successfully.":"Saved successfully.";return RedirectToAction(nameof(Index),new{module});
 }
 async Task SaveProductImages(Guid productId,IFormFileCollection files){
  var imageFiles=files.Where(x=>x.Name=="productImages"&&x.Length>0).Take(8).ToList();if(imageFiles.Count==0)return;
  var existing=await db.Set<ProductFile>().CountAsync(x=>x.ProductId==productId&&x.FileType.StartsWith("image/"));if(existing+imageFiles.Count>8)throw new BusinessException("A product can have up to 8 images.");
  var folder=Path.Combine(env.WebRootPath,"Docs","Inv",productId.ToString("N"));Directory.CreateDirectory(folder);var written=new List<string>();
  try{foreach(var file in imageFiles){if(file.Length>5*1024*1024)throw new BusinessException("Each product image must be 5 MB or smaller.");var ext=Path.GetExtension(file.FileName).ToLowerInvariant();using var mem=new MemoryStream();await file.CopyToAsync(mem);var bytes=mem.ToArray();var valid=ext switch {".png"=>file.ContentType=="image/png"&&bytes.Length>8&&bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),".jpg" or ".jpeg"=>file.ContentType=="image/jpeg"&&bytes.Length>3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255,".webp"=>file.ContentType=="image/webp"&&bytes.Length>12&&System.Text.Encoding.ASCII.GetString(bytes,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(bytes,8,4)=="WEBP",_=>false};if(!valid)throw new BusinessException("Only signature-checked PNG, JPEG and WebP product images are accepted.");
    var name=Guid.NewGuid().ToString("N")+ext;var thumbName=Guid.NewGuid().ToString("N")+".png";var path=Path.Combine(folder,name);var thumbPath=Path.Combine(folder,thumbName);await System.IO.File.WriteAllBytesAsync(path,bytes);written.Add(path);var thumb=await ProductMedia.ThumbnailAsync(bytes,file.ContentType);await System.IO.File.WriteAllBytesAsync(thumbPath,thumb);written.Add(thumbPath);var primary=existing==0&&!db.Set<ProductFile>().Local.Any(x=>x.ProductId==productId&&x.IsPrimary);db.Add(new ProductFile{ProductId=productId,FileName=name,FilePath="/Docs/Inv/"+productId.ToString("N")+"/"+name,FileType=file.ContentType,ThumbnailPath="/Docs/Inv/"+productId.ToString("N")+"/"+thumbName,SortOrder=existing++,IsPrimary=primary});}
   await db.SaveChangesAsync();}
  catch{foreach(var path in written)if(System.IO.File.Exists(path))System.IO.File.Delete(path);throw;}
 }

 string Snapshot(Entity e)=>JsonSerializer.Serialize(MasterModules.Fields(e.GetType()).ToDictionary(f=>f.Name,f=>f.GetValue(e)));
 [HttpPost("archive")]public async Task<IActionResult> Archive(string module,Guid id){if(!await Allowed(module,"delete"))return Forbid();var e=await db.FindAsync(MasterModules.Types[module],id) as Entity;if(e==null)return NotFound();if(e is NamedEntity n)n.Active=false;else if(e is PermissionGrant or MenuItem or RoleMenuPermission or UserMenuPermission)db.Remove(e);else return BadRequest("This record cannot be deleted.");ledger.Audit(User.FindFirstValue(ClaimTypes.NameIdentifier)!,"Archive",e);await db.SaveChangesAsync();return RedirectToAction(nameof(Index),new{module});}
}
