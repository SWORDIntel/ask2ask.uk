using Ask2Ask.Data;

namespace Ask2Ask.Services;

/// <summary>
/// Composite InferredRegionEngine with fallback strategy.
/// Attempts ONNX model inference first; falls back to heuristic if unavailable.
/// </summary>
public class CompositeInferredRegionEngine : IInferredRegionEngine
{
    private readonly ILogger<CompositeInferredRegionEngine> _logger;
    private readonly IInferredRegionEngine _primaryEngine;
    private readonly IInferredRegionEngine _fallbackEngine;

    public CompositeInferredRegionEngine(
        ILogger<CompositeInferredRegionEngine> logger,
        OnnxInferredRegionEngine onnxEngine,
        InferredRegionEngine heuristicEngine)
    {
        _logger = logger;
        _primaryEngine = onnxEngine;
        _fallbackEngine = heuristicEngine;
    }

    public async Task<InferredRegionResult?> InferAsync(Visit visit, AsnPingCorrelation? correlation)
    {
        try
        {
            // Try ONNX engine first
            var result = await _primaryEngine.InferAsync(visit, correlation);
            if (result != null)
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ONNX engine failed for visit {VisitId}, falling back to heuristic", visit.Id);
        }

        // Fall back to heuristic engine
        try
        {
            var result = await _fallbackEngine.InferAsync(visit, correlation);
            if (result != null)
            {
                return result;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallback heuristic engine also failed for visit {VisitId}", visit.Id);
        }

        return null;
    }
}
