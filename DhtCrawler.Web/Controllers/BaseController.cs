using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DhtCrawler.Web.Controllers
{
    public class BaseController : Controller
    {
        private const string UserIdCookieName = "uid";
        public string UserId
        {
            get
            {
                if (Request.Cookies.TryGetValue(UserIdCookieName, out var userId))
                {
                    return userId;
                }
                userId = Guid.NewGuid().ToString("N");
                Response.Cookies.Append(UserIdCookieName, userId, new CookieOptions() { Expires = DateTimeOffset.MaxValue, HttpOnly = true });
                return userId;
            }
        }
    }
}
