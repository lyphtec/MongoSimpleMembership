using System;
using System.Collections.Generic;
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
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

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

            // New way to use DateTimeSerializationOptions : http://grokbase.com/t/gg/mongodb-user/133vrg3p74/datetimeserializationoptions-defaults-obsolete-in-1-8
            var dtSerializer = new DateTimeSerializer(DateTimeKind.Utc, BsonType.Document);
            BsonSerializer.RegisterSerializer(dtSerializer);

            if (!BsonClassMap.IsClassMapRegistered(typeof (MembershipAccount)))
            {
                BsonClassMap.RegisterClassMap<MembershipAccount>(cm =>
                                                                     {
                                                                         cm.AutoMap();
                                                                         cm.SetIdMember(cm.GetMemberMap(x => x.UserId));
                                                                         cm.IdMemberMap.SetSerializer(new Int32Serializer(BsonType.Int32)).SetIdGenerator(IntIdGenerator<MembershipAccount>.Instance);
                                                                         cm.GetMemberMap(x => x.UserName).SetIsRequired(true);
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
                                                            cm.IdMemberMap.SetSerializer(new Int32Serializer(BsonType.Int32)).SetIdGenerator(IntIdGenerator<Role>.Instance);
                                                            cm.GetMemberMap(x => x.RoleName).SetIsRequired(true);
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
                                                                  cm.IdMemberMap.SetSerializer(new ObjectSerializer()).SetIdGenerator(BsonObjectIdGenerator.Instance);
                                                                  cm.GetMemberMap(x => x.Token).SetIsRequired(true);
                                                                  cm.GetMemberMap(x => x.Secret).SetIsRequired(true);
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
                                                                       cm.IdMemberMap.SetSerializer(new ObjectSerializer()).SetIdGenerator(BsonObjectIdGenerator.Instance);
                                                                       cm.GetMemberMap(x => x.Provider).SetIsRequired(true);
                                                                       cm.GetMemberMap(x => x.ProviderUserId).SetIsRequired(true);
                                                                       cm.SetIgnoreExtraElements(true);
                                                                   }
                    );
            }

            BsonSerializer.UseNullIdChecker = true;
            BsonSerializer.UseZeroIdChecker = true;
        }

        private readonly IMongoDatabase _db;
        private readonly IMongoCollection<Role> _roleCol;
        private readonly IMongoCollection<MembershipAccount> _userCol;
        private readonly IMongoCollection<OAuthToken> _oAuthTokenCol;
        private readonly IMongoCollection<OAuthMembership> _oAuthMembershipCol;

        private readonly CreateIndexOptions _uniqueIndexOptions = new CreateIndexOptions {Unique = true};

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

            SetupIndexes();
        }

        private void SetupIndexes()
        {
            _roleCol.Indexes.CreateOneAsync(Builders<Role>.IndexKeys.Ascending(x => x.RoleName), _uniqueIndexOptions);
            _userCol.Indexes.CreateOneAsync(Builders<MembershipAccount>.IndexKeys.Ascending(x => x.UserNameLower), _uniqueIndexOptions);
            _userCol.Indexes.CreateOneAsync(Builders<MembershipAccount>.IndexKeys.Ascending(x => x.ConfirmationToken), _uniqueIndexOptions);
            _oAuthTokenCol.Indexes.CreateOneAsync(Builders<OAuthToken>.IndexKeys.Ascending(x => x.Token), _uniqueIndexOptions);
            _oAuthMembershipCol.Indexes.CreateOneAsync(Builders<OAuthMembership>.IndexKeys.Ascending(x => x.Provider).Ascending(x => x.ProviderUserId), _uniqueIndexOptions);
        }

        private static IMongoDatabase GetDatase(string connectionString)
        {
            Contract.Requires<ArgumentNullException>(!string.IsNullOrWhiteSpace(connectionString));

            var url = MongoUrl.Create(connectionString);
            var client = new MongoClient(url);

            var settings = new MongoDatabaseSettings {WriteConcern = WriteConcern.Acknowledged};    // explicit

            return client.GetDatabase(url.DatabaseName, settings);
        }

        public IMongoDatabase Database => _db;

        public IQueryable<MembershipAccount> Users => _userCol.AsQueryable();

        public IQueryable<Role> Roles => _roleCol.AsQueryable();

        public IQueryable<OAuthToken> OAuthTokens => _oAuthTokenCol.AsQueryable();

        public IQueryable<OAuthMembership> OAuthMemberships => _oAuthMembershipCol.AsQueryable();

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

            try
            {
                var col = _db.GetCollection<T>(GetCollectionName<T>());

                var filter = new BsonDocument();
                var update = Builders<T>.Update.Set(x => x, item);

                var result = col.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true });
                var updateResult = result.Result;
                return updateResult.IsAcknowledged;
            }
            catch (Exception ex)
            {
                Trace.TraceError($"MongoDataContext.Save() ERROR: {ex}");
                throw new ProviderException(ex.Message);
            }
        }

        public bool RemoveById<T>(object id) where T : class
        {
            BsonDocument filter;

            if (typeof (T) == typeof (OAuthToken) || typeof(T) == typeof(OAuthMembership))
                filter = new BsonDocument("_id", id.ToString().ToBsonObjectId());
            else
                filter = new BsonDocument("_id", BsonValue.Create(id));

            var result = _db.GetCollection<T>(GetCollectionName<T>()).FindOneAndDeleteAsync(filter);

            if (result.IsFaulted && result.Exception != null)
                throw new ProviderException(result.Exception.Message);

            return result.IsCompleted;
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
            var filter = new BsonDocument("_id", value);
            
            var result = _db.GetCollection<T>(GetCollectionName<T>()).Find<T>(filter).FirstOrDefaultAsync();

            return result.Result;
        }

        public async void RemoveOAuthMemberships(int userId)
        {
            try
            {
                var filter = Builders<OAuthMembership>.Filter.Eq(x => x.UserId, BsonValue.Create(userId));

                await _oAuthMembershipCol.DeleteManyAsync(filter);
            }
            catch (Exception ex)
            {
                Trace.TraceError($"MongoDataContext.RemoveOAuthMemberships() ERROR: {ex}");
            }
        }
        
    }
}
