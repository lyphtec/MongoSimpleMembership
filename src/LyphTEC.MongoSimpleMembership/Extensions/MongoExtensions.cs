using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LyphTEC.MongoSimpleMembership.Extensions
{
    public static class MongoExtensions
    {
        // ideas from http://stackoverflow.com/questions/671968/retrieving-property-name-from-lambda-expression
        static PropertyInfo GetPropertyInfo(Expression exp)
        {
            var lamb = exp as LambdaExpression;
            if (lamb == null)
                throw new InvalidOperationException("exp must be a LambdaExpression");

            var body = lamb.Body as MemberExpression;

            if (body == null)
            {
                var ubody = (UnaryExpression)lamb.Body;
                body = ubody.Operand as MemberExpression;
            }

            return body == null ? null : body.Member as PropertyInfo;
        }


        /// <summary>
        /// Converts a string to a <see cref="BsonObjectId"/>
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public static BsonObjectId ToBsonObjectId(this string id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : new BsonObjectId(new ObjectId(id));
        }
    }
}
