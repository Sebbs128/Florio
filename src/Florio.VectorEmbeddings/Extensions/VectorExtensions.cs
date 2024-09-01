using System.Text;

namespace Florio.VectorEmbeddings.Extensions;
public static class VectorExtensions
{
    public static string ToSparseRepresentation(this ReadOnlyMemory<float> vector)
    {
        var sb = new StringBuilder();
        sb.Append('[');
        bool firstItem = true;
        for (var i = 0; i < vector.Length; i++)
        {
            if (vector.Span[i] > 0)
            {
                if (!firstItem)
                {
                    sb.Append(", ");
                }
                else
                {
                    firstItem = false;
                }

                sb.Append($"{{{i}: {vector.Span[i]}}}");
            }
        }
        sb.Append(']');
        return sb.ToString();
    }
}
