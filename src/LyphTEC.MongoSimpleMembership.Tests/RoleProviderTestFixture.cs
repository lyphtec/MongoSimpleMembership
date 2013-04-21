using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using LyphTEC.MongoSimpleMembership.Models;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

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

        private void SeedData(MongoDatabase db)
        {
            if (db == null) throw new ArgumentNullException("db");

            // Reset db
            db.GetCollection("IDSequence").Drop();
            
            var rolesCol = db.GetCollection<Role>(Role.GetCollectionName());
            rolesCol.Drop();        // clear 1st
            
            var roles = new List<Role>
                            {
                                new Role("Admin"),
                                new Role("User"),
                                new Role("Guest")
                            };

            rolesCol.InsertBatch(roles);

            
            var usersCol = db.GetCollection<MembershipAccount>(MembershipAccount.GetCollectionName());
            usersCol.Drop();

            var user1 = new MembershipAccount("User1");
            user1.Roles.Add(roles.Single(x => x.RoleName == "Admin"));  // this user is an Admin

            var users = new List<MembershipAccount>
                            {
                                user1,
                                new MembershipAccount("User2"),
                                new MembershipAccount("User3")
                            };

            usersCol.InsertBatch(users);

            Roles = rolesCol.AsQueryable();
            Users = usersCol.AsQueryable();
        }
    }
}
