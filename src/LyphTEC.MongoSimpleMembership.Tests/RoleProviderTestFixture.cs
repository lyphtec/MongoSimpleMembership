using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using LyphTEC.MongoSimpleMembership.Models;
using MongoDB.Driver;

namespace LyphTEC.MongoSimpleMembership.Tests
{
    public class RoleProviderTestFixture
    {
        public MongoRoleProvider Provider { get; protected set; }
        public IQueryable<Role> Roles { get; protected set; }
        public IQueryable<MembershipAccount> Users { get; protected set; }

        public RoleProviderTestFixture()
        {
            Provider = new MongoRoleProvider();

            var config = new NameValueCollection();
            config["connectionStringName"] = "DefaultConnection";

            Provider.Initialize("MongoRoleProvider", config);
            
            SeedData(Provider.Database);
        }

        private async void SeedData(IMongoDatabase db)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));

            // Reset db
            await db.DropCollectionAsync("IDSequence");
            await db.DropCollectionAsync(Role.GetCollectionName());

            var rolesCol = db.GetCollection<Role>(Role.GetCollectionName());
            
            var roles = new List<Role>
                            {
                                new Role("Admin"),
                                new Role("User"),
                                new Role("Guest")
                            };

            await rolesCol.InsertManyAsync(roles);

            await db.DropCollectionAsync(MembershipAccount.GetCollectionName());
            var usersCol = db.GetCollection<MembershipAccount>(MembershipAccount.GetCollectionName());

            var user1 = new MembershipAccount("User1");
            user1.Roles.Add("Admin");  // this user is an Admin

            var users = new List<MembershipAccount>
                            {
                                user1,
                                new MembershipAccount("User2"),
                                new MembershipAccount("User3")
                            };

            await usersCol.InsertManyAsync(users);

            Roles = rolesCol.AsQueryable();
            Users = usersCol.AsQueryable();
        }
    }
}
