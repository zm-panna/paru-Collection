using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
namespace Ecommerce.Web.Controllers;
[Route("account"),EnableRateLimiting("account")]
public class PasswordController(UserManager<IdentityUser> users,IConfiguration config):Controller {
 [HttpGet("forgot")]public IActionResult Forgot()=>View();
 [HttpPost("forgot")]public async Task<IActionResult> Forgot(string email){
  var user=await users.FindByEmailAsync(email??"");
  if(user!=null&&!string.IsNullOrWhiteSpace(config["Smtp:Host"])&&!string.IsNullOrWhiteSpace(config["Smtp:From"])){
   var token=WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GeneratePasswordResetTokenAsync(user)));
   var root=config["PublicBaseUrl"]?.TrimEnd('/');
   if(Uri.TryCreate(root,UriKind.Absolute,out var uri)&&uri.Scheme=="https"){
    var link=root+"/account/reset?email="+Uri.EscapeDataString(email!)+"&token="+token;
    using var smtp=new SmtpClient(config["Smtp:Host"],config.GetValue<int>("Smtp:Port",587)){EnableSsl=true,Credentials=new NetworkCredential(config["Smtp:Username"],config["Smtp:Password"])};
    using var message=new MailMessage(config["Smtp:From"]!,user.Email!,"Reset your Paru Collection password","Open this link to reset your password: "+link+"\nIf you did not request this, ignore this email.");
    try{await smtp.SendMailAsync(message);}catch(SmtpException){/* Generic response prevents account enumeration. Configure SMTP monitoring outside request logs. */}
   }
  }
  ViewBag.Message="If that account exists and email delivery is configured, a reset link has been sent.";return View();
 }
 [HttpGet("reset")]public IActionResult Reset(string email,string token){ViewBag.Email=email;ViewBag.Token=token;return View();}
 [HttpPost("reset")]public async Task<IActionResult> Reset(string email,string token,string password){var user=await users.FindByEmailAsync(email??"");if(user==null){ModelState.AddModelError("","Invalid reset request.");}else{try{var decoded=Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token));var r=await users.ResetPasswordAsync(user,decoded,password);if(r.Succeeded){TempData["Message"]="Password reset. Sign in with your new password.";return Redirect("/account/login");}foreach(var e in r.Errors)ModelState.AddModelError("",e.Description);}catch(FormatException){ModelState.AddModelError("","Invalid reset token.");}}ViewBag.Email=email;ViewBag.Token=token;return View();}
}
