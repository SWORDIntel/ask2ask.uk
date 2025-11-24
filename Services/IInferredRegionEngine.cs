using Ask2Ask.Data;

namespace Ask2Ask.Services;

/// <summary>
/// Inferred Region Engine
/// Predicts geographic region based on network timing, ASN, and behavioral signals
/// </summary>
public interface IInferredRegionEngine
{
    /// <summary>
    /// Infer region for a visit based on timing and metadata
    /// </summary>
    /// <param name="visit">The visit record</param>
    /// <param name="correlation">Optional ASN ping correlation data</param>
    /// <returns>Inferred region result or null if inference not possible</returns>
    Task<InferredRegionResult?> InferAsync(Visit visit, AsnPingCorrelation? correlation);
}
