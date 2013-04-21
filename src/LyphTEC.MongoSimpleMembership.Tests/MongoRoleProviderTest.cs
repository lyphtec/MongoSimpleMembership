using System;
using System.Collections.Specialized;
using System.Configuration.Provider;
using System.Linq;
using ServiceStack.Text;
using Xunit;

namespace LyphTEC.MongoSimpleMembership.Tests
{
    // ReSharper disable InconsistentNaming
    public class MongoRoleProviderTest : IUseFixture<RoleProviderTestFixture>
    {
        private RoleProviderTestFixture _fixture;
        private MongoRoleProvider _provider;

        #region IUseFixture<RoleProviderTestFixture> Members

        public void SetFixture(RoleProviderTestFixture data)
        {
            _fixture = data;
            _provider = data.Provider;
        }

        #endregion

        [Fact]
        public void ThrowsException_When_ConnectionStringNotConfigured()
        {
            var provider = new MongoRoleProvider();

            var config = new NameValueCollection();

            Assert.Throws<ProviderException>(() => provider.Initialize("Test", config)).PrintDump();
        }

        [Fact]
        public void CreateRole()
        {
            _provider.CreateRole("NewRole");

            Assert.Equal(4, _fixture.Roles.Count());

            _fixture.Roles.PrintDump();

            Assert.Throws<ProviderException>(() => _provider.CreateRole("Admin")).PrintDump();
        }

        [Fact]
        public void AddUsersToRoles()
        {
            var usernames = new[] { "User1", "User2" };
            var rolesArr = new[] { "User","Guest" };

            Assert.Throws<ArgumentNullException>(() => _provider.AddUsersToRoles(null, rolesArr));
            Assert.Throws<ArgumentNullException>(() => _provider.AddUsersToRoles(usernames, new string[] {}));
            
            _provider.AddUsersToRoles(usernames, rolesArr);

            Assert.Equal(3, _fixture.Users.Single(x => x.UserName == "User1").Roles.Count);
            
            _fixture.Users.PrintDump();
        }

        [Fact]
        public void DeleteRole()
        {
            var result = _provider.DeleteRole("Guest", false);

            Assert.Equal(2, _fixture.Roles.Count());
            Assert.True(result);
            
            _fixture.Roles.PrintDump();

            Assert.Throws<ProviderException>(() => _provider.DeleteRole("Admin", true)).PrintDump();

            // Users with role assigned
            var result2 = _provider.DeleteRole("Admin", false);
            Assert.True(result2);

            var user = _fixture.Users.Single(x => x.UserId == 1);
            Assert.Equal(0, user.Roles.Count);
            user.PrintDump();
        }

        [Fact]
        public void FindUsersInRole()
        {
            var results = _provider.FindUsersInRole("Admin", "User.");

            Assert.Equal(1, results.Length);
            Assert.Equal("User1", results[0]);

            _provider.AddUsersToRoles(new[] { "User1","User2" }, new[] { "Guest" });

            var results2 = _provider.FindUsersInRole("Guest", ".[12]$");

            Assert.Equal(2, results2.Length);
            
            results2.PrintDump();
        }

        [Fact]
        public void GetAllRoles()
        {
            var results = _provider.GetAllRoles();

            Assert.Equal(3, results.Length);

            _fixture.Roles.PrintDump();
        }

        [Fact]
        public void GetRolesForUser()
        {
            var results = _provider.GetRolesForUser("User1");

            Assert.Equal(1, results.Length);

            var results2 = _provider.GetRolesForUser("user1");      // lower case

            Assert.Equal(1, results2.Length);

            Assert.Throws<ProviderException>(() => _provider.GetRolesForUser("dummy")).PrintDump();
        }

        [Fact]
        public void GetUsersInRole()
        {
            var result = _provider.GetUsersInRole("Admin");

            Assert.Equal(1, result.Length);
            
            _provider.AddUsersToRoles(new[] { "User1","User2" }, new[] { "Guest" });

            var result2 = _provider.GetUsersInRole("Guest");

            Assert.Equal(2, result2.Length);

            Assert.Throws<ProviderException>(() => _provider.GetUsersInRole("dummy")).PrintDump();
        }

        [Fact]
        public void IsUserInRole()
        {
            var result = _provider.IsUserInRole("User1", "Admin");

            Assert.True(result);

            var result2 = _provider.IsUserInRole("User2", "Admin");

            Assert.False(result2);
        }

        [Fact]
        public void RemoveUsersFromRoles()
        {
            _provider.RemoveUsersFromRoles(new[]{ "User1" }, new[] { "Admin" });

            var user = _fixture.Users.SingleOrDefault(x => x.UserNameLower == "user1");

            Assert.Equal(0, user.Roles.Count);

            user.PrintDump();
        }

        [Fact]
        public void RoleExists()
        {
            var result = _provider.RoleExists("Guest");

            Assert.True(result);

            var result2 = _provider.RoleExists("dummy");

            Assert.False(result2);
        }
    }
    // ReSharper restore InconsistentNaming
}
