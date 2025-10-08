using System.Web;
using System.Web.Mvc;

namespace OnlineJewelryStore.Filters
{
    public class AdminAuthorizeAttribute : AuthorizeAttribute
    {
        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            if (httpContext.Session["UserID"] == null)
                return false;

            return httpContext.Session["UserRole"]?.ToString() == "Administrator";
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            filterContext.Result = new RedirectResult("~/Account/Login");
        }
    }
}