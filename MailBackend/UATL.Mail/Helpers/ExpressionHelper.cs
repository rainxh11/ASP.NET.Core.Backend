using System.Linq.Expressions;

namespace UATL.MailSystem.Helpers;

public class ExpressionHelper
{
    public static LambdaExpression StringToField<T, TResult>(string fieldName)
    {
        var parameter = Expression.Parameter(typeof(T));
        var accessor = Expression.PropertyOrField(parameter, fieldName);

        return Expression.Lambda(accessor, parameter);
    }
}