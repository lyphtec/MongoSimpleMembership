using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Configuration.Provider;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Security;
using LyphTEC.MongoSimpleMembership.Extensions;
using LyphTEC.MongoSimpleMembership.Helpers;
using LyphTEC.MongoSimpleMembership.Models;
using LyphTEC.MongoSimpleMembership.Services;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace LyphTEC.MongoSimpleMembership
{
    public class MongoRoleProvider : RoleProvider
    {
        private string _appName;
        private MongoDataContext _context;
        private bool _isInitialized = false;

        public IMongoDatabase Database
        {
            get
            {
                VerifyInitialize();

                return _context.Database;
            }
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            Check.IsNotNull(config);

            if (string.IsNullOrWhiteSpace(name))
                name = "MongoRoleProvider";

            if (string.IsNullOrWhiteSpace(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "MongoDB Role Provider");
            }

            base.Initialize(name, config);

            var connStringName = Util.GetValueOrDefault(config, "connectionStringName", o => o.ToString(), string.Empty);

            if (string.IsNullOrWhiteSpace(connStringName))
                throw new ProviderException("connectionStringName not specified");

            var connSettings = ConfigurationManager.ConnectionStrings[connStringName];
            Util.CheckConnectionStringSettings(connStringName, connSettings);

            InitializeContext(connSettings.ConnectionString);

            _appName = Util.GetValueOrDefault(config, "applicationName", o => o.ToString(), Util.GetDefaultAppName());
            Util.CheckAppNameLength(_appName);

            config.Remove("connectionStringName");
            config.Remove("applicationName");
            config.Remove("commandTimeout");
            config.Remove("name");

            if (config.Count <= 0)
            {
                _isInitialized = true;
                return;
            }

            var unrecognized = config.GetKey(0);
            if (!string.IsNullOrWhiteSpace(unrecognized))
                throw new ProviderException(string.Format("Config setting '{0}' is not supported by this provider.", unrecognized));
        }

        private void InitializeContext(string connectionString)
        {
            Check.IsNotNull(connectionString);

            try
            {
                _context = new MongoDataContext(connectionString);
            }
            catch (Exception ex)
            {
                throw new ProviderException("Error initializing the provider. See inner exception for details.", ex);
            }
        }

        private void VerifyInitialize()
        {
            if (!_isInitialized)
                throw new ProviderException("The provider hasn't been initialized yet. Please call Initialize() first.");
        }

        public override void AddUsersToRoles(string[] usernames, string[] roleNames)
        {
            Check.Requires<ArgumentNullException>(usernames != null && usernames.Length > 0);
            Check.Requires<ArgumentNullException>(roleNames != null && roleNames.Length > 0);

            VerifyInitialize();

            var usernamesLower = usernames.Select(x => x.ToLowerInvariant());

            var users = _context.Users.Where(x => usernamesLower.Contains(x.UserNameLower));
            var roles = _context.Roles.Where(x => roleNames.Contains(x.RoleName)).Select(x => x.RoleName).ToList();

            foreach (var user in users)
            {
                if (user.Roles == null)
                    user.Roles = new List<string>();

                if (user.Roles.Any())
                {
                    var newRoles = roles.Except(user.Roles);
                    user.Roles.AddRange(newRoles);
                }
                else
                {
                    user.Roles.AddRange(roles);
                }

                _context.Save(user);
            }
        }

        public override string ApplicationName
        {
            get { return _appName; }
            set
            {
                Util.CheckAppNameLength(value);

                _appName = value;
            }
        }

        public override void CreateRole(string roleName)
        {
            Check.IsNotNull(roleName);

            VerifyInitialize();

            if (_context.Roles.Any(x => x.RoleName == roleName))
                throw new ProviderException(string.Format("Role '{0}' already exist.", roleName));

            _context.Save(new Role(roleName));
        }

        public override bool DeleteRole(string roleName, bool throwOnPopulatedRole)
        {
            Check.IsNotNull(roleName); 

            VerifyInitialize();

            var usersWithRole = _context.Users.Where(x => x.Roles != null && x.Roles.Contains(roleName));

            if (throwOnPopulatedRole && usersWithRole.Any())
                throw new ProviderException(string.Format("Cannot delete role '{0}' as it contains users", roleName));

            var role = _context.GetRole(roleName);

            if (role != null)
            {
                _context.RemoveById<Role>(role.RoleId);

                // Also delete any that are currently assigned
                usersWithRole.ForEach(x =>
                                          {
                                              x.Roles.Remove(role.RoleName);
                                              _context.Save(x);
                                          }
                    );
            }

            return true;
        }

        // This actually supports wildcard & partial username matches
        // In our implementation, we support standard Regex pattern expression for usernameToMatch
        public override string[] FindUsersInRole(string roleName, string usernameToMatchRegex)
        {
            Check.IsNotNull(roleName); 
            Check.IsNotNull(usernameToMatchRegex); 

            VerifyInitialize();

            // Using IsMatch here : http://www.mongodb.org/display/DOCS/CSharp+Driver+LINQ+Tutorial#CSharpDriverLINQTutorial-IsMatch%28regularexpressionmethod%29
            var users = _context.Users.Where(x => Regex.IsMatch(x.UserNameLower, usernameToMatchRegex, RegexOptions.IgnoreCase) && x.Roles.Contains(roleName));

            return !users.Any() ? null : users.Select(x => x.UserName).ToList().OrderBy(x => x).ToArray();
        }

        public override string[] GetAllRoles()
        {
            VerifyInitialize();

            return _context.Roles.Select(x => x.RoleName).ToList().OrderBy(x => x).ToArray();
        }

        public override string[] GetRolesForUser(string username)
        {
            Check.IsNotNull(username); 

            VerifyInitialize();

            var user = _context.GetUser(username);
            if (user == null)
                throw new ProviderException(string.Format("No user found matching username '{0}'", username));

            return user.Roles.OrderBy(x => x).ToArray();
        }

        public override string[] GetUsersInRole(string roleName)
        {
            Check.IsNotNull(roleName); 

            VerifyInitialize();

            if (!RoleExists(roleName))
                throw new ProviderException("Role does not exist");

            return _context.Users.Where(x => x.Roles.Contains(roleName)).Select(x => x.UserName).ToList().OrderBy(x => x).ToArray();
        }

        public override bool IsUserInRole(string username, string roleName)
        {
            Check.IsNotNull(username);
            Check.IsNotNull(roleName);

            VerifyInitialize();

            var user = _context.GetUser(username);

            if (user == null || user.Roles == null || user.Roles.Count == 0)
                return false;

            return user.Roles.Contains(roleName);
        }

        public override void RemoveUsersFromRoles(string[] usernames, string[] roleNames)
        {
            Check.Requires<ArgumentNullException>(usernames != null && usernames.Length > 0);
            Check.Requires<ArgumentNullException>(roleNames != null && roleNames.Length > 0);

            VerifyInitialize();

            var usernamesLower = usernames.Select(x => x.ToLowerInvariant());

            var users = _context.Users.Where(x => usernamesLower.Contains(x.UserNameLower) && x.Roles != null && x.Roles.Any());
            foreach (var user in users)
            {
                user.Roles.RemoveAll(x => roleNames.Contains(x));

                _context.Save(user);
            }
        }

        public override bool RoleExists(string roleName)
        {
            Check.IsNotNull(roleName); 
            
            VerifyInitialize();

            return _context.Roles.Any(x => x.RoleName == roleName);
        }
    }
}
