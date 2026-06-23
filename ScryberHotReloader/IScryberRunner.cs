/// <summary>
/// Supplies the models for the current PDF render.
/// Exactly one class implementing this interface is allowed in the Model tab.
/// The runner is instantiated via dependency injection, so constructor parameters
/// that were registered in the Startup tab are automatically resolved.
/// </summary>
public interface IScryberRunner
{
    System.Collections.Generic.Dictionary<string, IScryberModel> GetModels();
}
