namespace KitLib.AI.Tests.Combat;

[CollectionDefinition(nameof(ModelDbTestCollection), DisableParallelization = true)]
public sealed class ModelDbTestCollection;

[Collection(nameof(ModelDbTestCollection))]
public abstract class ModelDbTestBase;
