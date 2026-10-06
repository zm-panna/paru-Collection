using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;
[Authorize(Roles="ADMIN,Admin,SuperAdmin"), Route("admin/company-settings")]
public class CompanySettingsController(AppDb db, IWebHostEnvironment env): Controller
{
 public static readonly string[] Keys={"CompanyName","CompanyAddress","CompanyPhone","CompanyEmail","CompanyWebsite","LogoUrl","FaviconUrl","BannerUrl","BackgroundImageUrl","PrimaryColor","AccentColor","BackgroundColor","WhatsAppNumber","MessengerUrl","ReportAddress","ReportFooter"};
 [HttpGet("")] public async Task<IActionResult> Index() {
 var rows=await db.Set<BusinessSetting>().Where(x=>x.Group=="Brand").ToListAsync();
 return View(Keys.ToDictionary(k=>k,k=>rows.FirstOrDefault(x=>x.Name==k)?.Value??"")); }
 [HttpPost(""), RequestSizeLimit(22*1024*1024)] public async Task<IActionResult> Save(CancellationToken ct) {
 var form=await Request.ReadFormAsync(ct); var values=Keys.ToDictionary(k=>k,k=>form[k].ToString().Trim());
 foreach(var v in values) if(v.Value.Length>1000) ModelState.AddModelError(v.Key,"Maximum 1000 characters.");
 if(string.IsNullOrWhiteSpace(values["CompanyName"])) ModelState.AddModelError("CompanyName","Company name is required.");
 foreach(var k in new[]{"PrimaryColor","AccentColor","BackgroundColor"}) if(!string.IsNullOrEmpty(values[k])&&BrandingService.Color(values[k],"")=="") ModelState.AddModelError(k,"Use #RRGGBB.");
 foreach(var k in new[]{"LogoUrl","FaviconUrl","BannerUrl","BackgroundImageUrl","CompanyWebsite"}) if(values[k]!=""&&BrandingService.SafeUrl(values[k])=="") ModelState.AddModelError(k,"Use a local /path or HTTPS URL.");
 if(values["MessengerUrl"]!=""&&BrandingService.SafeMessenger(values["MessengerUrl"])=="") ModelState.AddModelError("MessengerUrl","Use https://m.me/page-name or a messenger.com URL.");
 var number=values["WhatsAppNumber"].Replace("+","").Replace(" ","").Replace("-",""); values["WhatsAppNumber"]=number;
 if(number!=""&&!System.Text.RegularExpressions.Regex.IsMatch(number,@"^[1-9][0-9]{7,14}$")) ModelState.AddModelError("WhatsAppNumber","Include country code, for example 8801XXXXXXXXX.");
 var uploads=new List<(string Key,string Extension,byte[] Bytes)>();
 foreach(var k in new[]{"LogoUrl","FaviconUrl","BannerUrl","BackgroundImageUrl"}) {
 var file=form.Files.GetFile(k+"File"); if(file==null||file.Length==0)continue;
 if(file.Length>5*1024*1024){ModelState.AddModelError(k,"Maximum 5 MB per image.");continue;}
 using var mem=new MemoryStream(); await file.CopyToAsync(mem,ct); var b=mem.ToArray(); var ext=Path.GetExtension(file.FileName).ToLowerInvariant();
 bool valid=ext switch {".png"=>b.Length>8&&b.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),".jpg" or ".jpeg"=>b.Length>3&&b[0]==255&&b[1]==216&&b[2]==255,".webp"=>b.Length>12&&System.Text.Encoding.ASCII.GetString(b,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(b,8,4)=="WEBP",_=>false};
 if(!valid)ModelState.AddModelError(k,"Upload PNG, JPEG or WebP."); else uploads.Add((k,ext,b)); }
 if(!ModelState.IsValid)return View("Index",values);
 var written=new List<string>();
 try {
 var folder=Path.Combine(env.WebRootPath,"Docs","Brand");Directory.CreateDirectory(folder);
 foreach(var upload in uploads){var name=Guid.NewGuid().ToString("N")+upload.Extension;var path=Path.Combine(folder,name);written.Add(path);await System.IO.File.WriteAllBytesAsync(path,upload.Bytes,ct);values[upload.Key]="/Docs/Brand/"+name;}
 var rows=await db.Set<BusinessSetting>().Where(x=>x.Group=="Brand").ToListAsync(ct);
 foreach(var v in values){var row=rows.FirstOrDefault(x=>x.Name==v.Key);if(row==null){row=new BusinessSetting{Name=v.Key,Group="Brand"};db.Add(row);}row.Value=v.Value;row.Active=true;}
 await db.SaveChangesAsync(ct);
 } catch {foreach(var path in written)System.IO.File.Delete(path);throw;}
 TempData["Message"]="Company branding and contact settings saved.";return RedirectToAction(nameof(Index)); }
}
