# SimpleMembership providers using MongoDB as the backing store

This this a SimpleMembership implementation using MongoDB as the backing store that provides a custom Membership provider (ExtendedMembershipProvider to be exact) and Role provider.

For more information about SimpleMembership see this post by Jon Galloway : [SimpleMembership, Membership Providers, Universal Providers and the new ASP.NET 4.5 Web Forms and ASP.NET MVC 4 templates](http://weblogs.asp.net/jgalloway/archive/2012/08/29/simplemembership-membership-providers-universal-providers-and-the-new-asp-net-4-5-web-forms-and-asp-net-mvc-4-templates.aspx)

### Installation

Available on [NuGet](http://nuget.org/packages/LyphTEC.MongoSimpleMembership/):

```
PM> Install-Package LyphTEC.MongoSimpleMembership
```

### Required changes

You must complete the following steps after you have installed the NuGet package before your app will run:

1.  Configure your data connection string to the MongoDB database

    eg.

    ```xml
      <connectionStrings>
        <clear/>
        <add name="MongoSimpleMembership" connectionString="mongodb://localhost/SimpleMembership?safe=true"  />
        </connectionStrings>
    ```

    Note that the connection string must also include the database name. In the example above, this will use the database named "SimpleMembership".   

2.  Update the system.web/membership & system.web/roleManager sections to use the connection string name as defined above

    Also make sure that the "defaultProvider" attributes are set to use the matching MongoSimpleMembership provider names.

    eg.

    ```xml
      <membership defaultProvider="MongoSimpleMembershipProvider">
        <providers>
          <clear />
          <add name="MongoSimpleMembershipProvider" type="LyphTEC.MongoSimpleMembership.MongoSimpleMembershipProvider, LyphTEC.MongoSimpleMembership" connectionStringName="MongoSimpleMembership" />
        </providers>
      </membership>

      <roleManager enabled="true" defaultProvider="MongoRoleProvider">
        <providers>
          <clear />
          <add name="MongoRoleProvider" type="LyphTEC.MongoSimpleMembership.MongoRoleProvider, LyphTEC.MongoSimpleMembership" connectionStringName="MongoSimpleMembership" />
        </providers>
      </roleManager>
    ```
   
3. If you are using the default "ASP.NET MVC4 Internet" template. You must make some changes to AccountController:

*   Remove the [InitializeSimpleMembership] attribute (this is the ActionFilterAttribute defined in Filters/InitializeSimpleMembershipAttribute.cs).
    This used by the default SimpleMembershipProvider that needs to initialize the SQL Server to setup the database.
    Since we are no longer using SQL Server, this is no longer required.

*   Make changes to the ExternalLoginConfirmation() method to remove SQL Server related hooks used by the default SimpleMembershipProvider.

    Here's a complete example: 

    ```csharp
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ExternalLoginConfirmation(RegisterExternalLoginModel model, string returnUrl)
        {
            string provider = null;
            string providerUserId = null;

            if (User.Identity.IsAuthenticated || !OAuthWebSecurity.TryDeserializeProviderUserId(model.ExternalLoginData, out provider, out providerUserId))
            {
                return RedirectToAction("Manage");
            }

            if (ModelState.IsValid)
            {
                var userId = WebSecurity.GetUserId(model.UserName);

                if (userId == -1)
                {
                    // TODO : Add custom user profile logic

                    // this will create a non-local account
                    OAuthWebSecurity.CreateOrUpdateAccount(provider, providerUserId, model.UserName);
                    OAuthWebSecurity.Login(provider, providerUserId, createPersistentCookie: false);

                    return RedirectToLocal(returnUrl);
                }
                else
                {
                    ModelState.AddModelError("UserName", "User name already exists. Please enter a different user name.");
                }
            }

            ViewBag.ProviderDisplayName = OAuthWebSecurity.GetOAuthClientData(provider).DisplayName;
            ViewBag.ReturnUrl = returnUrl;
            return View(model);
        }
    ```

    Checkout a working [sample MVC4 app here](https://github.com/lyphtec/LyphTEC.MongoSimpleMembership/tree/master/src/LyphTEC.MongoSimpleMembership.Sample)   
