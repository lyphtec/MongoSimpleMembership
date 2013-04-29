using System;
using System.Configuration;
using MongoDB.Bson;

namespace LyphTEC.MongoSimpleMembership.Models
{
    public class OAuthToken
    {
        /// <summary>
        /// Instantiates a new OAuthToken
        /// </summary>
        /// <param name="token">Token</param>
        /// <param name="secret">Secret</param>
        public OAuthToken(string token, string secret)
        {
            Check.IsNotNull(token);
            Check.IsNotNull(secret);

            Token = token;
            Secret = secret;
        }

        public object Id { get; set; }
        public string Token { get; private set; }
        public string Secret { get; set; }

        /// <summary>
        /// Gets the name of the collection when stored in Mongo. By default it's &quot;webpages_OAuthToken&quot; (similar to the standard SimpleMembershipProvider in WebMatrix.WebData), but can be overridden in config by app setting &quot;MongoDBSimpleMembership:OAuthTokenName&quot;
        /// </summary>
        /// <returns></returns>
        public static string GetCollectionName()
        {
            var name = "webpages_OAuthToken";
            
            var setting = ConfigurationManager.AppSettings["MongoDBSimpleMembership:OAuthTokenName"];
            if (setting != null && !string.IsNullOrWhiteSpace(setting))
                name = setting;

            return name;
        }
    }
}
