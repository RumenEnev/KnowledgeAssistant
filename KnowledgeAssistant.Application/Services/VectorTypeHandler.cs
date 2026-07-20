using Dapper;
using Pgvector;
using System.Data;

namespace KnowledgeAssistant.Application.Services
{
    public class VectorTypeHandler : SqlMapper.TypeHandler<Vector>
    {
        public override void SetValue(IDbDataParameter parameter, Vector? value)
        {
            parameter.Value = value;
        }

        public override Vector Parse(object value)
        {
            return (Vector)value;
        }
    }
}