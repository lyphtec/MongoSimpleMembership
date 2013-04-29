using System;
using System.Configuration;
using System.Configuration.Provider;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using LyphTEC.MongoSimpleMembership.Extensions;
using LyphTEC.MongoSimpleMembership.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Options;
using MongoDB.Driver;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Linq;

namespace LyphTEC.MongoSimpleMembership.Services
{
    /// <summary>
    /// Wraps all interactions with MongoDB
    /// </summary>
    internal class MongoDataContext
    {
        static MongoDataContext()
        {
            // Init Mongo mappings & set configuration options etc
            DateTimeSerializationOptions.Defaults = new DateTimeSerializationOptions(DateTimeKind.Utc, BsonType.Document);

            if (!BsonClassMap.IsClassMapRegistered(typeof (MembershipAccount)))
            {
                BsonClassMap.RegisterClassMap<MembershipAccount>(cm =>
                                                                     {
                                                                         cm.AutoMap();
                                                                         cm.SetIdMember(cm.GetMemberMap(x => x.UserId));
                                                                         cm.IdMemberMap.SetRepresentation(BsonType.Int32).SetIdGenerator(IntIdGenerator.Instance);
                                                                         cm.SetIgnoreExtraElements(true);
                                                                     }
                    );
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof(Role)))
            {
                BsonClassMap.RegisterClassMap<Role>(cm =>
                                                        {
                                                            cm.AutoMap();
                                                            cm.SetIdMember(cm.GetMemberMap(x => x.RoleId));
                                                            cm.IdMemberMap.SetRepresentation(BsonType.Int32).SetIdGenerator(IntIdGenerator.Instance);
                                                            cm.SetIgnoreExtraElements(true);
                                                        }
                    );
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof (OAuthToken)))
            {
                BsonClassMap.RegisterClassMap<OAuthToken>(cm =>
                                                              {
                                                                  cm.AutoMap();
                                                                  cm.SetIdMember(cm.GetMemberMap(x => x.Id));
                                                                  cm.IdMemberMap.SetRepresentation(BsonType.ObjectId).SetIdGenerator(BsonObjectIdGenerator.Instance);
                                                                  cm.SetIgnoreExtraElements(true);
                                                              }
                    );
            }

            if (!BsonClassMap.IsClassMapRegistered(typeof (OAuthMembership)))
            {
                BsonClassMap.RegisterClassMap<OAuthMembership>(cm =>
                                                                   {
                                                                       cm.AutoMap();
                                                                       cm.SetIdMember(cm.GetMemberMap(x => x.Id));
                                                                       cm.IdMemberMap.SetRepresentation(BsonType.ObjectId).SetIdGenerator(BsonObjectIdGenerator.Instance);
                                                                       cm.SetIgnoreExtraElements(true);
                                                                   }
                    );
            }

