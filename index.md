---
layout: default
title: MongoSimpleMembership
tagline: SimpleMembership providers using MongoDB as the backing store
---
{% include JB/setup %}

# SimpleMembership providers using MongoDB as the backing store

This this a SimpleMembership implementation using MongoDB as the backing store that provides a custom Membership provider (ExtendedMembershipProvider to be exact) and Role provider.

For more information about SimpleMembership see this post by Jon Galloway : [SimpleMembership, Membership Providers, Universal Providers and the new ASP.NET 4.5 Web Forms and ASP.NET MVC 4 templates](http://weblogs.asp.net/jgalloway/archive/2012/08/29/simplemembership-membership-providers-universal-providers-and-the-new-asp-net-4-5-web-forms-and-asp-net-mvc-4-templates.aspx)

## Installation

Available on [NuGet](http://nuget.org/packages/LyphTEC.MongoSimpleMembership/):

```
PM> Install-Package LyphTEC.MongoSimpleMembership
```

## Required changes

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
   
3.  If you are using the default "ASP.NET MVC4 Internet" template. You must make some changes to AccountController:

*   Remove the `[InitializeSimpleMembership]` attribute (the ActionFilterAttribute defined in Filters/InitializeSimpleMembershipAttribute.cs).
    This is used by the default SimpleMembershipProvider that needs to initialize SQL Server.
    Since we are not using SQL Server, this is no longer required.

*   Make changes to the ExternalLoginConfirmation() method to remove Entity Framework related hooks used by the default SimpleMembershipProvider.

    Here's what it looks like before the change: 

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
            // Insert a new user into the database
            using (UsersContext db = new UsersContext())
            {
                UserProfile user = db.UserProfiles.FirstOrDefault(u => u.UserName.ToLower() == model.UserName.ToLower());
                // Check if user already exists
                if (user == null)
                {
                    // Insert name into the profile table
                    db.UserProfiles.Add(new UserProfile { UserName = model.UserName });
                    db.SaveChanges();

                    OAuthWebSecurity.CreateOrUpdateAccount(provider, providerUserId, model.UserName);
                    OAuthWebSecurity.Login(provider, providerUserId, createPersistentCookie: false);

                    return RedirectToLocal(returnUrl);
                }
                else
                {
                    ModelState.AddModelError("UserName", "User name already exists. Please enter a different user name.");
                }
            }
        }

        ViewBag.ProviderDisplayName = OAuthWebSecurity.GetOAuthClientData(provider).DisplayName;
        ViewBag.ReturnUrl = returnUrl;
        return View(model);
    }
    ```
    
    And here's what it should look like after the change:   

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
                // check if user exists                
                if (!WebSecurity.UserExists(model.UserName))
                {
                    // TODO : Add custom user profile logic here

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

    See the [sample MVC4 app](https://github.com/lyphtec/MongoSimpleMembership/tree/master/src/LyphTEC.MongoSimpleMembership.Sample) for reference.   

## Customising the storage collection names

By default, the persisted [entities](https://github.com/lyphtec/MongoSimpleMembership/tree/master/src/LyphTEC.MongoSimpleMembership/Models) are stored in collections with the following names:

![Default collection names](http://static.lyphtec.com/projects/msm/default_collections.png)

This matches the table names used in the standard SQL Server based SimpleMembershipProvider.

If desired, you can change the default collection names by using the following appSettings:

```xml
  <appSettings>
    <add key="MongoSimpleMembership:MembershipAccountName" value="webpages_Membership" />
    <add key="MongoSimpleMembership:OAuthMembershipName" value="webpages_OAuthMembership" />
    <add key="MongoSimpleMembership:OAuthTokenName" value="webpages_OAuthToken" />
    <add key="MongoSimpleMembership:RoleName" value="webpages_Role" />
  </appSettings>
```

## License

[Apache License, Version 2.0](https://github.com/lyphtec/MongoSimpleMembership/blob/master/license.txt)
