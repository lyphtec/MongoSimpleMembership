using System;
using System.Collections.Specialized;
using System.Configuration;
using System.Configuration.Provider;
using System.Security.Cryptography;
using System.Web;
using System.Web.Hosting;

namespace LyphTEC.MongoSimpleMembership.Helpers
{
    internal static class Util
    {
        internal static string GetDefaultAppName()
        {
            try
            {
                var appName = HostingEnvironment.ApplicationVirtualPath;

                if (string.IsNullOrEmpty(appName))
                {
                    appName = System.Diagnostics.Process.GetCurrentProcess().MainModule.ModuleName;

                    var indexOfDot = appName.IndexOf('.');
                    if (indexOfDot != -1)
                        appName = appName.Remove(indexOfDot);
                }

                return string.IsNullOrEmpty(appName) ? "/" : appName;
            }
            catch
            {
                return "/";
            }
        }

        internal static T GetValueOrDefault<T>(NameValueCollection nvc, string key, Func<object, T> converter, T defaultIfNull)
        {
            var val = nvc[key];

            if (val == null)
                return defaultIfNull;

            try
            {
                return converter(val);
            }
            catch
            {
                return defaultIfNull;
            }
        }

        internal static void CheckAppNameLength(string appName)
        {
            if (appName.Length > 256)
                throw new ProviderException("applicationName must be less than 256 characters");
        }

        internal static void CheckConnectionStringSettings(string connStringName, ConnectionStringSettings settings)
        {
            if (settings == null || string.IsNullOrWhiteSpace(settings.ConnectionString))
                throw new ProviderException(string.Format("Specified connection '{0}' cannot be found or is invalid. Please check the connectionStrings section in config.", connStringName));

        }

        internal static string GenerateToken(byte tokenSizeInBytes = 16)
        {
            using (var prng = new RNGCryptoServiceProvider())
            {
                return GenerateToken(prng, tokenSizeInBytes);
            }
        }

        internal static string GenerateToken(RandomNumberGenerator generator, byte tokenSizeInBytes = 16)
        {
            var tokenBytes = new byte[tokenSizeInBytes];
            generator.GetBytes(tokenBytes);
            return HttpServerUtility.UrlTokenEncode(tokenBytes);
        }
    }
}
