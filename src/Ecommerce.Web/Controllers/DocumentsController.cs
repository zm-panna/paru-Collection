using Ecommerce.Domain;
using Ecommerce.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Ecommerce.Web.Controllers;
public class DocumentsController(AppDb db,IWebHostEnvironment env):Controller{
 [HttpGet("documents/{id:guid}")]public async Task<IActionResult> Download(Guid id){var f=await db.Set<ProductFile>().SingleOrDefaultAsync(x=>x.Id==id&&x.Product.Active&&x.FileType=="application/pdf");if(f==null)return NotFound();var path=Path.Combine(env.ContentRootPath,"App_Data","documents",Path.GetFileName(f.FileName));if(!System.IO.File.Exists(path))return NotFound();return PhysicalFile(path,"application/pdf",f.FileName);}
}
