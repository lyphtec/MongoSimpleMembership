using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LyphTEC.MongoSimpleMembership
{
    /// <summary>
    /// Use to check method parameters with similar syntax as <see cref="Contract"/> class as Code Contracts are not supported in overridden methods on derived classes
    /// </summary>
    internal static class Check
    {
        public static void Requires<TException>(bool condition) where TException : Exception
        {
            if (condition)
                return;

            Requires<TException>(false, string.Empty);
        }

        public static void Requires<TException>(bool condition, string userMessage) where TException : Exception
        {
            if (condition)
                return;

            var t = typeof (TException);
            var ex = string.IsNullOrWhiteSpace(userMessage)
                         ? Activator.CreateInstance(t)
                         : Activator.CreateInstance(t, userMessage, null /* InnerException */);

            throw (TException)ex;
        }

        public static void IsNotNull(object arg)
        {
            if (ReferenceEquals(null, arg))
                throw new ArgumentNullException();

            var s = arg as string;
            if (s != null && string.IsNullOrWhiteSpace(s))
                throw new ArgumentNullException();
        }

        public static void IsNotNullOrDefault<T>(T arg)
        {
            IsNotNull(arg);

            if (ReferenceEquals(arg, default(T)))
                throw new ArgumentNullException();
        }
    }
}