            BsonSerializer.UseNullIdChecker = true;
            BsonSerializer.UseZeroIdChecker = true;
        }

        private readonly MongoDatabase _db;
        private readonly MongoCollection<Role> _roleCol;
        private readonly MongoCollection<MembershipAccount> _userCol;
        private readonly MongoCollection<OAuthToken> _oAuthTokenCol;
        private readonly MongoCollection<OAuthMembership> _oAuthMembershipCol;

        public MongoDataContext(string connectionNameOrString)
        {
            Contract.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(connectionNameOrString));

            // If it's a name, we lookup config setting
            var connSettings = ConfigurationManager.ConnectionStrings[connectionNameOrString];
            if (connSettings != null && !string.IsNullOrWhiteSpace(connSettings.ConnectionString))
                connectionNameOrString = connSettings.ConnectionString;

            _db = GetDatase(connectionNameOrString);

            _roleCol = _db.GetCollection<Role>(Role.GetCollectionName());
            _userCol = _db.GetCollection<MembershipAccount>(MembershipAccount.GetCollectionName());
            _oAuthTokenCol = _db.GetCollection<OAuthToken>(OAuthToken.GetCollectionName());
            _oAuthMembershipCol = _db.GetCollection<OAuthMembership>(OAuthMembership.GetCollectionName());

            // Check that we can connect to MongoDB server -- will throw an exception that should be caught in provider init
            _roleCol.EnsureUniqueIndex(x => x.RoleName);
        }
        
        private static MongoDatabase GetDatase(string connectionString)
        {
            Contract.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(connectionString));

            var url = MongoUrl.Create(connectionString);
            var client = new MongoClient(url);
            var server = client.GetServer();

            return server.GetDatabase(url.DatabaseName, WriteConcern.Acknowledged /* explicit setting */);
        }

        public MongoDatabase Database
        {
            get { return _db; }
        }

        public IQueryable<MembershipAccount> Users
        {
            get
            {
                _userCol.EnsureUniqueIndex(x => x.UserNameLower);
                _userCol.EnsureUniqueIndex(x => x.ConfirmationToken);
                //_userCol.EnsureUniqueIndex(x => x.PasswordVerificationToken);

                return _userCol.AsQueryable();
            }
        }

        public IQueryable<Role> Roles
        {
            get
            {
                _roleCol.EnsureUniqueIndex(x => x.RoleName);

                return _roleCol.AsQueryable();
            }
        }

        public IQueryable<OAuthToken> OAuthTokens
        {
            get
            {
                _oAuthTokenCol.EnsureUniqueIndex(x => x.Token);

                return _oAuthTokenCol.AsQueryable();
            }
        }

        public IQueryable<OAuthMembership> OAuthMemberships
        {
            get
            {
                _oAuthMembershipCol.EnsureUniqueIndex(x => x.Provider, x => x.ProviderUserId);

                return _oAuthMembershipCol.AsQueryable();
            } 
        }

        private static string GetCollectionName<T>()
        {
            var t = typeof (T);

            if (t == typeof (MembershipAccount))
                return MembershipAccount.GetCollectionName();

            if (t == typeof (Role))
                return Role.GetCollectionName();

            if (t == typeof (OAuthToken))
                return OAuthToken.GetCollectionName();

            if (t == typeof (OAuthMembership))
                return OAuthMembership.GetCollectionName();

            return string.Empty;
        }

        public bool Save<T>(T item) where T : class
        {
            Contract.Requires<ArgumentNullException>(item != null);

            var col = _db.GetCollection<T>(GetCollectionName<T>());
            
            var result = col.Save(item, WriteConcern.Acknowledged);
            
            ValidWriteResult(result);

            return result.Ok;
        }

        public bool RemoveById<T>(object id) where T : class
        {
            IMongoQuery query;

            if (typeof (T) == typeof (OAuthToken) || typeof(T) == typeof(OAuthMembership))
                query = Query.EQ("_id", id.ToString().ToBsonObjectId());
            else
                query = Query.EQ("_id", BsonValue.Create(id));
            
            var result = _db.GetCollection<T>(GetCollectionName<T>()).Remove(query, WriteConcern.Acknowledged);

            ValidWriteResult(result);

            return result.Ok;
        }

        public MembershipAccount GetUser(string userName)
        {
            return string.IsNullOrWhiteSpace(userName)
                       ? null
                       : Users.SingleOrDefault(x => x.UserNameLower == userName.ToLowerInvariant());
        }

        public Role GetRole(string roleName)
        {
            return string.IsNullOrWhiteSpace(roleName)
                       ? null
                       : Roles.SingleOrDefault(x => x.RoleName == roleName);
        }

        public OAuthToken GetToken(string token)
        {
            return string.IsNullOrWhiteSpace(token)
                       ? null
                       : OAuthTokens.SingleOrDefault(x => x.Token == token);
        }

        public OAuthMembership GetOAuthMembership(string provider, string providerUserId)
        {
            return string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerUserId)
                       ? null
                       : OAuthMemberships.SingleOrDefault(x => x.Provider.ToLowerInvariant() == provider.ToLowerInvariant() && x.ProviderUserId.ToLowerInvariant() == providerUserId.ToLowerInvariant());
        }

        public T FindOneById<T>(object id) where T : class
        {
            Contract.Requires<ArgumentNullException>(id != null);

            var value = typeof (T) == typeof (OAuthToken) || typeof (T) == typeof (OAuthMembership) ? id.ToString().ToBsonObjectId() : BsonValue.Create(id);
            
            return _db.GetCollection<T>(GetCollectionName<T>()).FindOneByIdAs<T>(value);
        }

        public void RemoveOAuthMembershipsByUserId(int userId)
        {
            try
            {
                var query = Query.EQ("UserId", BsonValue.Create(userId));
                var result = _oAuthMembershipCol.Remove(query, WriteConcern.Acknowledged);

                if (!result.Ok && result.HasLastErrorMessage)
                    Trace.TraceError("MongoDataContext.RemoveOAuthMembershipsByUserId() Remove ERROR: {0}", result.LastErrorMessage);
            }
            catch (Exception ex)
            {
                Trace.TraceError("MongoDataContext.RemoveOAuthMembershipsByUserId() ERROR: {0}", ex.ToString());
            }
        }

        private static void ValidWriteResult(GetLastErrorResult result)
        {
           if (!result.Ok && result.HasLastErrorMessage) 
               throw new ProviderException(result.LastErrorMessage);
        }
    }
}
